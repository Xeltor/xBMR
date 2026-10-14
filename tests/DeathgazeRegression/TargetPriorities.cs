using BossMod;
using BossMod.Heavensward.Alliance.A31DeathgazeHollow;

static partial class Checks
{
    private static void SpritePriorities()
    {
        using var module = Module();
        var player = Actor(1, 0, module.Center, true);
        Party(module, player);
        var sprite = Create(module, Actor(2, (uint)OID.VoidSprite, module.Center));
        foreach (var priority in new[] { 0, 7, -1, -2, -3, -4 })
        {
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(module.PrimaryActor, 0, false));
            hints.PotentialTargets.Add(new(sprite, priority, false));
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.PotentialTargets[1].Priority == (priority >= 0 ? Math.Max(priority, 2) : priority),
                "sprite priority favors adds while preserving stronger and forbidden/dying priorities: " + priority);
            Check(hints.PotentialTargets[0].Priority == 0 && hints.PotentialTargets[1].DesiredPosition == null
                && hints.ForcedMovement == null, "add priority does not move the tank, force movement or rewrite the boss priority");
        }
        foreach (var unavailable in new[] { "dead", "untargetable" })
        {
            sprite.IsDead = unavailable == "dead";
            sprite.IsTargetable = unavailable != "untargetable";
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(sprite, 0, false));
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.PotentialTargets[0].Priority == 0, "unavailable sprite is not promoted: " + unavailable);
        }
    }
}
