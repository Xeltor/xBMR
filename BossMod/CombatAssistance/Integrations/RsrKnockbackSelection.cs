using BossMod.Autorotation;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Native planning participates in RSR selection. The original CanUse,
// concrete action and delivery path remain authoritative; no saved config lease.
internal sealed class RsrKnockbackSelection : IDisposable
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly RotationModuleManager _manager;
    private readonly RsrRuntimeAccess _runtime;
    private readonly Func<bool> _allowed;
    private readonly Func<bool> _tankMitigation;
    private readonly RsrSelectionScopeOwner.Registration _registration;
    private RsrOriginalActionSelectionLease? _lease;
    private object? _rotation;
    private object? _yieldedRotation;
    private Type? _center;
    private PropertyInfo? _preview;
    private PropertyInfo? _forced;
    private PropertyInfo? _config;
    private FieldInfo? _enabled;
    private RsrSupportDutySlot? _duty;
    private MethodInfo? _canUse;
    private DateTime _worldTime;
    private long _observed;
    private uint _action;
    private (MovementPlan Plan, MovementPlanningContext Context)? _prepared;
    private (MovementPlan Plan, MovementPlanningContext Context, int Stage)? _authorized;
    private MovementPlan? _approved;
    internal string Status { get; private set; } = "RSR retains knockback immunity";
    internal void ResetPlan() { _prepared = null; _authorized = null; _approved = null; }
    internal static uint ActionFor(Class job) => job.GetClassCategory(allowLimited: false) switch
    {
        ClassCategory.Healer or ClassCategory.Caster => 7559,
        ClassCategory.Tank or ClassCategory.Melee or ClassCategory.PhysRanged => 7548,
        _ => 0
    };

    internal RsrKnockbackSelection(RotationModuleManager manager, RsrRuntimeAccess runtime,
        Func<bool> allowed, Func<bool> tankMitigation, RsrSelectionScopeOwner scopes)
    {
        _manager = manager; _runtime = runtime; _allowed = allowed; _tankMitigation = tankMitigation;
        _registration = scopes.Register(Enter, Failed, order: 20);
    }

    internal void Update()
    {
        if (_worldTime != _manager.WorldState.CurrentTime)
        { _worldTime = _manager.WorldState.CurrentTime; _observed = Stopwatch.GetTimestamp(); }
        if (!_allowed()) Release();
    }

    private object? Center(string name) => _center?.GetProperty(name, Static)?.GetValue(null);
    private bool Manual() => _preview?.GetValue(null) is true || _forced?.GetValue(null) is true
        || Center("CurrentInterceptedAction") != null
        || RsrCommandPolicy.HasExplicitCommand(Center("CommandStatus") as Enum, Center("SpecialType") as Enum)
        || !Targeting.NativeOffensiveTargeting.MayConstrainSelection(Center("IsManual") as bool?, Center("IsHenched") as bool?,
            Center("IsTargetOnly") as bool?, Center("IsPvP") as bool?);
    private bool Fresh() => _allowed() && _rotation != null && ReferenceEquals(Center("CurrentRotation"), _rotation)
        && Stopwatch.GetElapsedTime(_observed).TotalSeconds is >= 0 and < .25;
    private bool Configured()
    {
        var config = _lease == null ? null : _config?.GetValue(_lease.OriginalAction);
        return config != null && _enabled?.DeclaringType?.IsInstanceOfType(config) == true && _enabled.GetValue(config) is true;
    }
    internal uint AvailableAction
    {
        get
        {
            try
            {
                return Fresh() && !Manual() && Configured() ? _action : 0;
            }
            catch (Exception error) { Failed(error); return 0; }
        }
    }

    private IDisposable? Enter()
    {
        if (!_allowed()) { Release(); _yieldedRotation = null; return null; }
        if (!_runtime.TryGetSupportContext(out _, out var basic, out var rotation)) { Release(); return null; }
        if (ReferenceEquals(rotation, _yieldedRotation)) return null;
        var player = _manager.WorldState.Party.Player();
        if (player == null || RsrRuntimeAccess.Job(rotation) != (uint)player.Class) { Release(); return null; }
        var id = ActionFor(player.Class);
        if (id == 0) { Release(); return null; }
        if (!ReferenceEquals(rotation, _rotation) || id != _action)
        {
            Release(); _rotation = rotation; _action = id;
            _center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            _preview = action.GetProperty("ActionPreview", Static) ?? throw new MissingMemberException("RSR ActionPreview");
            _forced = action.GetProperty("ForceEnable", Static) ?? throw new MissingMemberException("RSR ForceEnable");
            _lease = RsrOriginalActionSelectionLease.Acquire(rotation, id == 7559 ? "SurecastPvE" : "ArmsLengthPvE",
                (original, method, args) =>
                {
                    var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                    if (result is not true || args is not { Length: > 0 } || !ReferenceEquals(args[0], original))
                    { _approved = null; _authorized = null; return result; }
                    try
                    {
                        if (!Fresh() || Manual() || !Configured())
                        { _approved = null; _authorized = null; return result; }
                        if (!Defer()) return result;
                        if (args is { Length: > 0 }) args[0] = null;
                        return false;
                    }
                    catch (Exception error) { Failed(error); return result; }
                });
            _config = _lease.OriginalAction.GetType().GetProperty("Config", Instance);
            var config = _config?.GetValue(_lease.OriginalAction);
            _enabled = config?.GetType().GetField("_isEnable", Instance);
            {
                _canUse = action.GetMethod("CanUse") ?? throw new MissingMethodException("RSR role action CanUse");
                var parameters = _canUse.GetParameters();
                if (parameters.Length == 0 || !parameters[0].IsOut || parameters.Skip(1).Any(p => !p.HasDefaultValue))
                    throw new MissingMethodException("RSR role action defaults changed");
                var remaining = RsrSupportDutySlot.RemainingAbilities(rotation, _center);
                _duty = new(_center.GetProperty("CurrentDutyRotation", Static) ?? throw new MissingMemberException("CurrentDutyRotation"),
                    args => GuardSelection(() => remaining(args)), args => GuardSelection(() => OfferPlannedImmunity(args) || OfferTankMitigation(args)));
            }
        }
        if (!Fresh() || Manual() || !Configured()) { _approved = null; _authorized = null; return null; }
        _lease!.Resume();
        try { if (_prepared != null || _action == 7548 && _tankMitigation()) _duty?.Enter(); }
        catch { Leave(); throw; }
        Status = "original RSR immunity selection participates in native planning";
        return new Scope(Leave);
    }

    private void Leave()
    {
        try { _duty?.Exit(); }
        finally { _lease?.Suspend(); }
    }

    private object? OriginalAvailableAction()
    {
        if (!Fresh() || Manual() || !Configured() || _lease?.Owned != true || _canUse == null) return null;
        var parameters = _canUse.GetParameters();
        var check = new object?[parameters.Length];
        for (var index = 1; index < parameters.Length; ++index) check[index] = parameters[index].DefaultValue;
        // Keep the real action and every original availability/status predicate.
        return RsrSupportActionProxy.Forward(_lease.OriginalAction, _canUse, check) is true
            && ReferenceEquals(check[0], _lease.OriginalAction) ? check[0] : null;
    }

    private bool OfferPlannedImmunity(object?[] arguments)
    {
        if (_prepared is not { } prepared || !prepared.Plan.Stages.Any(stage =>
            stage.Utility is { Effect: PlanningUtilityEffect.KnockbackImmunity } utility && utility.ActionId == _action)
            || !Valid(prepared.Plan, prepared.Context, 0)) return false;
        var original = OriginalAvailableAction();
        if (original == null) { _approved = null; _authorized = null; return false; }
        // Approval and execution refer to this exact verified publication.
        if (Defer()) return false;
        if (!ReferenceEquals(_approved, prepared.Plan)) return false;
        arguments[1] = original;
        return true;
    }

    private bool OfferTankMitigation(object?[] arguments)
    {
        if (_action != 7548 || !_tankMitigation()) return false;
        var original = OriginalAvailableAction();
        if (original == null) return false;
        arguments[1] = original;
        Status = "original RSR Arm's Length offered for nearby trash attackers";
        return true;
    }

    private bool GuardSelection(Func<bool> selection)
    {
        try { return selection(); }
        catch (Exception error) { Failed(error); return false; }
    }

    private bool Valid(MovementPlan plan, MovementPlanningContext context, int stage)
    {
        var actual = Service.ObjectTable.LocalPlayer;
        if (!Fresh() || Manual() || !Configured() || actual == null || !actual.IsValid() || actual.Address == 0
            || actual.GameObjectId != context.Identity.ActorId || context.Identity.WorldTime != _manager.WorldState.CurrentTime) return false;
        return MovementPlanValidation.Validate(plan, context, stage, true, Stopwatch.GetTimestamp(),
            new(actual.Position.X, actual.Position.Z)).Allowed;
    }

    private bool Defer()
    {
        if (_prepared is not { } prepared || !Valid(prepared.Plan, prepared.Context, 0))
        { _approved = null; _authorized = null; return false; }
        var utility = prepared.Plan.Stages.FirstOrDefault(stage => stage.Utility?.Effect == PlanningUtilityEffect.KnockbackImmunity);
        if (utility == null)
            return prepared.Plan.Status == MovementSearchStatus.Complete;
        if (utility.ActionId != _action) return false;
        _approved = prepared.Plan;
        if (_authorized is { } authorized && ReferenceEquals(authorized.Plan, prepared.Plan)
            && authorized.Stage >= 0 && authorized.Stage < authorized.Plan.Stages.Length
            && authorized.Plan.Stages[authorized.Stage].Utility?.ActionId == _action
            && Valid(authorized.Plan, authorized.Context, authorized.Stage))
        {
            _authorized = null;
            Status = "verified immunity stage reached; original RSR action released";
            return false;
        }
        Status = "original RSR approval received; awaiting verified native immunity stage";
        return true;
    }

    internal void Consider(MovementPlan plan, MovementPlanningContext context)
    {
        if (AvailableAction == 0 || context.KnockbackPhases is not { Phases.IsEmpty: false }
            || !context.Utilities.Any(u => u.Effect == PlanningUtilityEffect.KnockbackImmunity && u.ActionId == _action)
            || !plan.HasVerifiedRoute || !MovementPlanValidation.Validate(plan, context, 0, true).Allowed)
        { _prepared = null; _approved = null; _authorized = null; return; }
        _prepared = (plan, context);
    }
    internal bool MayCommit(MovementPlan plan) => !plan.Stages.Any(stage => stage.Utility?.Effect == PlanningUtilityEffect.KnockbackImmunity)
        || ReferenceEquals(plan, _approved) && AvailableAction != 0;
    internal void ObserveExecution(MovementPlan plan, MovementPlanningContext context, int stage, MovementExecution execution)
    {
        _authorized = null;
        if (ReferenceEquals(plan, _approved) && execution.Kind == MovementExecutionKind.Wait && execution.ActionId == _action
            && stage >= 0 && stage < plan.Stages.Length && plan.Stages[stage].Utility?.Effect == PlanningUtilityEffect.KnockbackImmunity)
            _authorized = (plan, context, stage);
    }
    private void Release()
    {
        _duty?.Exit(); _duty = null; _canUse = null;
        _lease?.Dispose(); _lease = null; _rotation = null; _action = 0; _center = null;
        _config = null; _enabled = null; _prepared = null; _approved = null; _authorized = null;
        Status = "RSR retains knockback immunity";
    }
    private void Failed(Exception error)
    {
        var rotation = _rotation;
        Release(); _yieldedRotation = rotation;
        Status = "immunity selection unavailable or changed externally; RSR retained";
        Service.Logger.Verbose(error, Status);
    }
    public void Dispose() { _registration.Dispose(); Release(); _yieldedRotation = null; }
    private sealed class Scope(Action leave) : IDisposable { public void Dispose() => leave(); }
}
