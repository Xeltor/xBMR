using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using BossMod.CombatAssistance.Targeting;
using Dalamud.Game.ClientState.Objects.Types;
using System.Reflection;

public static partial class Checks
{
    private static void OffensivePredicateConsumer()
    {
        Check(NativeOffensiveTargeting.MayConstrainSelection(true, true, false, false)
            && NativeOffensiveTargeting.MayConstrainSelection(false, false, false, false)
            && !NativeOffensiveTargeting.MayConstrainSelection(true, false, false, false)
            && !NativeOffensiveTargeting.MayConstrainSelection(true, true, true, false)
            && !NativeOffensiveTargeting.MayConstrainSelection(true, true, false, true)
            && !NativeOffensiveTargeting.MayConstrainSelection(null, null, false, false),
            "Henched delegates targeting to BMR despite RSR IsManual; explicit Manual, target-only, PvP and unknown modes keep control.");
        Check(!RsrCommandPolicy.HasExplicitCommand(CommandFlags.None, SpecialCommand.None)
            && !RsrCommandPolicy.HasExplicitCommand(CommandFlags.Burst, SpecialCommand.None)
            && RsrCommandPolicy.HasExplicitCommand(CommandFlags.Burst, SpecialCommand.Burst)
            && RsrCommandPolicy.HasExplicitCommand(CommandFlags.Burst | CommandFlags.NoCasting, SpecialCommand.NoCasting)
            && RsrCommandPolicy.HasExplicitCommand(CommandFlags.NoCasting, SpecialCommand.NoCasting)
            && RsrCommandPolicy.HasExplicitCommand((CommandFlags)8, SpecialCommand.None)
            && RsrCommandPolicy.HasExplicitCommand(null, SpecialCommand.None)
            && RsrCommandPolicy.HasExplicitCommand(CommandFlags.Burst, null)
            && RsrCommandPolicy.HasExplicitCommand(CommandFlags.None, SpecialCommand.None, queuedAction: true),
            "Automatic burst cannot masquerade as manual control; explicit burst, casting commands and unknown contracts retain authority.");
        var player = new ExecutionActor(1, default, 0, 0.5f, default);
        OffensiveTargetActor Enemy(ulong id, float x, int priority = 0, bool engaged = true, bool collateral = false)
            => new(new(id, new(x, 0), 0, 0.5f, default), priority, engaged, ForbiddenCollateral: collateral);
        var input = new OffensiveTargetFrame(player, 2, 0, false, true, true, 0,
            [Enemy(2, 5), Enemy(3, 8), Enemy(4, 11)], 100, [2, 3], 2);
        var area = new OffensiveActionGeometry(100, 2, 25, 3, 0, false, false, false);
        var single = area with { CastType = 1, EffectRange = 0 };
        var primary = new TargetPredicateSettings();
        var nativeCalls = 0;
        primary.CanTarget = target => { nativeCalls++; return target.GameObjectId != 4; };
        var originalTarget = primary.CanTarget;
        Func<bool> originalCheck = () => true;
        primary.ActionCheck = originalCheck;
        var manual = false;
        var failures = 0;
        Func<IBattleChara, bool> cached;
        using (var scope = new RsrOffensivePredicateScope(primary, () => input.AllowsArea(area),
            id => input.AllowsNative(area, id), () => manual, _ => failures++))
        {
            cached = primary.CanTarget;
            // Original RSR candidate iteration and status/resource predicate still
            // chooses on its own concrete action. Native intent consumes its result.
            var candidates = new[] { Character(2), Character(3), Character(4) };
            var selected = candidates.Where(target => primary.ActionCheck!() && primary.CanTarget(target)).ToArray().Last();
            Check(selected.GameObjectId == 3 && nativeCalls == 2,
                "Original candidate selection sees native area/recipient constraints and runs each retained original predicate once.");
            var hints = new AIHints { NativeTargetIntentCollectionRequested = true };
            var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = selected.GameObjectId;
            Check(hints.TrySetAssistedTarget(actor) && hints.ForcedTarget?.InstanceID == 3,
                "A constrained selected action target enters the same native target intent consumed by movement.");
            hints.SetNativeTarget(actor);
            Check(!hints.TrySetAssistedTarget(actor), "Authored target identity still wins at the publication consumer.");
            manual = true;
            Check(primary.CanTarget(Character(2)) && !primary.CanTarget(Character(4)),
                "Forced/manual selection bypasses assistance preference but never its original rotation predicate.");
            manual = false;
            Check(!primary.CanTarget(Character(3, valid: false)) && !primary.CanTarget(Character(3, address: 0)),
                "Invalid or zero-address native wrappers cannot reach original target predicates.");
        }
        Check(ReferenceEquals(primary.CanTarget, originalTarget) && ReferenceEquals(primary.ActionCheck, originalCheck)
            && cached(Character(2)) && failures == 0,
            "Scope exit restores exact predicates; cached delegates become transparent without replaying selection.");
        Func<IBattleChara, bool> foreign = _ => false;
        using (var scope = new RsrOffensivePredicateScope(primary, () => true, _ => true, () => false, _ => failures++))
            primary.CanTarget = foreign;
        Check(ReferenceEquals(primary.CanTarget, foreign), "Later foreign target ownership survives scope cleanup.");
        primary.CanTarget = originalTarget;
        using (var scope = new RsrOffensivePredicateScope(primary, () => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(), () => false, _ => failures++))
            Check(!primary.ActionCheck!() && !primary.CanTarget(Character(2)) && failures == 2,
                "Local action/target predicate failures reject only that candidate and are reported.");
        var invalidated = Character(3);
        var proxy = (BattleCharacterProxy)(object)invalidated;
        primary.CanTarget = _ => { proxy.Valid = false; return true; };
        using (var scope = new RsrOffensivePredicateScope(primary, () => true, id => id == 3, () => false, _ => failures++))
            Check(primary.CanTarget(invalidated), "Identity is captured before external selection and wrappers are not reread afterward.");
        Check(input.AllowsNative(single, 3), "Native single-target DoT/status choice is not replaced by the area preference policy.");
        Check(!(input with { RequiredRecipients = default }).AllowsNative(area, 3),
            "An incomplete recipient snapshot cannot prove a new area anchor improves the current action.");
        Check(!(input with { CurrentAction = 0, RequiredRecipients = default }).AllowsNative(area, 3),
            "An unobserved or skipped RSR selection cannot justify a fresh improved area anchor.");
        var forbidden = input with { ImproveAoe = false, Actors = [Enemy(2, 5), Enemy(3, 8), Enemy(9, 8, -4, collateral: true)] };
        Check(!forbidden.AllowsNative(area, 3) && forbidden.AllowsNative(single, 3),
            "Turning off area optimization cannot turn off independent collateral rejection.");
        var negative = input with { Actors = [Enemy(2, 5, -1)] };
        Check(negative.AllowsNative(single, 2) && !negative.AllowsNative(single, 3),
            "Native pointless targets may retain gauge/combo execution but are not acquired as a new opportunity.");
        Check(!(negative with { Actors = [Enemy(2, 5, -2)] }).AllowsNative(single, 2),
            "Invincible targets cannot become a primary action opportunity.");
        negative = input with { Actors = [Enemy(2, 5, -3, false, true)] };
        Check(negative.AllowsNative(single, 2) && !negative.AllowsNative(area, 2),
            "An explicitly selected undesirable target retains native single-target permission while area collateral remains forbidden.");
    }

    [Flags] private enum CommandFlags { None = 0, Burst = 1, NoCasting = 2 }
    private enum SpecialCommand { None, Burst, NoCasting }

    public sealed class TargetPredicateSettings
    {
        public Func<bool>? ActionCheck { get; set; }
        public Func<IBattleChara, bool> CanTarget { get; set; } = _ => true;
    }
    private static IBattleChara Character(ulong id, bool valid = true, nint address = 1)
    {
        var result = DispatchProxy.Create<IBattleChara, BattleCharacterProxy>();
        var proxy = (BattleCharacterProxy)(object)result;
        proxy.Id = id; proxy.Valid = valid; proxy.Address = address;
        return result;
    }
    public class BattleCharacterProxy : DispatchProxy
    {
        public ulong Id;
        public bool Valid;
        public nint Address;
        public IGameObject? Target;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_GameObjectId" => Id,
            "get_Address" => Address,
            "IsValid" => Valid,
            "get_TargetObject" => Target,
            _ => throw new NotSupportedException(method?.Name)
        };
    }
}
