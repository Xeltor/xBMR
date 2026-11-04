namespace BossMod.Stormblood.Alliance.A14Argath;

class Unrelenting(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Unrelenting2, new AOEShapeCone(60f, 10f.Degrees()))
{
    private const float PreferredClearance = 1f;
    private DateTime _visualUntil;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == (uint)AID.Unrelenting1 && caster == Module.PrimaryActor && Casters.Count == 0)
            _visualUntil = Module.CastFinishAt(spell);
        else if (spell.Action.ID == WatchedAction)
            _visualUntil = default;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (spell.Action.ID == (uint)AID.Unrelenting1 && caster == Module.PrimaryActor)
            _visualUntil = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(cone => cone.ActorID == caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _visualUntil = default;
            Casters.Clear();
        }
        else
        {
            Casters.RemoveAll(cone => cone.ActorID == actor.InstanceID);
        }
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_visualUntil > WorldState.CurrentTime || Casters.Count != 0)
            hints.Add("Unrelenting: find a gap between the cones or move behind Argath.");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var cone in ActiveAOEs(slot, actor))
        {
            if (cone.Check(actor.Position))
            {
                hints.Add(GnawingDread.IsMisdirected(actor, WorldState.CurrentTime)
                    ? "Move when the arrow points into a clear gap!"
                    : "Step between the cones!");
                break;
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!GnawingDread.IsMisdirected(actor, WorldState.CurrentTime) || !Casters.Any(cone => cone.Check(actor.Position)))
            return;

        // An oblique approach with the spinning arrow benefits from extra clearance.
        // Keep narrow gaps usable if concurrent hazards or time limit the route.
        var cones = Casters.Select(cone => cone.ShapeDistance ?? cone.Shape.Distance(cone.Origin, cone.Rotation)).ToArray();
        hints.GoalZones.Add(position =>
        {
            foreach (var cone in cones)
                if (cone.Distance(position) < PreferredClearance)
                    return 0f;
            return 6f;
        });
    }
}
