using BossMod.CombatAssistance.Planning;
using System.Diagnostics;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

// Keep moving attacks in RSR's normal selection. Only automatic delivery is
// deferred to the one native executor, so available damage remains observable
// even when the player is already inside melee range.
internal sealed class RsrRotationalDelivery : IDisposable
{
    private readonly RsrRuntimeAccess _runtime;
    private readonly ActionManagerEx _actions;
    private readonly RsrSelectionScopeOwner _owner;
    private readonly Func<bool> _allowed;
    private readonly Func<uint, bool>? _deferFallback;
    private readonly Func<MovementPlan, MovementPlanningContext, int, bool>? _approvePreparation;
    private readonly RsrSelectionScopeOwner.Registration _registration;
    private Type? _center;
    private PropertyInfo? _force, _preview, _command, _special, _queued;
    private PropertyInfo? _duty;
    private RsrSupportDutySlot? _slot;
    private object? _rotation;
    private (MovementPlan Plan, MovementPlanningContext Context, int Stage, MovementExecution Execution)? _requested;
    private (MovementPlan Plan, int Stage, long At)? _approved;
    private (MovementPlan Plan, int Stage, long At)? _attempted;
    internal bool Available => _owner.Available && _allowed() && Resolve();
    internal ImmutableHashSet<uint> Owned => Available ? NativeRotationalMobility.Definitions.Select(d => d.Id).ToImmutableHashSet() : [];
    internal bool ExplicitlyControlled => Resolve() && ExplicitUse();
    internal string Status { get; private set; } = "Rotational delivery retained by RSR";
    internal RsrRotationalDelivery(RsrRuntimeAccess runtime, ActionManagerEx actions, RsrSelectionScopeOwner owner, Func<bool> allowed,
        Func<uint, bool>? deferFallback = null, Func<MovementPlan, MovementPlanningContext, int, bool>? approvePreparation = null)
    {
        _runtime = runtime; _actions = actions; _owner = owner; _allowed = allowed; _deferFallback = deferFallback; _approvePreparation = approvePreparation;
        // Wrap existing duty/support slots, so their healing and explicit duty
        // choices precede a planner-offered general GCD.
        _registration = owner.Register(Enter, Failed, order: 20);
    }
    private bool Resolve()
    {
        try
        {
            if (!_runtime.TryGetSupportContext(out _, out var basic, out _)) return false;
            var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            if (_center == center) return true;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var action = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            _force = action.GetProperty("ForceEnable", flags) ?? throw new MissingMemberException("RSR forced action");
            _preview = action.GetProperty("ActionPreview", flags) ?? throw new MissingMemberException("RSR preview");
            _command = center.GetProperty("CommandStatus", flags) ?? throw new MissingMemberException("RSR command status");
            _special = center.GetProperty("SpecialType", flags) ?? throw new MissingMemberException("RSR special command");
            _queued = center.GetProperty("CommandNextAction", flags) ?? throw new MissingMemberException("RSR command queue");
            _duty = center.GetProperty("CurrentDutyRotation", flags) ?? throw new MissingMemberException("RSR duty rotation");
            _center = center;
            return true;
        }
        catch (Exception error) { Failed(error); return false; }
    }
    private IDisposable? Enter()
    {
        if (!_allowed()) { ResetPlan(); if (_deferFallback == null) return null; }
        if (!Resolve()) return null;
        Status = "Original RSR automatic delivery retained";
        var delivery = _actions.EnterAutomaticDeliveryGate(Permit);
        RsrSupportDutySlot? entered = null;
        try
        {
            if (_requested != null && _runtime.TryGetSupportContext(out _, out var basic, out var rotation))
            {
                if (!ReferenceEquals(rotation, _rotation))
                {
                    _rotation = rotation;
                    var remaining = RsrSupportDutySlot.RemainingAbilities(rotation, _center!);
                    _slot = new(_duty!, remaining, args => TryOffer(args, basic, rotation, gcd: false),
                        args => TryOffer(args, basic, rotation, gcd: true));
                }
                entered = _slot;
                entered!.Enter();
            }
            return new Scope(() => { try { entered?.Exit(); } finally { delivery.Dispose(); } });
        }
        catch { try { entered?.Exit(); } finally { delivery.Dispose(); } throw; }
    }

