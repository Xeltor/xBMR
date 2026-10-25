using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Targeting;
using System.Collections.Immutable;

public static partial class Checks
{
    private static void OffensiveTargetSelection()
    {
        FateArrivalAcquisition();
        var activeFate = FateTargetPolicy.Matches(123, 123, true, 99);
        Check(activeFate && !FateTargetPolicy.Matches(0, 0, true, 0)
            && !FateTargetPolicy.Matches(456, 123, true, 0)
            && !FateTargetPolicy.Matches(123, 123, false, 0)
            && !FateTargetPolicy.Matches(123, 123, true, 100),
            "FATE acquisition requires the matching active, synced, incomplete FATE.");
        var fatePermission = new RsrTargetDecision(RsrTargetPermission.Allowed, 1);
        Check(fatePermission.EligiblePriority(AIHints.Enemy.PriorityUndesirable, activeFate) == 0
            && fatePermission.EligiblePriority(0, activeFate) == 0
            && fatePermission.EligiblePriority(3, activeFate) == 3,
            "Enabled FATE priority permits passive FATE mobs without raising them above authored encounter priorities.");
        foreach (var restricted in new[] { AIHints.Enemy.PriorityForbidden, AIHints.Enemy.PriorityInvincible, AIHints.Enemy.PriorityPointless })
            Check(fatePermission.EligiblePriority(restricted, activeFate) == restricted,
                "FATE priority preserves forbidden, invincible and dying enemy restrictions.");
        Check(new RsrTargetDecision(RsrTargetPermission.Allowed, 0).EligiblePriority(-3, activeFate) == -3
            && fatePermission.EligiblePriority(-3, false) == -3,
            "An opt-out or unrelated FATE cannot authorize a passive pull.");
        var player = new ExecutionActor(1, default, 0, 0.5f, default);
        OffensiveTargetActor Enemy(ulong id, float x, float z, int priority = 0, bool engaged = true,
            bool dots = false, bool collateral = false)
            => new(new(id, new(x, z), 0, 0.5f, default), priority, engaged, dots, collateral);
        var first = Enemy(2, 5, 0);
        var middle = Enemy(3, 8, 0);
        var last = Enemy(4, 11, 0);
        var fateFrame = new OffensiveTargetFrame(player, 2, 0, false, true, false, 0,
            [first, middle with { Priority = 1 }]);
        Check(fateFrame.PriorityAcquisition(3) == 3
            && (fateFrame with { RetainSelection = true }).PriorityAcquisition(3) == 0
            && (fateFrame with { Acquire = false }).PriorityAcquisition(3) == 0
            && (fateFrame with { Forced = 2 }).PriorityAcquisition(3) == 0
            && (fateFrame with { Current = 999 }).PriorityAcquisition(3) == 0
            && (fateFrame with { Current = 3 }).PriorityAcquisition(3) == 0,
            "A higher priority FATE can replace an ordinary target, respecting manual/authored ownership and saved opt-outs without churn.");
        var circle = new OffensiveActionGeometry(100, 2, 25, 3, 0, false, false, false);
        var single = circle with { EffectRange = 0, CastType = 1 };
        var frame = new OffensiveTargetFrame(player, 2, 0, false, true, true, 0,
            [first, middle, last], 100, [2, 3], 2);
        Check(frame.Allows(circle, 2) && frame.Allows(circle, 3) && !frame.Allows(circle, 4),
            "A strictly better AoE anchor preserves the current target and each current recipient; a different two-hit set cannot replace them.");
        Check(!frame.Allows(single, 3) && !frame.Allows(circle with { Ground = true }, 3),
            "A target preference cannot silently retarget a single-target opportunity or invent a different ground point.");
        Check(!(frame with { RetainSelection = true }).Allows(circle, 3)
            && !(frame with { ImproveAoe = false }).Allows(circle, 3),
            "Manual retention and the saved AoE opt-out both retain the existing target.");
        Check(!(frame with { Forced = 2 }).Allows(circle, 3)
            && (frame with { Forced = 4 }).Allows(single, 4),
            "An explicit native forced target wins before combat preference; an old automatic recipient set cannot veto a new authored target.");
        var tie = frame with { Actors = [first, middle], RequiredRecipients = [2, 3] };
        Check(!tie.Allows(circle, 3), "Equivalent AoE coverage retains its current anchor without timing noise or a switch cooldown.");
        Check(!(frame with { Actors = [first, middle with { Priority = -4 }, last] }).Allows(circle, 3),
            "Additional coverage cannot purchase a forbidden primary target.");
        Check(!(frame with { Actors = [first, middle with { Engaged = false }, last] }).Allows(circle, 3),
            "Combat against one enemy does not authorize acquiring an unrelated idle enemy.");
        var collateral = frame with { Actors = [first, middle, last, Enemy(9, 10, 0, -3, false, collateral: true)] };
        Check(!collateral.Allows(circle, 3) && collateral.Allows(circle, 2),
            "A better-looking area center cannot hit an independent forbidden recipient.");
        var dot = frame with { Actors = [first, middle with { ForbidDots = true }, last] };
        Check(!dot.Allows(circle with { ProvidesTargetStatus = true }, 3) && dot.Allows(circle, 3),
            "Per-enemy DoT restrictions remain independent of ordinary damage eligibility.");
        var priority = frame with { Actors = [first with { Priority = 2 }, middle, last] };
        Check(!priority.Allows(circle, 3), "Native encounter priority cannot be outweighed by more AoE hits.");
        var acquire = frame with { Current = 0, CurrentAction = 0, RequiredRecipients = [], SelectedTarget = 0 };
        Check(acquire.Acquisition == 2 && acquire.Allows(single, 2) && !acquire.Allows(single, 3)
            && !(acquire with { Acquire = false }).Allows(single, 2),
            "Missing-target acquisition has one native candidate and obeys the saved reacquisition opt-out.");
        Check((acquire with { SelectedTarget = 3 }).Acquisition == 3
            && (acquire with { RecoveryTarget = 4 }).Acquisition == 4,
            "An evidenced selected target and confirmed tank recovery precede acquisition distance.");
        Check(!(frame with { RecoveryTarget = 4, ImproveAoe = false }).Allows(circle, 4)
            && (frame with { RecoveryTarget = 4, ImproveAoe = false }).Allows(circle, 3),
            "Tank recovery can improve the area anchor while retaining the old target and all current recipients.");
        var unsafeArea = acquire with { Actors = [Enemy(2, 2, 0), Enemy(9, 3, 0, -3, false, collateral: true)] };
        Check(!unsafeArea.AllowsArea(circle with { Range = 0 })
            && !unsafeArea.AllowsArea(circle with { Ground = true }),
            "Self and not-yet-selected ground effects cannot bypass independent collateral constraints.");
        // Enumerate center/recipient layouts independently of the policy's geometry
        // evaluator. Compare strict hit-set inclusion, not a summed loss/gain score.
        foreach (var secondX in new[] { 5f, 7f, 9f })
        foreach (var thirdX in new[] { 6f, 8f, 10f, 12f })
        foreach (var keepSecond in new[] { false, true })
        {
            var actors = ImmutableArray.Create(first, Enemy(3, secondX, 0), Enemy(4, thirdX, 0));
            var required = keepSecond ? ImmutableArray.Create(2ul, 3ul) : ImmutableArray.Create(2ul);
            var input = frame with { Actors = actors, RequiredRecipients = required };
            HashSet<ulong> Hits(float anchor) => actors.Where(a => Math.Abs(a.Actor.Position.X - anchor) <= 3.5f)
                .Select(a => a.Actor.Id).ToHashSet();
            var baseline = Hits(first.Actor.Position.X);
            foreach (var target in actors)
            {
                var hits = Hits(target.Actor.Position.X);
                var expected = target.Actor.Id == 2 ? required.All(baseline.Contains)
                    : required.All(hits.Contains) && hits.Contains(2) && baseline.IsSupersetOf(required)
                        && hits.Count > baseline.Count;
                Check(input.Allows(circle, target.Actor.Id) == expected,
                    "Native target alternatives match independent required-recipient set comparisons.");
            }
        }
        var far = acquire with { Actors = [Enemy(2, 50, 0)] };
        Check(far.Acquisition == 0 && !far.Allows(single with { Range = 100 }, 2)
            && (far with { Current = 2 }).Allows(single with { Range = 100 }, 2),
            "Native acquisition is strictly below fifty yalms while an existing target can be retained.");
        Check(!(acquire with { Actors = [first with { Actor = first.Actor with { Height = float.NaN } }] }).Allows(single, 2),
            "Unknown target geometry cannot become a new native acquisition proof.");
        MissingTargetRecovery(acquire, first);
        RsrAutomaticTargetScopes();
        NativeTargetIntentOwnership();
    }

