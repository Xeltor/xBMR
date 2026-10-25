using BossMod.CombatAssistance.Support;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace BossMod.CombatAssistance.Integrations;

// This is the remaining external boundary. No xBMR reflection is used.
internal sealed class RsrRuntimeAccess(IDalamudPluginInterface plugin)
{
    private Assembly? _provider;
    private Assembly? _basic;
    private PropertyInfo? _currentRotation;
    private Func<IBattleChara, bool>? _namedPriority;
    private Func<IBattleChara, bool>? _specificPriority;
    private Func<IBattleChara, HashSet<uint>, HashSet<long>, bool>? _targetValidity;
    private Func<long[]>? _stopTargets;
    private PropertyInfo? _blacklistedNames;
    internal ScopedObservation<IExposedPlugin[]>.Scope BeginObservation() => ReflectionObjectSearch.BeginObservation(plugin);
    internal bool TryGetSupportContext([NotNullWhen(true)] out Assembly? assembly, [NotNullWhen(true)] out Assembly? basic, [NotNullWhen(true)] out object? rotation)
    {
        assembly = ReflectionObjectSearch.FindLoadedPlugin(plugin, "RotationSolver.RotationSolverPlugin",
            "RotationSolver", "Rotation Solver Reborn")?.GetType().Assembly;
        if (assembly != _provider || assembly != null && _currentRotation == null)
        {
            _specificPriority = null;
            _namedPriority = null; _targetValidity = null; _stopTargets = null; _blacklistedNames = null;
            _provider = assembly;
            _basic = assembly == null ? null : ResolveReferencedAssemblyInLoadContext(assembly, "RotationSolver.Basic");
            _currentRotation = _basic?.GetType("RotationSolver.Basic.DataCenter")?.GetProperty("CurrentRotation",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        }
        basic = _basic;
        // Metadata belongs to this loaded assembly. The rotation itself remains
        // a live read, including a job/rotation replacement inside the update.
        rotation = assembly == null ? null : _currentRotation?.GetValue(null);
        return assembly != null && basic != null && rotation != null;
    }

    internal static Assembly? ResolveReferencedAssemblyInLoadContext(Assembly assembly, string name)
        => AssemblyLoadContext.GetLoadContext(assembly)?.Assemblies.SingleOrDefault(a => a.GetName().Name == name);
    internal static uint Job(object rotation) => Convert.ToUInt32(rotation.GetType().GetProperty("Job",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(rotation) ?? 0);
    internal RsrTargetRules? TargetRules(bool autorotationActive)
    {
        try
        {
            if (!autorotationActive) return null;
            if (!TryGetSupportContext(out _, out var basic, out var rotation)) return null;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            object? Center(string name) => center.GetProperty(name, flags)?.GetValue(null);
            var authority = new RsrTargetAuthority(Center("State") as bool?, Center("IsHenched") as bool?,
                Center("IsManual") as bool?, Center("IsTargetOnly") as bool?, Center("IsPvP") as bool?);
            if (!authority.DelegatesNativeTargeting) return null;
            var helper = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!;
            _namedPriority ??= helper.GetMethod("IsTopPriorityNamedHostile", flags)!.CreateDelegate<Func<IBattleChara, bool>>();
            _specificPriority ??= helper.GetMethod("IsTopPriorityHostile", flags)!.CreateDelegate<Func<IBattleChara, bool>>();
            _targetValidity ??= basic.GetType("RotationSolver.Basic.Helpers.TargetHelper", true)!
                .GetMethod("ValidityCheck", flags)!.CreateDelegate<Func<IBattleChara, HashSet<uint>, HashSet<long>, bool>>();
            _stopTargets ??= basic.GetType("RotationSolver.Basic.Helpers.MarkingHelper", true)!
                .GetMethod("GetStopTargets", flags)!.CreateDelegate<Func<long[]>>();
            _blacklistedNames ??= basic.GetType("RotationSolver.Basic.DataCenter", true)!.GetProperty("BlacklistedNameIds", flags)!;
            var blacklist = (_blacklistedNames.GetValue(null) as System.Collections.IEnumerable)?.Cast<object>()
                .Select(Convert.ToUInt32).ToHashSet() ?? throw new MissingMemberException("RSR target blacklist");
            var stops = _stopTargets().Where(id => id != 0).ToHashSet();
            var validity = _targetValidity;
            var mode = Center("CurrentTargetToHostileType") as Enum;
            var party = Center("PartyMembers") as System.Collections.IEnumerable;
            var hostiles = (Center("AllHostileTargets") as System.Collections.IEnumerable)?.Cast<object>().OfType<IBattleChara>()
                .Where(a => a.IsValid() && a.Address != 0).Select(a => a.GameObjectId).ToHashSet();
            var player = Service.ObjectTable.LocalPlayer;
            RsrFallbackTargeting? fallback = null;
            if (mode != null && Enum.TryParse<RsrHostilePolicy>(mode.ToString(), out var policy)
                && Enum.IsDefined(policy) && player != null && party != null && hostiles != null)
            {
                var smart = helper.GetMethod("IsSoloDeepDungeonSmartAttackable", flags)!.CreateDelegate<Func<IBattleChara, bool>>();
                fallback = new(policy, player.GameObjectId, party.Cast<object>().Count() == 1,
                    Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
                        || Service.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56],
                    Center("TargetFreelyEnabled") is true && Center("InCombat") is true,
                    smart, () => rotation.GetType().GetProperty("AllBaseActions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(rotation) as System.Collections.IEnumerable ?? Array.Empty<object>(), RsrFallbackTargeting.IsUsable,
                    candidate => hostiles.Contains(candidate.GameObjectId));
            }
            // Henched bypasses only IsAttackable's general engagement tail. Its
            // exclusions still apply; engagement is a separate fallback input.
            return RsrTargetRules.Capture(authority, actor => validity(actor, blacklist, stops), _namedPriority, _specificPriority, fallback);
        }
        catch { return null; }
    }
    internal static SupportAction? Support(uint job) => job switch
    {
        40 => SupportAction.Kardia,
        38 => SupportAction.DancePartner,
        28 => SupportAction.FeyUnion,
        33 => SupportAction.Balance,
        24 or 27 or 35 => SupportAction.Resurrection,
        _ => null
    };
}
