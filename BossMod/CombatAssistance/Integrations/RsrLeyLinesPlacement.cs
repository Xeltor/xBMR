using BossMod.Autorotation;
using BossMod.CombatAssistance.Planning;
using System.Diagnostics;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Selection remains in RSR's original callback and on its concrete BaseAction.
// The native planner can defer an already-approved ability within this GCD slot;
// it never predicts a rotation action or sends a separate ability request.
internal sealed class RsrLeyLinesPlacement : IDisposable
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly RotationModuleManager _manager;
    private readonly ActionManagerEx _actions;
    private readonly RsrRuntimeAccess _runtime;
    private readonly Func<bool> _allowed;
    private readonly RsrSelectionScopeOwner.Registration _registration;
    private readonly NativePlacementOpportunity _opportunity = new();
    private RsrOriginalActionSelectionLease? _lease;
    private object? _rotation;
    private object? _yieldedRotation;
    private Assembly? _provider;
    private PropertyInfo? _config;
    private FieldInfo? _enabled;
    private PropertyInfo? _preview;
    private PropertyInfo? _forced;
    private Type? _center;
    private DateTime _worldTime;
    private DateTime _gcd;
    private long _worldObserved;
    private (MovementPlan Plan, MovementPlanningContext Context, int Stage)? _authorized;
    private DateTime _nextLog;
    private (MovementPlan Plan, MovementPlanningContext Context)? _prepared;
    internal string Status { get; private set; } = "inactive";

    internal RsrLeyLinesPlacement(RotationModuleManager manager, ActionManagerEx actions, RsrRuntimeAccess runtime,
        Func<bool> allowed, RsrSelectionScopeOwner scopes)
    {
        _manager = manager; _actions = actions; _runtime = runtime; _allowed = allowed;
        _registration = scopes.Register(Enter, Failed, order: 10);
    }
    internal void Update()
    {
        if (_worldTime != _manager.WorldState.CurrentTime)
        { _worldTime = _manager.WorldState.CurrentTime; _worldObserved = Stopwatch.GetTimestamp(); }
        var last = _manager.LastCast;
        if (last.Data is { } data && ActionDefinitions.Instance[data.Action]?.IsGCD == true && last.Time > _gcd) _gcd = last.Time;
        if (!_allowed()) { Release(); Status = "inactive under native movement/action authority"; }
        else
        {
            try { ObserveAvailability(); }
            catch (Exception error) { Failed(error); }
        }
    }
    private unsafe void ObserveAvailability()
    {
        if (_lease?.OriginalAction is not { } action || _manager.WorldState.Party.Player() is not { } player
            || _actions.CastTimeRemaining > 0 || player.FindStatus((uint)BLM.SID.LeyLines) != null)
        { _opportunity.Retire(); return; }
        var config = _config?.GetValue(action);
        var id = ActionID.MakeSpell(BLM.AID.LeyLines);
        var definition = ActionDefinitions.Instance[id];
        if (config == null || _enabled?.DeclaringType?.IsInstanceOfType(config) != true || _enabled.GetValue(config) is not true || definition?.IsUnlocked(_manager.WorldState, player) != true
            || _actions.GetActionStatus(id, player.InstanceID, checkCastingActive: false) != 0)
        { _opportunity.Retire(); return; }
        _opportunity.Observe(player.InstanceID, _manager.WorldState.CurrentZone, player.TargetID, _gcd,
            Stopwatch.GetTimestamp(), _actions.EffectiveAnimationLock, Math.Max(0, _actions.GCD()),
            definition.InstantAnimLock + _actions.AnimationLockDelayEstimate, player.Position);
    }
    private object? Center(string name) => _center?.GetProperty(name, Static)?.GetValue(null);
    private bool Manual() => _preview?.GetValue(null) is true || _forced?.GetValue(null) is true
        || Center("CurrentInterceptedAction") != null
        || RsrCommandPolicy.HasExplicitCommand(Center("CommandStatus") as Enum, Center("SpecialType") as Enum)
        || !Targeting.NativeOffensiveTargeting.MayConstrainSelection(Center("IsManual") as bool?, Center("IsHenched") as bool?,
            Center("IsTargetOnly") as bool?, Center("IsPvP") as bool?);
    private bool Fresh() => _allowed() && _rotation != null && ReferenceEquals(Center("CurrentRotation"), _rotation)
        && Stopwatch.GetElapsedTime(_worldObserved).TotalSeconds is >= 0 and < .25;
    private IDisposable? Enter()
    {
        if (!_allowed()) { _opportunity.Reset(); return null; }
        if (!_runtime.TryGetSupportContext(out _, out var basic, out var rotation)) { Release(); _yieldedRotation = null; return null; }
        if (ReferenceEquals(rotation, _yieldedRotation))
        { Status = "Ley Lines selection changed externally; native placement yielded"; return null; }
        if (!ReferenceEquals(rotation, _rotation) || basic != _provider)
        {
            Release(); _rotation = rotation; _provider = basic;
            _center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            _preview = action.GetProperty("ActionPreview", Static) ?? throw new MissingMemberException("RSR ActionPreview");
            _forced = action.GetProperty("ForceEnable", Static) ?? throw new MissingMemberException("RSR ForceEnable");
            _lease = RsrOriginalActionSelectionLease.Acquire(rotation, "LeyLinesPvE", (original, method, args) =>
            {
                var result = RsrOriginalActionSelectionProxy.Forward(original, method, args);
                try
                {
                    if (result is not true || !Fresh() || Manual())
                    { _opportunity.Retire(); _authorized = null; return result; }
                    if (!Defer()) return result;
                    if (args is { Length: > 0 }) args[0] = null;
                    return false;
                }
                catch (Exception error) { Failed(error); return result; }
            });
            _config = _lease.OriginalAction.GetType().GetProperty("Config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var config = _config?.GetValue(_lease.OriginalAction);
            _enabled = config?.GetType().GetField("_isEnable", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        if (!Fresh() || Manual()) { _opportunity.Retire(); return null; }
        _lease!.Resume();
        Status = "original Ley Lines selection participates in native placement";
        return new Scope(_lease.Suspend);
    }
    private bool Defer()
    {
        var player = _manager.WorldState.Party.Player();
        var actual = Service.ObjectTable.LocalPlayer;
        if (player == null || actual == null || !actual.IsValid() || actual.Address == 0
            || actual.GameObjectId != player.InstanceID || _actions.CastTimeRemaining > 0
            || player.FindStatus((uint)BLM.SID.LeyLines) != null)
        { _opportunity.Retire(); return false; }
        // Capture the real pose before any external selection/native submission.
        var position = new WPos(actual.Position.X, actual.Position.Z);
        var now = Stopwatch.GetTimestamp();
        var intent = _opportunity.Capture(player.InstanceID, _manager.WorldState.CurrentZone, player.TargetID, now);
        if (intent == null) return false;
        if (!_opportunity.Approved)
        {
            // Preparation may search native availability, but cannot move or
            // defer native timing until original CanUse approves the SAME plan.
            if (_prepared is not { } prepared || prepared.Plan.FieldPlacement?.Opportunity != intent.Opportunity
                || prepared.Plan.FieldPlacementGain <= 0
                || !MovementPlanValidation.Validate(prepared.Plan, prepared.Context, 0, true, now, position).Allowed)
            { _opportunity.Retire(); return false; }
            _opportunity.Approve(intent.Opportunity);
        }
        if (_authorized is { } authorized && authorized.Plan.FieldPlacement?.Opportunity == intent.Opportunity
            && authorized.Stage < authorized.Plan.Stages.Length
            && authorized.Plan.Stages[authorized.Stage] is { Kind: MovementStageKind.FieldPlacement } stage
            && position.AlmostEqual(stage.To, MovementPlanValidation.ArrivalTolerance)
            && MovementPlanValidation.Validate(authorized.Plan, authorized.Context, authorized.Stage, true, now, position).Allowed)
        {
            _opportunity.Retire(); _authorized = null;
            Status = "native route reached; original RSR Ley Lines selection released";
            return false;
        }
        Status = "native search evaluating Ley Lines within the original current GCD weave";
        return true;
    }
    internal PlanningFieldPlacement? Capture()
    {
        if (!Fresh() || Manual() || _manager.WorldState.Party.Player() is not { } player
            || player.FindStatus((uint)BLM.SID.LeyLines) != null || _actions.CastTimeRemaining > 0)
        { _opportunity.Retire(); _authorized = null; return null; }
        return _opportunity.Capture(player.InstanceID, _manager.WorldState.CurrentZone, player.TargetID, Stopwatch.GetTimestamp());
    }
    internal void Consider(MovementPlan plan, MovementPlanningContext context)
    {
        if (plan.HasVerifiedRoute && plan.FieldPlacement?.Opportunity == _opportunity.Active
            && plan.FieldPlacementGain > 0 && plan.Stages.Any(s => s.Kind == MovementStageKind.FieldPlacement)
            && MovementPlanValidation.Validate(plan, context, 0, true).Allowed)
            _prepared = (plan, context);
    }
    internal bool MayCommit(MovementPlan plan) => !plan.Stages.Any(s => s.Kind == MovementStageKind.FieldPlacement)
        || _opportunity.Approved && plan.FieldPlacement?.Opportunity == _opportunity.Active;
    internal void ObserveExecution(MovementPlan plan, MovementPlanningContext context, int stage, MovementExecution execution)
    {
        _authorized = null;
        if (context.FieldPlacement?.Opportunity != _opportunity.Active || _opportunity.Active == 0) return;
        if (plan.Status == MovementSearchStatus.Complete && !plan.Stages.Any(s => s.Kind == MovementStageKind.FieldPlacement))
        { _opportunity.Retire(); Status = "no verified placement gain; original RSR timing retained"; return; }
        if (execution.Kind == MovementExecutionKind.Wait && execution.ActionId == PlanningFieldPlacement.ActionId
            && stage >= 0 && stage < plan.Stages.Length && plan.Stages[stage].Kind == MovementStageKind.FieldPlacement)
            _authorized = (plan, context, stage);
    }
    private void Release()
    {
        _lease?.Dispose(); _lease = null; _rotation = null; _provider = null; _config = null; _enabled = null;
        _opportunity.Reset(); _authorized = null; _prepared = null;
    }
    private void Failed(Exception error)
    {
        var yielded = _rotation;
        Release(); _yieldedRotation = yielded;
        Status = "Ley Lines placement unavailable; original RSR selection retained";
        if (DateTime.UtcNow < _nextLog) return;
        _nextLog = DateTime.UtcNow.AddSeconds(10); Service.Logger.Verbose(error, Status);
    }
    public void Dispose() { _registration.Dispose(); Release(); _yieldedRotation = null; }
    private sealed class Scope(Action leave) : IDisposable { public void Dispose() => leave(); }
}
