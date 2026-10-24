using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin.Services;
using BossMod.CombatAssistance.Integrations;

internal static class RsrSupportLeaseTests
{
    private static readonly List<string> Calls = [];

    public static void CallbackBoundary()
    {
        var framework = new Framework();
        framework.Update += _ => Calls.Add("before");
        framework.Update += OriginalUpdate;
        framework.Update += _ => Calls.Add("after");
        var method = typeof(RsrSupportLeaseTests).GetMethod(nameof(OriginalUpdate), BindingFlags.Static | BindingFlags.NonPublic)!;
        using (var lease = RsrSupportCallbackLease.Acquire(framework, method,
            () => { Calls.Add("enter"); return new Scope(); }, _ => Calls.Add("failure")))
        {
            Calls.Clear();
            framework.Fire();
            Require(string.Join(",", Calls) == "before,enter,original,leave,after", "Preserve callback position and bracket the original exactly once.");
        }
        Calls.Clear();
        framework.Fire();
        Require(string.Join(",", Calls) == "before,original,after", "Restore the original event ordering.");
        using (var lease = RsrSupportCallbackLease.Acquire(framework, method,
            () => throw new InvalidOperationException("fixture"), _ => Calls.Add("failure")))
        {
            Calls.Clear();
            framework.Fire();
            Require(string.Join(",", Calls) == "before,failure,original,after", "A failed acquisition cannot skip or replay RSR.");
            framework.Clear();
        }
        Calls.Clear();
        framework.Fire();
        Require(Calls.Count == 0, "Disposal must not resurrect an unloaded plugin.");
    }

    public static void SharedSelectionScopes()
    {
        var framework = new Framework();
        framework.Update += OriginalUpdate;
        var original = typeof(RsrSupportLeaseTests).GetMethod(nameof(OriginalUpdate), BindingFlags.Static | BindingFlags.NonPublic)!;
        var firstRestored = true;
        var secondRestored = true;
        var failures = 0;
        IDisposable? First()
        {
            Calls.Add("first-enter"); firstRestored = false;
            return new SelectionScope(() => { firstRestored = true; Calls.Add("first-exit"); });
        }
        IDisposable? Second()
        {
            Calls.Add("second-enter"); secondRestored = false;
            return new SelectionScope(() => { secondRestored = true; Calls.Add("second-exit"); throw new InvalidOperationException("restore status"); });
        }
        using (var lease = RsrSupportCallbackLease.Acquire(framework, original,
            () => RsrSelectionScopeOwner.EnterAll([
                (First, _ => { failures++; Calls.Add("first-failure"); }),
                (() => throw new InvalidOperationException("one policy unavailable"), _ => { failures++; Calls.Add("local-failure"); }),
                (Second, _ => { failures++; Calls.Add("second-failure"); })]), _ => Calls.Add("owner-failure")))
        {
            Calls.Clear(); framework.Fire();
            Require(string.Join(",", Calls) == "first-enter,local-failure,second-enter,original,second-exit,second-failure,first-exit"
                && failures == 2 && firstRestored && secondRestored,
                "Independent policies share one original callback; local setup/cleanup failure never skips another restoration or replays RSR.");
        }
        using (var lease = RsrSupportCallbackLease.Acquire(framework, original,
            () => RsrSelectionScopeOwner.EnterAll([
                (First, _ => { }),
                (() => throw new InvalidOperationException("setup"), _ => throw new InvalidOperationException("reporter"))]),
            _ => Calls.Add("owner-failure")))
        {
            Calls.Clear(); framework.Fire();
            Require(string.Join(",", Calls) == "first-enter,first-exit,owner-failure,original" && firstRestored,
                "A failed reporter unwinds prior policy writes before falling back to the original callback.");
        }
        using (var lease = RsrSupportCallbackLease.Acquire(framework, original,
            () => RsrSelectionScopeOwner.EnterAll([
                (First, _ => { }),
                (Second, _ => throw new InvalidOperationException("restore reporter"))]), _ => Calls.Add("owner-failure")))
        {
            Calls.Clear(); framework.Fire();
            Require(string.Join(",", Calls) == "first-enter,second-enter,original,second-exit,first-exit,owner-failure"
                && firstRestored && secondRestored,
                "A cleanup reporter failure cannot strand an earlier native override or repeat action delivery.");
        }
        Require(RsrSelectionScopeOwner.EnterAll([]) == null, "No policy scope makes the original callback transparent.");
    }