    private static void MissingTargetRecovery(OffensiveTargetFrame missing, OffensiveTargetActor first)
    {
        // Reproduce Henched's bootstrap: with no hard target RSR has no selected
        // action or recipients. Native acquisition must work before that callback.
        missing = missing with { CurrentAction = 0, RequiredRecipients = default, SelectedTarget = 0 };
        Check(missing.MissingTargetAcquisition(false) == 2,
            "Henched missing-target recovery does not wait for an action that requires a hard target.");
        foreach (var oldTarget in new[] { 0ul, 2ul, 999ul })
        {
            var lost = missing with { Current = oldTarget, Actors = missing.Actors.Where(a => a.Actor.Id != oldTarget).ToImmutableArray() };
            var expected = oldTarget == 2 ? 3ul : 2ul;
            Check(lost.MissingTargetAcquisition(false) == expected,
                "Cleared, removed and untargetable old targets can hand off to the remaining engaged pack.");
            Check(lost.MissingTargetAcquisition(true) == 0,
                "A usable current hard target, including a manually chosen ally, is never replaced by acquisition.");
        }
        var dead = missing with { Current = 2, Actors = [first with { Actor = first.Actor with { Dead = true } }, missing.Actors[1]] };
        Check(dead.MissingTargetAcquisition(false) == 3,
            "A dead hard target cannot prevent acquiring the living pack member.");
        Check((missing with { Acquire = false }).MissingTargetAcquisition(false) == 0
            && (missing with { RetainSelection = true }).MissingTargetAcquisition(false) == 0
            && (missing with { Forced = 3 }).MissingTargetAcquisition(false) == 0,
            "Saved opt-out, manual retention and authored target ownership prevent bootstrap acquisition.");
        Check((missing with { Actors = [first with { Priority = AIHints.Enemy.PriorityForbidden }] }).MissingTargetAcquisition(false) == 0
            && (missing with { Actors = [first with { Engaged = false }] }).MissingTargetAcquisition(false) == 0
            && (missing with { Actors = [] }).MissingTargetAcquisition(false) == 0,
            "Missing-target recovery cannot pull idle enemies, select forbidden targets or fabricate a target.");
        Check((missing with { Actors = [first, missing.Actors[1] with { Priority = 2 }] }).MissingTargetAcquisition(false) == 3
            && (missing with { Actors = [first with { Actor = first.Actor with { Position = new(50, 0) } }] }).MissingTargetAcquisition(false) == 0,
            "Bootstrap acquisition preserves native encounter priority and the actual target-selection distance limit.");
        var acquired = missing.MissingTargetAcquisition(false);
        var resumed = missing with { Current = acquired, CurrentAction = 100, RequiredRecipients = [acquired], SelectedTarget = acquired };
        Check(resumed.MissingTargetAcquisition(true) == 0
            && resumed.AllowsNative(new(100, 1, 25, 0, 0, false, false, false), acquired),
            "After acquisition, original action validation resumes and equivalent frames do not reacquire.");
    }

