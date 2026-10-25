using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;

namespace BossMod.CombatAssistance.Integrations;

// External RSR objects are confined to this framework-thread reader. No reflected
// action, target wrapper, delegate or assembly reaches the native search worker.
internal sealed record RsrActionEvidence(uint ActionId, ulong TargetId, Vector3 TargetPoint,
    ImmutableArray<ulong> AffectedRecipients, bool Friendly, bool GroundTargeted,
    int CastType, float Range, float EffectRange, float HalfWidth,
    float ReadyIn, float Recast, float QueueAhead, float CastTime, float AnimationLock,
    byte CastTimeProc = 0, float WithoutProcCastTime = 0, float BaseCastTime = 0, Positional Positional = Positional.Any, float BaseRecast = 0);

internal sealed partial class RsrActionEvidenceSource(IDalamudPluginInterface pluginInterface, IDataManager data, IPluginLog log) : IDisposable
{
    private Assembly? _assembly;
    private PropertyInfo? _nextGcd;
    private PropertyInfo? _nextAction;
    private PropertyInfo? _rotation;
    private PropertyInfo? _remain;
    private PropertyInfo? _total;
    private PropertyInfo? _ahead;
    private FieldInfo? _positionals;
    private Type? _redMage;
    private PropertyInfo? _redMageCombo;
    private PropertyInfo? _redMageStacks;
    private PropertyInfo? _manafication;
    private RsrRotationSelectionTracker _selection = new();
    private RsrRotationSelectionTracker _immediateSelection = new();
    private readonly Dictionary<(Type, string), PropertyInfo?> _properties = [];
    private readonly Dictionary<Type, FieldInfo?> _enabledFields = [];
    internal string Status { get; private set; } = "RSR action evidence not requested";
    internal bool NoSelectedAction { get; private set; }
    internal (uint Id, ulong Target, bool Gcd)? ImmediateSelection { get; private set; }

    internal ulong TargetOf(object action)
        => Read(action, "Target") is { } target && Read(target, "Target") is IGameObject wrapper
            && wrapper.IsValid() && wrapper.Address != 0 ? wrapper.GameObjectId : 0;

    internal RsrActionEvidence? Capture(ActionManagerEx manager, Class job) => Capture(manager, job, false);
    internal RsrActionEvidence? CaptureImmediatePlanningAction(ActionManagerEx manager, Class job) => Capture(manager, job, true);