    private sealed class SelectionScope(System.Action leave) : IDisposable
    {
        public void Dispose() => leave();
    }

    private static void OriginalUpdate(IFramework _) => Calls.Add("original");

    private sealed class Framework
    {
        public event IFramework.OnUpdateDelegate? Update;
        public void Fire() => this.Update?.Invoke(null!);
        public void Clear() => this.Update = null;
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Calls.Add("leave");
    }

    public static void LeaseAndExecution()
    {
        var rotation = new Rotation();
        var original = rotation.Support;
        var uses = 0;
        var lease = RsrSupportActionLease.Acquire(rotation, nameof(Rotation.Support), (action, method, args) =>
        {
            if (method.Name == "Use") uses++;
            return RsrSupportActionProxy.Forward(action, method, args);
        });
        Require(lease.Owned && !ReferenceEquals(original, rotation.Support), "Lease replaces only the action instance getter.");
        Require(rotation.Support.CanUse(out var selected), "RSR action remains selectable.");
        Require(ReferenceEquals(selected, rotation.Support), "RSR's selected action retains the execution guard.");
        Require(selected.Use() && uses == 1, "Execution traverses the guard once.");
        lease.Suspend();
        Require(ReferenceEquals(rotation.Support, original), "A scope exit restores the getter without rebuilding the proxy.");
        Require(selected.Use() && uses == 1, "A suspended proxy cannot intercept an out-of-scope execution.");
        lease.Resume();
        Require(ReferenceEquals(rotation.Support, selected), "Resuming reuses the same action proxy.");
        Require(selected.Use() && uses == 2, "A resumed execution reaches the guard.");
        lease.Dispose();
        Require(ReferenceEquals(rotation.Support, original), "Disposal restores the original action getter.");
        Require(selected.Use() && uses == 2, "A previously selected proxy becomes transparent on release.");
        lease.Dispose();

        var second = RsrSupportActionLease.Acquire(rotation, nameof(Rotation.Support), RsrSupportActionProxy.Forward);
        var replacement = new Action();
        typeof(Rotation).GetField("_SupportCreator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(rotation, new Lazy<IAction>(() => replacement));
        second.Dispose();
        Require(ReferenceEquals(rotation.Support, replacement), "Do not overwrite a replacement made by another owner.");
    }

    public static int InstalledContract(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            var candidate = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
            var basic = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "RotationSolver.Basic.dll"));
            var actionUpdaterType = assembly.GetType("RotationSolver.Updaters.ActionUpdater", true)!;
            const BindingFlags selectionFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var healingUpdater = assembly.GetType("RotationSolver.Updaters.StateUpdater", true)!;
            Require(healingUpdater.GetMethod("StatusFromHealing", selectionFlags)?.ReturnType.IsEnum == true
                && healingUpdater.GetMethods(selectionFlags).Count(m => m.Name == "ShouldHealSingle" && m.GetParameters().Length == 4) == 1,
                "Installed RSR exposes original range-independent configured healing demand");
            var healingConfig = basic.GetType("RotationSolver.Basic.Service", true)!.GetProperty("Config", selectionFlags)!.PropertyType;
            foreach (var name in new[] { "HealthSingleAbility", "HealthSingleAbilityHot", "HealthSingleSpell", "HealthSingleSpellHot",
                "HealthAreaAbility", "HealthAreaAbilityHot", "HealthAreaSpell", "HealthAreaSpellHot" })
                Require(healingConfig.GetProperty(name)?.PropertyType == typeof(float), "Installed healing threshold " + name);
            Require(basic.GetType("RotationSolver.Basic.Actions.ActionBasicInfo", true)!.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(m => m.Name == "BasicCheck" && m.GetParameters().Length == 6),
                "Installed range-independent healing availability contract");
            Require(actionUpdaterType.GetProperty("NextAction", selectionFlags)?.PropertyType.FullName == "RotationSolver.Basic.Actions.IAction"
                && actionUpdaterType.GetProperty("NextGCDAction", selectionFlags)?.PropertyType.FullName == "RotationSolver.Basic.Actions.IBaseAction",
                "Installed immediate party-action and GCD evidence contracts");
            var comboContract = basic.GetType("RotationSolver.Basic.Rotations.Basic.RedMageRotation", true)!;
            var blackMage = basic.GetType("RotationSolver.Basic.Rotations.Basic.BlackMageRotation", true)!;
            var baseAction = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            Require(blackMage.GetProperty("RetracePvE") is { } retraceProperty && baseAction.IsAssignableFrom(retraceProperty.PropertyType),
                "Installed RSR Retrace selector supports scoped native field ownership");
            Require(comboContract.GetProperty("IsInMeleeCombo")?.PropertyType == typeof(bool)
                && comboContract.GetProperty("ManaStacks", selectionFlags)?.PropertyType == typeof(byte)
                && comboContract.GetProperty("HasManafication", selectionFlags)?.PropertyType == typeof(bool),
                "Installed RSR observed Red Mage combo contract");
            foreach (var name in new[] { "HasEnoughManaFor1Combo", "HasEnoughManaFor23Combo", "CanMagickedSwordplay", "CanInstantCast", "HasAccelerate", "HasDualcast", "HasEmbolden" })
                Require(comboContract.GetProperty(name, selectionFlags)?.PropertyType == typeof(bool), "Installed read-only RDM policy flag " + name);
            foreach (var name in new[] { "EnoughManaComboPooling", "EnoughManaComboNoPooling" })
                Require(comboContract.GetProperty(name)?.PropertyType == typeof(bool), "Installed RDM pooling policy " + name);
            var rebornRdm = assembly.GetType("RotationSolver.RebornRotations.Magical.RDM_Reborn", true)!;
            var beirutaRdm = assembly.GetType("RotationSolver.ExtraRotations.Magical.BeirutaRDM", true)!;
            var rdmActionIds = basic.GetType("RotationSolver.Basic.Data.ActionID", true)!;
            foreach (var (name, id) in new (string, uint)[] { ("EnchantedRipostePvE_45960", 45960), ("EnchantedZwerchhauPvE_45961", 45961), ("EnchantedRedoublementPvE_45962", 45962) })
                Require(Convert.ToUInt32(rdmActionIds.GetField(name)?.GetRawConstantValue()) == id, "Installed RSR adjusted sword identity " + name);
            Require(rebornRdm.GetProperty("Pooling")?.PropertyType == typeof(bool)
                && beirutaRdm.GetProperty("HoldMeleeComboIfOutOfRange")?.PropertyType == typeof(bool)
                && beirutaRdm.GetField("_meleeHoldUntilMs", BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType == typeof(long)
                && beirutaRdm.GetField("DefaultAoeThreshold", BindingFlags.NonPublic | BindingFlags.Static)?.FieldType == typeof(int),
                "Installed Reborn policy and explicit Beiruta hold contracts");
            foreach (var (job, names) in new (string, string[])[]
            {
                ("BlackMage", ["LeyLinesPvE", "SurecastPvE"]),
                ("RedMage", ["EnchantedRipostePvE", "EnchantedZwerchhauPvE", "EnchantedRedoublementPvE", "EnchantedMoulinetPvE", "EnchantedMoulinetDeuxPvE", "EnchantedMoulinetTroisPvE"]),
                ("Dragoon", ["TrueNorthPvE", "LegSweepPvE", "SprintPvE", "ArmsLengthPvE"]),
                ("Paladin", ["InterjectPvE", "LowBlowPvE", "ShieldBashPvE", "IronWillPvE", "ArmsLengthPvE"]),
                ("Warrior", ["DefiancePvE"]),
                ("DarkKnight", ["GritPvE"]),
                ("Gunbreaker", ["RoyalGuardPvE"]),
                ("Machinist", ["HeadGrazePvE", "ArmsLengthPvE"]),
                ("Sage", ["KardiaPvE", "SurecastPvE"]),
                ("Dancer", ["ClosedPositionPvE", "EndingPvE"]),
                ("Scholar", ["AetherpactPvE", "DissolveUnionPvE"]),
                ("Astrologian", ["TheBalancePvE", "TheSpearPvE", "TheBolePvE", "TheArrowPvE", "TheSpirePvE", "TheEwerPvE"])
            })
            {
                var type = basic.GetType($"RotationSolver.Basic.Rotations.Basic.{job}Rotation", true)!;
                foreach (var name in names)
                {
                    var property = type.GetProperty(name)!;
                    FieldInfo? field = null;
                    for (var owner = type; owner != null && field == null; owner = owner.BaseType)
                        field = owner.GetField("_" + name + "Creator", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    Require(field != null, "Installed generated action ownership field: " + name);
                    Require(property.PropertyType.IsInterface && field!.FieldType == typeof(Lazy<>).MakeGenericType(property.PropertyType),
                        "Installed generated action lease: " + name);
                    var proxy = DispatchProxy.Create(property.PropertyType, typeof(RsrSupportActionProxy));
                    Require(property.PropertyType.IsInstanceOfType(proxy), "Installed interface proxy: " + name);
                }
            }

            var actionType = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            var actionIds = basic.GetType("RotationSolver.Basic.Data.ActionID", true)!;
            foreach (var (job, id) in new (string, uint)[] { ("Paladin", 16461), ("Warrior", 7386),
                ("DarkKnight", 36926), ("Gunbreaker", 36934), ("Monk", 25762), ("Dragoon", 36951),
                ("Ninja", 2262), ("Samurai", 7492), ("Bard", 112), ("RedMage", 7506),
                ("Sage", 24295), ("WhiteMage", 37008), ("BlackMage", 155), ("Pictomancer", 34684) })
                Require(RsrNativeMobilitySelection.Property(basic.GetType($"RotationSolver.Basic.Rotations.Basic.{job}Rotation", true)!,
                    actionIds, actionType, id) != null, "Installed native movement selection: " + job);
            var statusType = basic.GetType("RotationSolver.Basic.Data.StatusID", true)!;
            Require(Enum.IsDefined(statusType, "Scalebound") && Enum.IsDefined(statusType, "ShackledHealing"),
                "Installed Horn healing restrictions");
            Require(basic.GetType("RotationSolver.Basic.DataCenter", true)!.GetProperty("TargetsByRange")!.PropertyType ==
                typeof(Dictionary<float, List<Dalamud.Game.ClientState.Objects.Types.IBattleChara>>), "Installed external heal range cache");
            var rangeHelper = basic.GetType("RotationSolver.Basic.Helpers.TargetHelper", true)!.GetMethod("GetTargetsByRange")!;
            Require(rangeHelper.GetParameters().Select(p => p.ParameterType).SequenceEqual([typeof(float), typeof(bool?)]),
                "Installed native external-player range filter");
            Require(actionType.GetProperty("TargetInfo")!.PropertyType.GetProperty("Range")!.PropertyType == typeof(float),
                "Installed native heal cast range");
            var canUse = actionType.GetMethod("CanUse")!;
            Require(canUse.GetParameters().Length == 12 && canUse.GetParameters()[0].IsOut, "Installed CanUse signature");
            var settingType = actionType.GetProperty("Setting")!.PropertyType;
            Require(settingType.GetProperty("CanTarget") != null &&
                settingType.GetProperty("ActionCheck", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "Installed recipient and eligibility delegates");
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Require(settingType.GetField("_type", Instance) != null, "Installed target policy field");
            var concrete = basic.GetType("RotationSolver.Basic.Actions.BaseAction", true)!;
            Require(concrete.GetProperty("Config", Instance)!.PropertyType.GetField("_isEnable", Instance)?.FieldType == typeof(bool),
                "Installed cast-control saved action opt-out");
            new RsrCastControlEligibility().Prepare(basic);
            Require(concrete.GetProperty("Target", Instance)!.PropertyType.GetProperty("Target")!.PropertyType ==
                typeof(Dalamud.Game.ClientState.Objects.Types.IBattleChara), "Installed action target identity");
            var targetInfo = concrete.GetProperty("TargetInfo", Instance)!.PropertyType;
            Require(targetInfo.GetMethod("GetCanTargets", Instance)!.GetParameters().Length == 4,
                "Installed legal candidate selector");
            foreach (var member in new[] { "TargetOverride", "ActionPreview", "ForceEnable" })
                Require(actionType.GetProperty(member, Static) != null, "Installed static scope: " + member);
            var dataCenter = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            var mergedStatus = dataCenter.GetProperty("MergedStatus", Static)?.PropertyType;
            Require(mergedStatus?.IsEnum == true && Enum.IsDefined(mergedStatus, "AntiKnockback"),
                "Installed automatic immunity request contract; explicit commands retain separate priority");
            foreach (var name in new[] { "IpcOverrides", "TargetingTypeOverride" })
                Require(dataCenter.GetProperty(name, Static)?.SetMethod != null, "Installed scoped mode-option restoration: " + name);
            foreach (var name in new[] { "IsTargetOnly", "CurrentInterceptedAction", "CommandStatus" })
                Require(dataCenter.GetProperty(name, Static) != null, "Installed manual stance-selection gate: " + name);
            var duty = dataCenter.GetProperty("CurrentDutyRotation", Static)!.PropertyType;
            var selectors = duty.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.IsVirtual && !method.IsFinal && method.ReturnType == typeof(bool) &&
                    (method.Name.EndsWith("Ability", StringComparison.Ordinal) || method.Name.EndsWith("GCD", StringComparison.Ordinal)))
                .ToArray();
            _ = RsrSupportDutySlot.CreateFactory(duty, selectors);
            var custom = basic.GetType("RotationSolver.Basic.Rotations.CustomRotation", true)!;
            Require(custom.GetMethod("UseMpPotion", Static) != null &&
                custom.GetMethod("GeneralUsingAbility", Instance) != null &&
                custom.GetMethod("SpeedAbility", Instance) != null &&
                dataCenter.GetProperty("Role", Static) != null && dataCenter.GetProperty("AutoStatus", Static) != null,
                "Installed remaining ability selectors before transfers");
            var updater = RsrSupportCallbackLease.ResolveUpdate(assembly);
            Require(updater.CreateDelegate<IFramework.OnUpdateDelegate>() != null, "Installed atomic selection/execution callback");
            Console.WriteLine($"PASS support reflection surface: RSR={assembly.ManifestModule.ModuleVersionId}, Basic={basic.ManifestModule.ModuleVersionId}");
            Console.WriteLine("Surface validation only: does not establish rotation priority, transfer scheduling, or live acceptance.");
            return 0;
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
    }

    public interface IAction
    {
        bool CanUse(out IAction action);
        bool Use();
    }

    private sealed class Action : IAction
    {
        public bool CanUse(out IAction action) { action = this; return true; }
        public bool Use() => true;
    }

    private sealed class Rotation
    {
        private readonly Lazy<IAction> _SupportCreator = new(() => new Action());
        public IAction Support => this._SupportCreator.Value;
    }

    private static void Require(bool condition, string reason)
    {
        Checks.Check(condition, reason);
    }
}