    // A request is only an exact executable stage from the single movement
    // owner. It is not a command queue entry or a prediction of RSR's rotation.
    internal bool MaySubmit(MovementExecution execution, MovementPlan plan, MovementPlanningContext context, int stage, out string? rejection)
    {
        rejection = null;
        if (execution.ActionId != 0 && _center != null && ExplicitUse())
        {
            ResetPlan();
            rejection = "explicit RSR action owns delivery; automatic movement action yielded";
            return false;
        }
        if (!RequiresRotationSelection(context, stage, plan) && !IsPreparation(plan, context, stage)) return true;
        _requested = (plan, context, stage, execution);
        var approved = _approved is { } approval && ReferenceEquals(approval.Plan, plan) && approval.Stage == stage
            && Stopwatch.GetElapsedTime(approval.At).TotalSeconds is >= 0 and < .25;
        if (!approved && _attempted is { } attempt && ReferenceEquals(attempt.Plan, plan) && attempt.Stage == stage
            && Stopwatch.GetElapsedTime(attempt.At).TotalSeconds is >= 0 and < .25)
            rejection = "original RSR selection rejected this planned action; retain other legal routes";
        if (!approved && execution.DepartureSeconds <= 0)
            rejection = "required native departure cannot wait for planned-action selection";
        if (rejection != null) _requested = null;
        return approved;
    }

    internal static bool RequiresRotationSelection(MovementPlanningContext context, int stage, MovementPlan plan)
        => stage >= 0 && stage < plan.Stages.Length && NativeRotationalMobility.Contains(plan.Stages[stage].ActionId)
            && !context.Actions.Any(a => a.Id == plan.Stages[stage].ActionId && a.TargetId == plan.Stages[stage].AnchorId);

    internal static bool YieldToCurrent(uint requested, uint current, bool currentGcd, bool currentlyUsable, float animationLock, float departure)
        => requested != 0 && current != 0 && current != requested && !currentGcd && !NativeRotationalMobility.Contains(current)
            && currentlyUsable && float.IsFinite(animationLock) && animationLock > 0 && departure >= animationLock;

    internal static bool RequiresGcdSelection(MovementPlanningContext context, int stage, MovementPlan plan)
        => stage >= 0 && stage < plan.Stages.Length
            && NativeRotationalMobility.Contains(plan.Stages[stage].ActionId)
            && !context.Actions.Any(a => a.Id == plan.Stages[stage].ActionId && a.TargetId == plan.Stages[stage].AnchorId)
            && context.ResolveMobility(context.Origin, plan.Stages[stage])?.IsGCD == true;

    internal static bool IsPreparation(MovementPlan plan, MovementPlanningContext context, int stage)
        => stage >= 0 && stage < plan.Stages.Length && plan.Stages[stage].Kind == MovementStageKind.Cast
            && context.ActionFor(plan.Stages[stage])?.Preparation is PlanningPreparationMode.Optional or PlanningPreparationMode.Committed;

    private bool TryOffer(object?[] arguments, Assembly basic, object rotation, bool gcd)
    {
        try { return Offer(arguments, basic, rotation, gcd); }
        catch (Exception error)
        {
            _approved = null;
            Status = "Planned movement selection unavailable; original RSR choices retained";
            Service.Logger.Verbose(error, Status);
            return false;
        }
    }

