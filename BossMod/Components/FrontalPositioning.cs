namespace BossMod.Components;

// A standing-position response for a known frontal whose exact cone angle is
// unavailable. This does not draw or claim a measured damage cone or attack timer.
public class FrontalPositioning(BossModule module, float range, uint[]? enemyOID = null) : BossComponent(module)
{
    private readonly uint[] _oids = enemyOID ?? [module.PrimaryActor.OID];
    protected virtual bool AppliesToActor(Actor actor) => !actor.IsDeadOrDestroyed;
    private IEnumerable<(Actor Source, WDir Direction)> Sources(Actor actor)
    {
        if (!AppliesToActor(actor))
            yield break;
        foreach (var source in Module.Enemies(_oids))
            if (!source.IsDeadOrDestroyed && source.IsTargetable && source.InCombat && source.CastInfo == null
                && source.TargetID != actor.InstanceID && Module.ActorsMatchArenaProjectionLayer(actor, source)
                && WorldState.Actors.Find(source.TargetID) is { Type: ActorType.Player, IsDeadOrDestroyed: false } target)
            {
                var delta = target.Position - source.Position;
                if (delta.LengthSq() > 0.01f)
                    yield return (source, delta.Normalized());
            }
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Sources(actor).Any(s => actor.Position.InCircle(s.Source.Position, range + s.Source.HitboxRadius)
            && (actor.Position - s.Source.Position).Dot(s.Direction) >= 0))
            hints.Add("Keep behind the enemy or outside its frontal reach!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var (source, direction) in Sources(actor))
            hints.AddForbiddenZone(new SDIntersection([new SDCircle(source.Position, range + source.HitboxRadius),
                new SDHalfPlane(source.Position, -direction)]), arenaProjectionLayer: Module.ResolveArenaProjectionLayer(source));
    }
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var (source, direction) in Sources(pc))
        {
            if (!pc.Position.InCircle(source.Position, range + source.HitboxRadius) || (pc.Position - source.Position).Dot(direction) < 0)
                continue;
            var destination = source.Position - (source.HitboxRadius + 1) * direction;
            if (!Arena.InBounds(destination) || Module.Components.OfType<GenericAOEs>().Any(c => c.ActiveAOEs(pcSlot, pc).ToArray().Any(a => a.Risky && a.Check(destination))))
                continue;
            using (Arena.WorldProjectionLayer(Module.ResolveArenaProjectionLayer(source), true))
            {
                Arena.AddCircleUnfilled(destination, 1, Colors.Safe);
                Arena.AddLine(pc.Position, destination, Colors.Safe);
            }
        }
    }
}
