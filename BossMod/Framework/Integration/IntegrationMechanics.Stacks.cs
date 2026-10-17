using BossMod.Components;

namespace BossMod.Integration;

internal static partial class IntegrationMechanics
{
    internal sealed record StackParticipation(bool Complete, bool ActiveTarget, string[] EligibleAliveActorIds,
        string[] OccupantActorIds, int OccupantCount, bool LocalEligible, bool LocalInside,
        int? MinSize, int? MaxSize, string SizingMeaning, float[] ConstraintOrigin, float ConstraintRotation,
        string Meaning = "same native party/layer/forbidden state; eligibility and occupancy are not route or assignment permission");

    private static StackParticipation? StackFacts(BossModule module, Actor local, GenericStackSpread component,
        GenericStackSpread.Stack stack, IntegrationRoutes.Budget budget, bool carrierSeparation = false)
    {
        var layer = stack.ResolveArenaProjectionLayer(module);
        var forbidden = stack.ForbiddenPlayers;
        var localSlot = module.Raid.FindSlot(local.InstanceID);
        if (carrierSeparation && localSlot >= 0 && IntegrationStackCorrection.Required(component, local, stack)) forbidden.Set(localSlot);
        return Participation(module, local, stack.Target, forbidden, layer, stack.RestrictToArenaProjectionLayer,
            component.IncludeDeadTargets || !stack.Target.IsDead, stack.IsInside, stack.MinSize, stack.MaxSize,
            "native-stack-minimum-and-maximum", stack.Target.Position.Quantized(), default, budget);
    }

    private static StackParticipation? BaitStackFacts(BossModule module, Actor local, GenericBaitAway component,
        GenericBaitAway.Bait bait, IntegrationRoutes.Budget budget)
    {
        var origin = component.BaitOrigin(ref bait);
        // GenericBaitStack.AddAIHints uses this aim, rather than the display/custom rotation.
        var rotation = Angle.FromDirection(bait.Target.Position - origin);
        var layer = bait.ResolveArenaProjectionLayer(module);
        return Participation(module, local, bait.Target, bait.Forbidden, layer, bait.RestrictToArenaProjectionLayer,
            !bait.Source.IsDead && (component.AllowDeadTargets || !bait.Target.IsDead),
            a => bait.Shape.Check(a.Position, origin, rotation), null, null,
            "native-GenericBaitStack-does-not-publish-size-limits", origin, rotation, budget);
    }

    private static StackParticipation? Participation(BossModule module, Actor local, Actor target, BitMask forbidden,
        int? layer, bool? restrict, bool activeTarget, Func<Actor, bool> inside, int? min, int? max,
        string sizing, WPos origin, Angle rotation, IntegrationRoutes.Budget budget)
    {
        var eligible = new List<string>(); var occupants = new List<string>(); var localEligible = false;
        foreach (var (slot, actor) in module.Raid.WithSlot())
        {
            if (!budget.Step()) return null;
            if (forbidden[slot] || !module.ActorMatchesArenaProjectionLayer(actor, layer, restrict)) continue;
            eligible.Add(actor.InstanceID.ToString("X"));
            if (ReferenceEquals(actor, local)) localEligible = true;
            if (inside(actor)) occupants.Add(actor.InstanceID.ToString("X"));
        }
        if (!budget.Step()) return null;
        return new(true, activeTarget, [.. eligible], [.. occupants], occupants.Count, localEligible, inside(local),
            min, max, sizing, P(origin), rotation.Rad);
    }
}