    private bool Offer(object?[] arguments, Assembly basic, object rotation, bool gcd)
    {
        if (_requested is not { } request || !_allowed() || ExplicitUse() || arguments.Length != (gcd ? 1 : 2)
            || (IsPreparation(request.Plan, request.Context, request.Stage)
                ? request.Context.ActionFor(request.Plan.Stages[request.Stage])!.IsGCD
                : request.Context.ResolveMobility(request.Context.Origin, request.Plan.Stages[request.Stage])?.IsGCD == true) != gcd
            || Stopwatch.GetElapsedTime(request.Context.Identity.CapturedTimestamp).TotalSeconds is < 0 or >= .25
            || request.Execution.Kind != MovementExecutionKind.Action && !(request.Execution.Kind == MovementExecutionKind.Wait
                && IsPreparation(request.Plan, request.Context, request.Stage))
            || !MovementPlanValidation.Validate(request.Plan, request.Context, request.Stage, true, continuingCommittedRoute: true).Allowed)
            return false;
        // Selection ahead of a real setup clock is pending, not a rejected action.
        if (IsPreparation(request.Plan, request.Context, request.Stage)
            && request.Context.ActionFor(request.Plan.Stages[request.Stage])!.Earliest > 0) return false;
        _approved = null;
        _attempted = (request.Plan, request.Stage, Stopwatch.GetTimestamp());
        var stage = request.Plan.Stages[request.Stage];
        var actionType = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
        var ids = basic.GetType("RotationSolver.Basic.Data.ActionID", true)!;
        var property = RsrNativeMobilitySelection.Property(rotation.GetType(), ids, actionType, stage.ActionId);
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (property == null || rotation.GetType().GetProperty(property, instance)?.GetValue(rotation) is not { } action
            || Convert.ToUInt32(action.GetType().GetProperty("AdjustedID", instance)?.GetValue(action) ?? 0u) != stage.ActionId)
            return false;
        var config = action.GetType().GetProperty("Config", instance)?.GetValue(action);
        if (config?.GetType().GetField("_isEnable", instance)?.GetValue(config) is not true) return false;
        var canUse = actionType.GetMethod("CanUse") ?? throw new MissingMethodException("RSR original CanUse");
        var parameters = canUse.GetParameters();
        var check = new object?[parameters.Length];
        for (var i = 1; i < parameters.Length; ++i) check[i] = parameters[i].DefaultValue;
        if (canUse.Invoke(action, check) is not true || check[0] == null
            || Convert.ToUInt32(check[0]!.GetType().GetProperty("AdjustedID", instance)?.GetValue(check[0]) ?? 0u) != stage.ActionId) return false;
        if (stage.AnchorId != 0)
        {
            var target = action.GetType().GetProperty("Target", instance)?.GetValue(action);
            var actor = target?.GetType().GetProperty("Target", instance)?.GetValue(target);
            if (actor is not Dalamud.Game.ClientState.Objects.Types.IGameObject wrapper || !wrapper.IsValid()
                || wrapper.Address == 0 || wrapper.GameObjectId != stage.AnchorId) return false;
        }
        if (IsPreparation(request.Plan, request.Context, request.Stage)
            && _approvePreparation?.Invoke(request.Plan, request.Context, request.Stage) != true) return false;
        arguments[gcd ? 0 : 1] = check[0];
        _approved = (request.Plan, request.Stage, Stopwatch.GetTimestamp());
        Status = $"Original RSR selection approved native planned action {stage.ActionId}";
        return true;
    }

    private bool ExplicitUse() => _force?.GetValue(null) is true || _preview?.GetValue(null) is true
        || RsrCommandPolicy.HasExplicitCommand(_command?.GetValue(null) as Enum, _special?.GetValue(null) as Enum,
            _queued?.GetValue(null) != null);

    internal void ResetPlan() { _requested = null; _approved = null; _attempted = null; }

    private sealed class Scope(Action exit) : IDisposable
    {
        public void Dispose() => exit();
    }
    private bool Permit(ActionID action)
    {
        try
        {
            var explicitUse = ExplicitUse();
            if (!explicitUse && action.Type == ActionType.Spell && _deferFallback?.Invoke(action.ID) == true)
            {
                Status = "Verified melee recovery retains this GCD; ranged filler would delay melee";
                return false;
            }
            return Permits(action.Type, action.ID, explicitUse, _allowed());
        }
        catch (Exception error) { Failed(error); return true; }
    }
    internal static bool Permits(ActionType type, uint id, bool explicitUse, bool assistanceAllowed)
        => type != ActionType.Spell || !NativeRotationalMobility.Contains(id) || explicitUse || !assistanceAllowed;
    private void Failed(Exception error)
    {
        _center = null;
        ResetPlan();
        _slot = null; _rotation = null;
        Status = "Rotational delivery scope unavailable: " + error.GetBaseException().Message;
        Service.Logger.Verbose(error, "Rotational movement delivery scope unavailable; original RSR delivery retained.");
    }
    public void Dispose() { _registration.Dispose(); _center = null; ResetPlan(); _slot = null; _rotation = null; }
}