    private static void InstalledTargetScopeContract(string directory)
    {
        var basic = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.Combine(directory, "RotationSolver.Basic.dll"));
        var type = basic.GetType("RotationSolver.Basic.Data.IpcStateOverrides", true)!;
        string[] names = ["HostileType", "TargetFreely", "AutoOffAfterCombat", "FriendlyPartyNpcHealRaise"];
        var properties = names.Select(name => type.GetProperty(name) ?? throw new MissingMemberException(type.FullName, name)).ToArray();
        var constructor = type.GetConstructor(properties.Select(p => p.PropertyType).ToArray())!;
        var hostile = Enum.ToObject(Nullable.GetUnderlyingType(properties[0].PropertyType)!, 2);
        var original = constructor.Invoke([hostile, true, false, true]);
        var suppressed = RsrAutomaticTargetScope.Suppress(original);
        if (ReferenceEquals(original, suppressed) || properties[1].GetValue(original) is not true
            || properties[1].GetValue(suppressed) is not false
            || properties.Where((_, index) => index != 1).Any(p => !Equals(p.GetValue(original), p.GetValue(suppressed))))
            throw new InvalidOperationException("RSR session target suppression changed unrelated override values");
        var center = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        if (center.GetProperty("TargetFreelyOverride", flags)?.PropertyType != typeof(bool)
            || center.GetProperty("IpcOverrides", flags)?.PropertyType != type)
            throw new InvalidOperationException("RSR independent target writers changed contract");
        if (new[] { "State", "IsManual", "IsHenched", "IsTargetOnly", "IsPvP" }
            .Any(name => center.GetProperty(name, flags)?.PropertyType != typeof(bool)))
            throw new InvalidOperationException("RSR native target ownership mode contract changed");
        if (center.GetProperty("CommandNextAction", flags)?.PropertyType != basic.GetType("RotationSolver.Basic.Actions.IAction", true))
            throw new InvalidOperationException("RSR live command queue contract changed");
        var commandType = center.GetProperty("CommandStatus", flags)?.PropertyType;
        var specialType = center.GetProperty("SpecialType", flags)?.PropertyType;
        if (commandType?.IsEnum != true || specialType?.IsEnum != true
            || !Enum.GetNames(commandType).Contains("Burst") || !Enum.GetNames(specialType).Contains("Burst"))
            throw new InvalidOperationException("RSR automatic/explicit burst command contract changed");
        var setting = basic.GetType("RotationSolver.Basic.Actions.ActionSetting", true)!;
        var actionCheck = setting.GetProperty("ActionCheck", flags | System.Reflection.BindingFlags.Instance);
        var canTarget = setting.GetProperty("CanTarget", flags | System.Reflection.BindingFlags.Instance);
        if (actionCheck?.PropertyType != typeof(Func<bool>) || actionCheck.SetMethod == null
            || canTarget?.PropertyType != typeof(Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool>)
            || canTarget.SetMethod == null)
            throw new InvalidOperationException("RSR original offensive predicate contract changed");
        var validity = basic.GetType("RotationSolver.Basic.Helpers.TargetHelper", true)!.GetMethod("ValidityCheck", flags)!;
        var actionInfo = basic.GetType("RotationSolver.Basic.Actions.ActionBasicInfo", true)!;
        var basicCheck = actionInfo.GetMethod("BasicCheck", flags | System.Reflection.BindingFlags.Instance);
        if (basicCheck?.ReturnType != typeof(bool) || basicCheck.GetParameters() is not { Length: 6 } basicParameters
            || basicParameters.Take(5).Any(p => p.ParameterType != typeof(bool)) || !basicParameters[5].ParameterType.IsEnum)
            throw new InvalidOperationException($"RSR original action readiness contract changed: {basicCheck}");
        _ = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!.GetMethod("IsSoloDeepDungeonSmartAttackable", flags)!
            .CreateDelegate<Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool>>();
        if (center.GetProperty("TargetFreelyEnabled", flags)?.PropertyType != typeof(bool)
            || center.GetProperty("InCombat", flags)?.PropertyType != typeof(bool)
            || center.GetProperty("CurrentTargetToHostileType", flags)?.PropertyType.IsEnum != true)
            throw new InvalidOperationException("RSR fallback engagement/nearest-target contract changed");
        foreach (var name in new[] { "IsTopPriorityHostile", "IsTopPriorityNamedHostile" })
            _ = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!.GetMethod(name, flags)!
                .CreateDelegate<Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool>>();
        var configType = basic.GetType("RotationSolver.Basic.Service", true)!.GetProperty("Config", flags)!.PropertyType;
        var fateOption = configType.GetProperty("TargetFatePriority")!.PropertyType;
        if (fateOption != typeof(bool) && fateOption.GetMethod("op_Implicit", flags, [fateOption])?.ReturnType != typeof(bool))
            throw new InvalidOperationException("RSR FATE option Boolean conversion contract changed");
        _ = validity.CreateDelegate<Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, HashSet<uint>, HashSet<long>, bool>>();
        _ = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!
            .GetMethod("IsAttackable", flags, [typeof(Dalamud.Game.ClientState.Objects.Types.IBattleChara)])!
            .CreateDelegate<Func<Dalamud.Game.ClientState.Objects.Types.IBattleChara, bool>>();
        _ = basic.GetType("RotationSolver.Basic.Helpers.MarkingHelper", true)!.GetMethod("GetStopTargets", flags)!
            .CreateDelegate<Func<long[]>>();
        var targetInfo = basic.GetType("RotationSolver.Basic.Actions.ActionTargetInfo", true)!;
        var all = flags | System.Reflection.BindingFlags.Instance;
        foreach (var name in new[] { "IsRestrictedDotTarget", "CheckResistance", "CheckTimeToKill", "TargetOnScreen", "CanUseTo" })
        {
            var method = targetInfo.GetMethod(name, all, [typeof(Dalamud.Game.ClientState.Objects.Types.IBattleChara)]);
            if (method?.ReturnType != typeof(bool)) throw new MissingMethodException(targetInfo.FullName, name);
        }
        if (targetInfo.GetMethod("CheckStatus", all, [typeof(Dalamud.Game.ClientState.Objects.Types.IBattleChara), typeof(bool), typeof(bool)])?.ReturnType != typeof(bool))
            throw new MissingMethodException(targetInfo.FullName, "CheckStatus");
        _ = new RsrOffensiveEligibility(
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(basic.GetType("RotationSolver.Basic.Actions.BaseAction", true)!),
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(setting), Activator.CreateInstance(targetInfo)!);
        Console.WriteLine("PASS installed RSR target override, original predicate and eligibility helper contracts; static validation, not live ownership.");
    }

    public sealed record TargetOverrides(int? HostileType, bool? TargetFreely, bool? AutoOffAfterCombat, bool? FriendlyPartyNpcHealRaise);
    private static void RsrAutomaticTargetScopes()
    {
        foreach (var configured in new[] { false, true })
        foreach (var session in new[] { false, true })
        foreach (var overrideValue in new bool?[] { null, false, true })
        {
            var config = configured; var temporary = session;
            var original = new TargetOverrides(2, overrideValue, true, false);
            object? overrides = original;
            using (var scope = new RsrAutomaticTargetScope(() => config, v => config = v,
                () => temporary, v => temporary = v, () => overrides, v => overrides = v, RsrAutomaticTargetScope.Suppress))
                Check(!config && !temporary && overrides is TargetOverrides { HostileType: 2, TargetFreely: false,
                    AutoOffAfterCombat: true, FriendlyPartyNpcHealRaise: false },
                    "Synchronous target ownership suspends all three native fallback sources without changing unrelated overrides.");
            Check(config == configured && temporary == session && ReferenceEquals(overrides, original),
                "Scope exit restores the exact original optional override and Boolean choices.");
        }
        var saved = false; var sessionValue = false;
        object? overrideRecord = new TargetOverrides(2, true, null, null);
        using (var scope = new RsrAutomaticTargetScope(() => saved, v => saved = v,
            () => sessionValue, v => sessionValue = v, () => overrideRecord, v => overrideRecord = v, RsrAutomaticTargetScope.Suppress))
        {
            saved = sessionValue = true;
            overrideRecord = new TargetOverrides(3, true, false, true);
        }
        Check(saved && sessionValue && overrideRecord is TargetOverrides { HostileType: 3, TargetFreely: true, FriendlyPartyNpcHealRaise: true },
            "A later foreign setting or override replacement survives local scope cleanup.");
        var configFailure = true;
        try
        {
            _ = new RsrAutomaticTargetScope(() => configFailure, v => configFailure = v,
                () => true, _ => throw new InvalidOperationException("native session write rejected"),
                () => null, _ => { }, RsrAutomaticTargetScope.Suppress);
        }
        catch (InvalidOperationException) { }
        Check(configFailure, "Failed target scope setup still restores a preceding owned configuration write.");
    }
}