    // An elective field move must not take the weave of a selected offensive or
    // unsupported ability whose execution/resource effect is not in this plan.
    internal bool FieldActionMayWeave(Class job, RsrActionEvidence? gcd, RsrActionEvidence? immediate)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("RSR field ordering requires the framework thread.");
        try
        {
            if (_rotation?.GetValue(null) is not { } rotation || Convert.ToUInt32(Read(rotation, "Job") ?? 0) != (uint)job
                || _nextAction?.GetValue(null) is not { } next) return false;
            var id = Convert.ToUInt32(Read(next, "AdjustedID") ?? 0u);
            var definition = RsrNativeActionDefinition.Find(id, static action => ActionDefinitions.Instance[action]);
            return definition != null && FieldActionMayWeave(id, definition.IsGCD, gcd?.ActionId ?? 0, immediate?.ActionId ?? 0);
        }
        catch (Exception error)
        {
            log.Verbose($"RSR field-action ordering unavailable: {error.GetBaseException().Message}");
            return false;
        }
    }

    internal static bool FieldActionMayWeave(uint selected, bool isGcd, uint gcd, uint immediate)
        => selected != 0 && (isGcd ? selected == gcd : selected == immediate || selected == (uint)BLM.AID.LeyLines);

    private unsafe RsrActionEvidence? Capture(ActionManagerEx manager, Class job, bool immediate)
    {
        if (!immediate) NoSelectedAction = false;
        else ImmediateSelection = null;
        if (!Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("RSR evidence must be captured on the framework thread.");
        try
        {
            // Inspect the named live plugin on each capture. A time-based loaded check
            // could keep an old action alive after unloading or replacing RSR.
            var assembly = ReflectionObjectSearch.FindLoadedPlugin(pluginInterface,
                "RotationSolver.RotationSolverPlugin", "RotationSolver", "Rotation Solver Reborn")?.GetType().Assembly;
            if (assembly == null)
            {
                Reset("RSR is not loaded");
                return null;
            }
            if (_assembly != assembly)
                Resolve(assembly);
            if (_nextGcd == null || _rotation == null || immediate && _nextAction == null)
                return null;
            var rotation = _rotation.GetValue(null);
            var action = (immediate ? _nextAction : _nextGcd)!.GetValue(null);
            if (!immediate) NoSelectedAction = rotation != null && Convert.ToUInt32(Read(rotation, "Job") ?? 0) == (uint)job && action == null;
            if (rotation == null || Convert.ToUInt32(Read(rotation, "Job") ?? 0) != (uint)job
                || !(immediate ? _immediateSelection : _selection).Observe(rotation, action) || action == null)
            {
                Status = "Waiting for RSR's current rotation to select an action";
                return null;
            }
            return CaptureSelectedAction(manager, action, immediate);
        }
        catch (Exception error)
        {
            var reason = "RSR action evidence unavailable: " + error.GetBaseException().Message;
            if (Status != reason)
                log.Verbose(reason);
            Reset(reason);
            return null;
        }
    }

    private unsafe RsrActionEvidence? CaptureSelectedAction(ActionManagerEx manager, object action, bool immediate)
    {
        var targetResult = Read(action, "Target");
        if (targetResult == null || Read(targetResult, "Target") is not IGameObject wrapper || !wrapper.IsValid() || wrapper.Address == 0)
        {
            Status = "RSR selected target is unavailable";
            return null;
        }
        // Capture identity before consulting any more external data. Native actor
        // lookup supplies the geometry later; never retain the wrapper.
        var targetId = wrapper.GameObjectId;
        var point = Read(targetResult, "Position") is Vector3 position ? position : wrapper.Position;
        var id = Convert.ToUInt32(Read(action, "AdjustedID") ?? 0u);
        var targetInfo = Read(action, "TargetInfo");
        var setting = Read(action, "Setting");
        var nativeAction = new ActionID(ActionType.Spell, id);
        var definition = RsrNativeActionDefinition.Find(id, static action => ActionDefinitions.Instance[action]);
        if (id == 0 || targetId == 0 || targetInfo == null || setting == null || definition == null
            || !data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row))
        {
            Status = "RSR selected action has no supported native definition";
            return null;
        }
        if (immediate) ImmediateSelection = (id, targetId, definition.IsGCD);
        // Only RSR's actually selected healing/party ability is added. This is not a
        // scan for hypothetical future heals or a new rotation predictor.
        // Offensive abilities can share mobility gauges; they need explicit
        // resource effects before belonging to a multi-action forecast.
        if (immediate && (definition.IsGCD || !Planning.NativeRotationalMobility.Contains(id) && !Planning.NativeRotationalPreparation.IsImmediateSetup(id)
            && (Read(setting, "IsFriendly") is not true || !Planning.NativeHealingActions.Supported(id)
                && (!Planning.NativeActionSequence.IsImmediatePartyHeal(id) || row.CastType != 2
                    || Number(Read(targetInfo, "Range")) != 0 || Number(Read(targetInfo, "EffectRange")) <= 0)))) return null;
        var affected = ImmutableArray.CreateBuilder<ulong>();
        if (Read(targetResult, "AffectedTargets") is IEnumerable targets)
            foreach (var target in targets)
                if (target is IGameObject recipient && recipient.IsValid() && recipient.Address != 0)
                {
                    var recipientId = recipient.GameObjectId;
                    if (recipientId != 0 && !affected.Contains(recipientId))
                        affected.Add(recipientId);
                }
        var proc = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.CastTimeProc.None;
        var cast = manager.GetAdjustedCastTime(nativeAction, true, &proc) * 0.001f;
        var withoutProc = manager.GetAdjustedCastTime(nativeAction, false) * 0.001f;
        var clockId = Planning.NativeRotationalPreparation.SharedClockAction(id);
        if (!data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(clockId, out var clockRow))
        { Status = "Selected action shared-clock definition is unavailable"; return null; }
        var recast = definition.IsGCD ? manager.GetAdjustedRecastTime(new(ActionType.Spell, clockId)) * 0.001f : 0;
        var ready = Math.Max(immediate ? 0 : Number(_remain?.GetValue(null)), manager.EffectiveAnimationLock);
        var total = Number(_total?.GetValue(null));
        var ahead = Number(_ahead?.GetValue(null));
        var range = Number(Read(targetInfo, "Range"));
        var effect = Number(Read(targetInfo, "EffectRange"));
        if (!FiniteNonnegative(cast, withoutProc, recast, ready, total, ahead, range, effect) || total == 0 || definition.IsGCD && recast <= 0)
        {
            Status = "RSR action timing or range evidence is incomplete";
            return null;
        }
        var result = new RsrActionEvidence(id, targetId, point, affected.ToImmutable(),
            Read(setting, "IsFriendly") is true, Read(targetInfo, "IsTargetArea") is true,
            row.CastType, range, effect, row.XAxisModifier > 0 ? row.XAxisModifier / 2f : 0,
            ready, recast, ahead, cast, cast > 0 ? definition.CastAnimLock : definition.InstantAnimLock,
            (byte)proc, withoutProc, row.Cast100ms * 0.1f, CapturePositional(action, setting, id), definition.IsGCD ? clockRow.Recast100ms * 0.1f : 0);
        Status = $"RSR selected action {id}; {affected.Count} evidenced recipients";
        return result;
    }

    internal ImmutableHashSet<uint> EnabledActions(Class job, bool originalIds = false)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("RSR action settings require the framework thread.");
        try
        {
            // Capture must have verified this live rotation in the same frame. Do not
            // read IsEnabled: RSR's ForceEnable override can hide a saved user opt-out.
            if (_assembly == null || _rotation?.GetValue(null) is not { } rotation
                || Convert.ToUInt32(Read(rotation, "Job") ?? 0) != (uint)job
                || Read(rotation, "AllBaseActions") is not IEnumerable actions)
                return [];
            var result = ImmutableHashSet.CreateBuilder<uint>();
            var basic = RsrRuntimeAccess.ResolveReferencedAssemblyInLoadContext(_assembly, "RotationSolver.Basic");
            var configuredActions = actions.Cast<object>().Concat(basic == null ? [] : RsrNativeMobilitySelection.DutyActions(basic));
            foreach (var action in configuredActions)
            {
                if (action == null || Read(action, "Config") is not { } config)
                    continue;
                if (!_enabledFields.TryGetValue(config.GetType(), out var field))
                    _enabledFields[config.GetType()] = field = config.GetType().GetField("_isEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.FieldType == typeof(bool) && field.GetValue(config) is true)
                {
                    var id = Convert.ToUInt32(Read(action, originalIds ? "ID" : "AdjustedID") ?? 0);
                    if (id != 0)
                        result.Add(id);
                }
            }
            return result.ToImmutable();
        }
        catch (Exception error)
        {
            log.Verbose($"RSR mobility settings unavailable: {error.GetBaseException().Message}");
            return [];
        }
    }

    private void Resolve(Assembly assembly)
    {
        Reset("RSR action contract is unavailable");
        _assembly = assembly;
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        var basic = context?.Assemblies.SingleOrDefault(a => a.GetName().Name == "RotationSolver.Basic");
        var updater = assembly.GetType("RotationSolver.Updaters.ActionUpdater");
        var dataCenter = (basic ?? assembly).GetType("RotationSolver.Basic.DataCenter");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        _nextGcd = updater?.GetProperty("NextGCDAction", flags);
        _nextAction = updater?.GetProperty("NextAction", flags);
        _rotation = dataCenter?.GetProperty("CurrentRotation", flags);
        _remain = dataCenter?.GetProperty("DefaultGCDRemain", flags);
        _total = dataCenter?.GetProperty("DefaultGCDTotal", flags);
        _ahead = dataCenter?.GetProperty("CalculatedActionAhead", flags);
        _positionals = basic?.GetType("RotationSolver.Basic.Helpers.ConfigurationHelper")?.GetField("ActionPositional", flags);
        _redMage = basic?.GetType("RotationSolver.Basic.Rotations.Basic.RedMageRotation");
        _redMageCombo = _redMage?.GetProperty("IsInMeleeCombo", BindingFlags.Public | BindingFlags.Instance);
        _redMageStacks = _redMage?.GetProperty("ManaStacks", flags);
        _manafication = _redMage?.GetProperty("HasManafication", flags);
    }

    private Positional CapturePositional(object action, object setting, uint adjustedId)
    {
        // Bind positional evidence to this exact selected action. The separate
        // event cache can describe an earlier GCD, including an adjusted action
        // whose backing RSR object did not change.
        var selected = Read(setting, "EnemyPositional");
        if (selected is not Enum) throw new MissingMemberException("RSR selected action positional");
        var named = NamedPositional(selected);
        if (named != Positional.Any) return named;
        if (_positionals?.GetValue(null) is not IDictionary entries)
            throw new MissingMemberException("RSR action positional table");
        var baseId = Convert.ToUInt32(Read(action, "ID") ?? adjustedId);
        object? original = null;
        foreach (DictionaryEntry entry in entries)
        {
            var id = Convert.ToUInt32(entry.Key);
            if (id == adjustedId) return NamedPositional(entry.Value);
            if (id == baseId) original = entry.Value;
        }
        return NamedPositional(original);
    }

    internal static Positional NamedPositional(object? value) => value?.ToString() switch
    {
        "Rear" => Positional.Rear,
        "Flank" => Positional.Flank,
        "Front" => Positional.Front,
        null or "None" => Positional.Any,
        _ => throw new NotSupportedException("Unknown RSR positional value.")
    };

    private object? Read(object value, string property)
    {
        var type = value.GetType();
        if (!_properties.TryGetValue((type, property), out var member))
        {
            // Read the declared action interfaces before proxy implementation getters.
            // RSR's IBaseAction and IAction hide some properties with different types.
            member = type.GetInterface("RotationSolver.Basic.Actions.IBaseAction")?.GetProperty(property)
                ?? type.GetInterface("RotationSolver.Basic.Actions.IAction")?.GetProperty(property)
                ?? type.GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _properties.Add((type, property), member);
        }
        return member?.GetValue(value);
    }

    private static float Number(object? value) => value == null ? float.NaN : Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
    private static bool FiniteNonnegative(params float[] values) => values.All(v => float.IsFinite(v) && v >= 0);
    private void Reset(string status)
    {
        ImmediateSelection = null;
        NoSelectedAction = false;
        _assembly = null;
        _nextAction = _nextGcd = _rotation = _remain = _total = _ahead = null;
        _positionals = null;
        _redMage = null;
        _redMageCombo = _redMageStacks = _manafication = null;
        _selection = new();
        _immediateSelection = new();
        _properties.Clear();
        _enabledFields.Clear();
        _comboMembers.Clear();
        Status = status;
        HealingStatus = status;
    }
    public void Dispose() => Reset("RSR evidence reader disposed");
}
