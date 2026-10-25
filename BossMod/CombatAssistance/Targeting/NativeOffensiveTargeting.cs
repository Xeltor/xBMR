using BossMod.Autorotation;
using BossMod.Autorotation.MiscAI;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace BossMod.CombatAssistance.Targeting;

internal sealed record OffensiveTargetDecision(ulong Target, Vector3 Position, uint Action, string Reason,
    bool Published, DateTime ObservedAt, int GuardedActions = 0, int UnsupportedActions = 0, double CaptureMilliseconds = 0);

// Framework-only capture and intent collection. RSR's original selection checks
// retain rotation/resource/status authority. The native target field is published
// before movement modules consume the selected current opportunity.
internal sealed partial class NativeOffensiveTargeting : IDisposable
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly RotationModuleManager _manager;
    private readonly RsrRuntimeAccess _runtime;
    private readonly Func<AssistancePreferences> _preferences;
    private readonly Func<bool> _allowed;
    private readonly Func<bool> _mayRetarget;
    private readonly Func<ulong> _recovery;
    private readonly RsrSelectionScopeOwner.Registration _registration;
    private readonly Dictionary<(Type, string), PropertyInfo?> _members = [];
    private readonly Dictionary<uint, Metadata> _currentActions = [];
    private Assembly? _basic;
    private object? _rotation;
    private Type? _center;
    private PropertyInfo? _preview;
    private PropertyInfo? _force;
    private Func<IBattleChara, bool>? _attackable;
    private Func<IBattleChara, HashSet<uint>, HashSet<long>, bool>? _validity;
    private Func<long[]>? _stops;
    private ulong _player;
    private ushort _zone;
    private ulong _observedTarget;
    private ulong _expectedTarget;
    private DateTime _manualUntil;
    private DateTime _worldTime;
    private long _worldObserved;
    private long _selectionObserved;
    private (uint Action, ulong Target, ImmutableArray<ulong> Recipients) _observedSelection;
    private DateTime _nextLog;
    private bool _disposed;
    private string _selectionReason = "not observed";
    internal OffensiveTargetDecision? Decision { get; private set; }
    internal string Status { get; private set; } = "inactive";
    internal bool Enabled => _preferences().PickBetterAoeTarget || _preferences().KeepTrashTargetSelected || _preferences().TankTargetLostTrashAggro;
    private sealed record Metadata(object Action, object Setting, OffensiveActionGeometry Geometry);

    internal NativeOffensiveTargeting(RotationModuleManager manager, RsrRuntimeAccess runtime,
        Func<AssistancePreferences> preferences, Func<bool> allowed, Func<bool> mayRetarget, Func<ulong> recovery,
        RsrSelectionScopeOwner scopes)
    {
        _manager = manager; _runtime = runtime; _preferences = preferences; _allowed = allowed;
        _mayRetarget = mayRetarget; _recovery = recovery;
        _registration = scopes.Register(Enter, Failed, order: 0);
    }

    internal void Update()
    {
        var world = _manager.WorldState;
        var player = world.Party.Player();
        if (_worldTime != world.CurrentTime) { _worldTime = world.CurrentTime; _worldObserved = Stopwatch.GetTimestamp(); }
        if (_player != player?.InstanceID || _zone != world.CurrentZone)
        {
            Reset(); _player = player?.InstanceID ?? 0; _zone = world.CurrentZone;
            _observedTarget = player?.TargetID ?? 0;
            if (_observedTarget != 0) _manualUntil = world.CurrentTime.AddSeconds(3);
        }
        if (!Enabled || !_allowed() || player == null)
        {
            _targetSetup.Release(); _restoreTarget = 0;
            Decision = null; Status = "inactive under native activation or action authority"; return;
        }
        UpdateTargetSetup(player);
        if (player.TargetID != _observedTarget)
        {
            if (player.TargetID != _expectedTarget && (player.TargetID != 0
                || world.Actors.Find(_observedTarget) is { IsTargetable: true, IsDead: false, IsDestroyed: false }))
                // Existing player/external target handoff, not an IPC timeout.
                _manualUntil = world.CurrentTime.AddSeconds(3);
            _observedTarget = player.TargetID;
        }
        try
        {
            if (!Prepare()) { Decision = null; _selectionObserved = 0; Status = "RSR rotation unavailable"; }
            else if (Status.StartsWith("inactive", StringComparison.Ordinal) || Status == "RSR rotation unavailable")
                Status = "Native target constraints ready; waiting for original RSR callback";
        }
        catch (Exception error) { Failed(error); }
    }

    private bool Prepare()
    {
        if (!_runtime.TryGetSupportContext(out _, out var basic, out var rotation)
            || _manager.WorldState.Party.Player() is not { } player || RsrRuntimeAccess.Job(rotation) != (uint)player.Class) return false;
        if (ReferenceEquals(_rotation, rotation) && _basic == basic) return true;
        _members.Clear(); _currentActions.Clear(); _eligibility.Clear(); _targetSetup.Release();
        _setupEstimate = 0; _lastSelectionPass = 0;
        _basic = basic; _rotation = rotation;
        _center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
        var actionInterface = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
        _preview = actionInterface.GetProperty("ActionPreview", Static) ?? throw new MissingMemberException("RSR action preview");
        _force = actionInterface.GetProperty("ForceEnable", Static) ?? throw new MissingMemberException("RSR forced action");
        var helper = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!;
        _attackable = helper.GetMethod("IsAttackable", Static, [typeof(IBattleChara)])!.CreateDelegate<Func<IBattleChara, bool>>();
        var targets = basic.GetType("RotationSolver.Basic.Helpers.TargetHelper", true)!;
        _validity = targets.GetMethod("ValidityCheck", Static)!.CreateDelegate<Func<IBattleChara, HashSet<uint>, HashSet<long>, bool>>();
        _stops = basic.GetType("RotationSolver.Basic.Helpers.MarkingHelper", true)!.GetMethod("GetStopTargets", Static)!.CreateDelegate<Func<long[]>>();
        return true;
    }

    private object? Get(object value, string name)
    {
        var type = value.GetType();
        if (!_members.TryGetValue((type, name), out var property))
            _members[(type, name)] = property = type.GetInterface("RotationSolver.Basic.Actions.IBaseAction")?.GetProperty(name)
                ?? type.GetInterface("RotationSolver.Basic.Actions.IAction")?.GetProperty(name) ?? type.GetProperty(name, Instance);
        return property?.GetValue(value);
    }
    private object? Center(string name) => _center?.GetProperty(name, Static)?.GetValue(null);
    private bool Manual() => _force?.GetValue(null) is true || _preview?.GetValue(null) is true
        // CurrentInterceptedAction is historical and survives expired commands.
        // Only the live queue may hold target ownership beyond the mode gates.
        || RsrCommandPolicy.HasExplicitCommand(Center("CommandStatus") as Enum, Center("SpecialType") as Enum,
            Center("CommandNextAction") != null);
    private bool Fresh(object rotation) => !_disposed && _allowed() && ReferenceEquals(_rotation, rotation)
        && ReferenceEquals(Center("CurrentRotation"), rotation)
        && Stopwatch.GetElapsedTime(_worldObserved).TotalSeconds is >= 0 and < 0.25;

    private OffensiveTargetFrame? CaptureFrame()
    {
        var world = _manager.WorldState;
        var player = world.Party.Player();
        if (player == null || _attackable == null || _validity == null || _stops == null) return null;
        var blacklist = Center("BlacklistedNameIds") is IEnumerable ids ? ids.Cast<object>().Select(Convert.ToUInt32).ToHashSet() : [];
        var stops = _stops().Where(id => id != 0).ToHashSet();
        var party = world.Party.WithoutSlot(includeDead: true, excludeAlliance: true).Select(a => a.InstanceID).ToHashSet();
        var forced = _manager.Hints.AuthoritativeTarget?.InstanceID ?? 0;
        var syncedToFate = Utils.IsPlayerSyncedToFate(world);
        var actors = ImmutableArray.CreateBuilder<OffensiveTargetActor>();
        foreach (var enemy in _manager.Hints.PotentialTargets)
        {
            var actor = enemy.Actor;
            if (actor.IsDestroyed || !float.IsFinite(actor.PosRot.X) || !float.IsFinite(actor.PosRot.Y) || !float.IsFinite(actor.PosRot.Z)) continue;
            var engaged = actor.AggroPlayer || enemy.Priority >= 0
                && (enemy.RsrTargetDecision?.MayAcquire == true || enemy.RsrFallbackTarget
                    || FateTargetPolicy.Matches(actor.FateID, world.Client.ActiveFate.ID,
                        syncedToFate, world.Client.ActiveFate.Progress))
                || actor.InCombat && (party.Contains(actor.TargetID)
                || actor.FateID != 0 && actor.FateID == world.Client.ActiveFate.ID)
                || enemy.ExplicitTargetRequirement == true || actor.InstanceID == forced && enemy.Priority >= 0
                || enemy.Priority > 0 && _manager.Bossmods.ActiveModule?.StateMachine.ActiveState != null;
            var wrapper = Service.ObjectTable.SearchById(actor.InstanceID) as IBattleChara;
            var permitted = actor.IsTargetable && !actor.IsDead
                && wrapper != null && wrapper.IsValid()
                && wrapper.Address != 0 && _attackable(wrapper) && _validity(wrapper, blacklist, stops);
            // Reuse RSR's specific priority rules, including its user options,
            // even while Henched. Preference is not permission to pull idle mobs.
            var preferred = permitted && engaged && enemy.Priority >= 0 && enemy.RsrTargetPreference;
            actors.Add(new(Value(actor), permitted ? enemy.Priority : AIHints.Enemy.PriorityForbidden,
                engaged, enemy.ForbidDOTs, enemy.Priority <= AIHints.Enemy.PriorityUndesirable || !engaged, preferred));
        }
        var settings = _preferences();
        var selected = SelectionFresh() ? ReadSelection() : default;
        if (!SameSelection(selected, _observedSelection)) selected = default;
        var hardTarget = player.TargetID;
        if (_targetSetup.Pending is { } pending && HardTarget() == pending.Target) hardTarget = pending.Target;
        return new(Value(player), hardTarget, forced,
            !_mayRetarget() || world.CurrentTime < _manualUntil || _manager.Hints.AutomaticRetargetingAllowed == false,
            settings.KeepTrashTargetSelected, settings.PickBetterAoeTarget,
            settings.TankTargetLostTrashAggro ? _recovery() : 0, actors.ToImmutable(), selected.Action,
            selected.Recipients, selected.Target);
    }

    private Metadata? ReadMetadata(object action)
    {
        var id = Convert.ToUInt32(Get(action, "AdjustedID") ?? 0);
        var setting = Get(action, "Setting");
        var targetInfo = Get(action, "TargetInfo");
        if (id == 0 || setting == null || targetInfo == null
            || !Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)) return null;
        var friendly = Get(setting, "IsFriendly") is true;
        var hybrid = id is 3571 or 7439 or 8324 or 25874;
        if (!hybrid && (row.AttackType.RowId == 0 || friendly || !row.CanTargetHostile && row.EffectRange == 0)) return null;
        var range = Convert.ToSingle(Get(targetInfo, "Range"));
        var effect = Convert.ToSingle(Get(targetInfo, "EffectRange"));
        if (!float.IsFinite(range) || range < 0 || !float.IsFinite(effect) || effect < 0) return null;
        return new(action, setting, new(id, row.CastType, range, effect, row.XAxisModifier > 0 ? row.XAxisModifier / 2f : 0,
            row.TargetArea, friendly, !friendly && Get(setting, "TargetStatusProvide") is Array { Length: > 0 }));
    }

    private IDisposable? Enter()
    {
        var clock = Stopwatch.StartNew();
        if (!Enabled || !_allowed()) return null;
        if (!Prepare() || _rotation == null || _basic == null) { Status = "RSR rotation unavailable before selection"; return null; }
        if (!SelectionMode()) { Status = "RSR explicit Manual, target-only, PvP or unknown mode retains control"; return null; }
        var rotation = _rotation;
        if (!Fresh(rotation)) { Status = "Native actor/rotation snapshot stale before selection"; return null; }
        if (CaptureFrame() is not { } frame) { Status = "Native target frame unavailable"; return null; }
        if (!frame.NeedsActionConstraints)
        {
            _selectionObserved = 0;
            _currentActions.Clear();
            Decision = null;
            Status = "No target or engaged enemy; original RSR idle selection retained";
            return null;
        }
        if (Get(rotation, "AllBaseActions") is not IEnumerable actions) { Status = "Original RSR action catalogue unavailable"; return null; }
        var scopes = new List<IDisposable>();
        var unsupported = 0;
        var checkedActions = new HashSet<uint>();
        _currentActions.Clear();
        try
        {
            scopes.Add(RsrAutomaticTargetScope.Enter(_basic));
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var action in actions)
            {
                if (action == null) continue;
                try
                {
                    if (ReadMetadata(action) is not { } metadata || !seen.Add(metadata.Setting)) continue;
                    _currentActions[metadata.Geometry.Id] = metadata;
                    scopes.Add(new RsrOffensivePredicateScope(metadata.Setting,
                        () => { checkedActions.Add(metadata.Geometry.Id); return Fresh(rotation) && frame.AllowsArea(metadata.Geometry); },
                        id => { checkedActions.Add(metadata.Geometry.Id); return Fresh(rotation) && AllowsPrimary(frame, metadata.Geometry, id); }, Manual, Failed));
                }
                catch (Exception error) { unsupported++; Failed(error); }
            }
            if (_currentActions.Count == 0) { Status = "No supported offensive actions; RSR targeting retained"; DisposeAll(scopes); return null; }
            var elapsed = clock.Elapsed.TotalMilliseconds;
            var guarded = _currentActions.Count;
            Status = $"Native target constraints guard {guarded} actions; {unsupported} unsupported";
            return new Scope(() =>
            {
                try
                {
                    if (Fresh(rotation))
                    {
                        var selected = ReadSelection();
                        var observed = selected.Action != 0 && checkedActions.Contains(selected.Action);
                        var passStamp = Stopwatch.GetTimestamp();
                        if (checkedActions.Count != 0)
                        {
                            var interval = _lastSelectionPass == 0 ? 0 : Stopwatch.GetElapsedTime(_lastSelectionPass, passStamp).TotalSeconds;
                            if (interval is > 0 and < 0.25) _setupEstimate = Math.Max(_setupEstimate, (float)interval);
                            _lastSelectionPass = passStamp;
                        }
                        Status = $"Native target constraints guard {guarded} actions; {unsupported} unsupported; "
                            + $"selected {selected.Action}, checked {checkedActions.Count}/{observed}, recipients "
                            + (selected.Recipients.IsDefault ? "unknown" : selected.Recipients.Length.ToString())
                            + $"; {_selectionReason}";
                        // RSR may skip a framework cycle. Reading an unchanged
                        // cached result is not evidence of a fresh selection.
                        if (selected.Action != 0 && checkedActions.Contains(selected.Action))
                        {
                            _observedSelection = selected;
                            _selectionObserved = Stopwatch.GetTimestamp();
                        }
                        var next = ReadImmediateTarget();
                        var hard = Service.TargetManager.Target;
                        var actual = hard != null && hard.IsValid() && hard.Address != 0 ? hard.GameObjectId : 0;
                        _expectedTarget = _targetSetup.Pending?.Target
                            ?? (actual != 0 && (actual == next || actual == selected.Target) ? actual : 0);
                        if (SelectionFresh() && SameSelection(selected, _observedSelection)
                            && selected.Target != 0 && _manager.WorldState.Actors.Find(selected.Target) is { } target)
                            Decision = new(selected.Target, target.PosRot.XYZ(), selected.Action,
                                "RSR selected within native target constraints", false, DateTime.UtcNow, _currentActions.Count, unsupported, elapsed);
                    }
                    else Status = $"Guarded {guarded} actions; selection exit rejected by native authority, identity or freshness";
                }
                finally { DisposeAll(scopes); }
            });
        }
        catch { DisposeAll(scopes); throw; }
    }

    private bool AllowsPrimary(OffensiveTargetFrame frame, OffensiveActionGeometry action, ulong id)
        => frame.AllowsNative(action, id);

    private (uint Action, ulong Target, ImmutableArray<ulong> Recipients) ReadSelection()
    {
        _selectionReason = "rotation unavailable";
        if (_rotation == null || !_runtime.TryGetSupportContext(out var plugin, out _, out var current)
            || !ReferenceEquals(current, _rotation)) return default;
        var updater = plugin.GetType("RotationSolver.Updaters.ActionUpdater", true)!;
        var action = updater.GetProperty("NextGCDAction", Static)?.GetValue(null)
            ?? updater.GetProperty("NextAction", Static)?.GetValue(null);
        _selectionReason = "selected action/target unavailable";
        if (action == null || Get(action, "Target") is not { } selection
            || Get(selection, "Target") is not IBattleChara target || !target.IsValid() || target.Address == 0) return default;
        var targetId = target.GameObjectId;
        _selectionReason = "selected action not a supported hostile opportunity";
        if (ReadMetadata(action) is not { Geometry.Friendly: false } metadata) return default;
        _currentActions[metadata.Geometry.Id] = metadata;
        var recipients = ImmutableArray.CreateBuilder<ulong>();
        _selectionReason = "selected recipient set unavailable or partial";
        // Keep unknown/partial recipients distinct from a complete empty set.
        if (Get(selection, "AffectedTargets") is not IEnumerable affected)
            return (metadata.Geometry.Id, targetId, default);
        foreach (var value in affected)
        {
            if (value is not IGameObject recipient || !recipient.IsValid() || recipient.Address == 0)
                return (metadata.Geometry.Id, targetId, default);
            var id = recipient.GameObjectId;
            if (id == 0) return (metadata.Geometry.Id, targetId, default);
            if (!recipients.Contains(id)) recipients.Add(id);
        }
        if (targetId != _player && !recipients.Contains(targetId)) recipients.Add(targetId);
        _selectionReason = "complete current action/target/recipient identities";
        return (metadata.Geometry.Id, targetId, recipients.ToImmutable());
    }

    // Henched intentionally sets IsManual: RSR delegates target ownership to
    // BMR. Accept native constraints there without changing that mode flag or
    // broadening RSR's original hard-target-only candidate set.
    internal static bool MayConstrainSelection(bool? manual, bool? henched, bool? targetOnly, bool? pvp)
        => pvp == false && targetOnly == false && (manual == false || henched == true);
    private bool SelectionMode() => MayConstrainSelection(Center("IsManual") as bool?, Center("IsHenched") as bool?,
        Center("IsTargetOnly") as bool?, Center("IsPvP") as bool?);

    private bool SelectionFresh() => _selectionObserved != 0
        && Stopwatch.GetElapsedTime(_selectionObserved).TotalSeconds is >= 0 and < 0.25;
    private static bool SameSelection((uint Action, ulong Target, ImmutableArray<ulong> Recipients) first,
        (uint Action, ulong Target, ImmutableArray<ulong> Recipients) second)
        => first.Action != 0 && first.Action == second.Action && first.Target == second.Target
            && !first.Recipients.IsDefault && !second.Recipients.IsDefault
            && first.Recipients.Length == second.Recipients.Length && first.Recipients.All(second.Recipients.Contains);

    private ulong ReadImmediateTarget()
    {
        if (!_runtime.TryGetSupportContext(out var plugin, out _, out _)) return 0;
        var action = plugin.GetType("RotationSolver.Updaters.ActionUpdater", true)!
            .GetProperty("NextAction", Static)?.GetValue(null);
        return action != null && Get(action, "Target") is { } selected
            && Get(selected, "Target") is IGameObject target && target.IsValid() && target.Address != 0
                ? target.GameObjectId : 0;
    }

    internal bool ResolveTargetIntent()
    {
        if (_targetSetup.Pending != null || _restoreTarget != 0) return MaintainTargetSetup();
        if (!Enabled || !_allowed() || !_manager.Hints.NativeTargetIntentCollectionRequested
            || _manager.Hints.AssistedTargetApplied || !_mayRetarget()
            || _manager.WorldState.CurrentTime < _manualUntil || _manager.Hints.AuthoritativeTarget != null
            || _manager.Hints.AutomaticRetargetingAllowed == false
            || _manager.HasAutomaticTargetModule && _manager.Hints.AutomaticRetargetingAllowed == null) return false;
        try
        {
            if (!Prepare() || _rotation == null || !Fresh(_rotation) || Manual()
                || !SelectionMode() || CaptureFrame() is not { } frame) return false;
            var current = _manager.WorldState.Actors.Find(frame.Current);
            var preferred = frame.Acquisition;
            // Henched RSR cannot select an action on a preferred enemy until we
            // establish its hard target. Respect native/manual acquisition gates.
            if (frame.Actors.Any(a => a.Actor.Id == preferred && a.RsrPreferred)
                && frame.PriorityAcquisition(preferred) != 0 && _manager.Hints.ForcedTarget == null)
                return PublishTarget(frame, preferred, 0, "Honor RSR's specific target preference within native permissions; awaiting original RSR action selection");
            var currentExcluded = frame.Actors.Any(a => a.Actor.Id == frame.Current && a.Priority == AIHints.Enemy.PriorityForbidden);
            var acquisition = frame.MissingTargetAcquisition(!currentExcluded
                && current is { IsTargetable: true, IsDead: false, IsDestroyed: false });
            if (acquisition != 0 && _manager.Hints.ForcedTarget == null)
                return PublishTarget(frame, acquisition, 0, "Reacquire a permitted native combat target; awaiting original RSR action selection");
            if (!SelectionFresh()) return false;
            var selection = ReadSelection();
            if (!SameSelection(selection, _observedSelection)) return false;
            var target = selection.Target != 0 && selection.Target != frame.Player.Id ? selection.Target : 0;
            if (target != 0 && _currentActions.TryGetValue(selection.Action, out var selectedAction)
                && !frame.AllowsNative(selectedAction.Geometry, target)) return false;
            // Changes to a usable target still require RSR's original validated
            // action, geometry and recipient evidence.
            if (target == 0 || frame.Current == 0 && !frame.Acquire && _manager.Hints.ForcedTarget == null) return false;
            return PublishTarget(frame, target, selection.Action, "Current RSR action and native movement share the selected target");
        }
        catch (Exception error) { Failed(error); return false; }
    }

    private bool PublishTarget(OffensiveTargetFrame frame, ulong target, uint action, string reason)
    {
        var actor = _manager.WorldState.Actors.Find(target);
        if (actor is not { IsTargetable: true, IsDead: false, IsDestroyed: false }
            || !frame.Eligible(target) || _manager.Hints.ForcedTarget?.InstanceID == target
            || !_manager.Hints.TrySetAssistedTarget(actor)) return false;
        _expectedTarget = target;
        Decision = new(target, actor.PosRot.XYZ(), action, reason, true, DateTime.UtcNow);
        return true;
    }

    private static ExecutionActor Value(Actor actor) => new(actor.InstanceID, actor.Position, actor.PosRot.Y,
        actor.HitboxRadius, actor.Rotation, actor.IsDead || actor.IsDestroyed, actor.Omnidirectional);
    private static void DisposeAll(List<IDisposable> scopes)
    {
        List<Exception>? errors = null;
        for (var i = scopes.Count - 1; i >= 0; --i)
            try { scopes[i].Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        scopes.Clear();
        if (errors != null) throw new AggregateException("Native target scope cleanup failed", errors);
    }
    private void Failed(Exception error)
    {
        Decision = null; _selectionObserved = 0;
        Status = "Native target assistance unavailable; original rotation retained";
        if (DateTime.UtcNow < _nextLog) return;
        _nextLog = DateTime.UtcNow.AddSeconds(10);
        Service.Logger.Verbose(error, Status);
    }
    private void Reset()
    {
        Decision = null; _currentActions.Clear(); _members.Clear(); _rotation = null; _basic = null;
        _manualUntil = default; _expectedTarget = 0; _selectionObserved = 0; _observedSelection = default;
        _targetSetup.Release(); _restoreTarget = 0; _eligibility.Clear(); _rejectedTargets.Clear(); _lastSelectionPass = 0; _setupEstimate = 0;
    }
    public void Dispose() { _disposed = true; _registration.Dispose(); Reset(); }
    private sealed class Scope(Action leave) : IDisposable { public void Dispose() => leave(); }
}