public static partial class Checks
{
    private static void NativeTargetIntentOwnership()
    {
        Actor ActorWithId(ulong id)
        {
            var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = id;
            return actor;
        }
        var native = ActorWithId(10);
        var assisted = ActorWithId(20);
        var hints = new AIHints { NativeTargetIntentCollectionRequested = true };
        hints.SetAutomaticTarget(native);
        Check(hints.ForcedTarget == native && hints.AuthoritativeTarget == null && hints.TrySetAssistedTarget(assisted)
            && hints.ForcedTarget == assisted && hints.AssistedTargetApplied,
            "Automatic target provenance permits one native assisted publication without replacing the public field contract.");
        hints.SetNativeTarget(assisted);
        Check(hints.AuthoritativeTarget == assisted && !hints.TrySetAssistedTarget(native) && !hints.AssistedTargetApplied,
            "Assigning the same actor explicitly still establishes authored authority over a later assisted replacement.");
        hints.SetAutomaticTarget(native);
        Check(hints.ForcedTarget == assisted, "Routine automatic selection cannot overwrite an authored target during native intent collection.");
        hints.Clear();
        Check(hints.ForcedTarget == null && hints.AuthoritativeTarget == null && !hints.AssistedTargetApplied
            && hints.NativeTargetIntentCollectionRequested && hints.AutomaticRetargetingAllowed == null,
            "Frame clearing retires all target provenance while preserving the owner's next capture request.");
        hints.ForcedTarget = native;
        Check(hints.AuthoritativeTarget == native && !hints.TrySetAssistedTarget(assisted),
            "Existing encounter writers keep authoritative native semantics without a dependency on assistance.");
        hints.Clear(); hints.AutomaticRetargetingAllowed = false;
        hints.SetAutomaticTarget(native);
        Check(!hints.TrySetAssistedTarget(assisted) && hints.ForcedTarget == native,
            "An explicit native preset's no-retarget decision cannot be bypassed by combat preference.");
        hints.Clear(); hints.NativeTargetIntentCollectionRequested = false;
        hints.ForcedTarget = native; hints.SetAutomaticTarget(assisted);
        Check(hints.ForcedTarget == assisted && !hints.TrySetAssistedTarget(native),
            "Outside assisted intent collection, upstream automatic assignment semantics remain unchanged.");
        hints.SetAutomaticTarget(native, onlyIfUnassigned: true);
        Check(hints.ForcedTarget == assisted, "Following preserves the existing only-if-unassigned targeting contract.");
        Check(typeof(AIHints).GetField(nameof(AIHints.ForcedTarget))?.FieldType == typeof(Actor),
            "Native target provenance does not change the upstream public field into a property or serialized interface.");
    }
}
