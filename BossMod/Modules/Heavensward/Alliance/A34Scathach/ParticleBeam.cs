namespace BossMod.Heavensward.Alliance.A34Scathach;

class ParticleBeam(BossModule module) : Components.GenericTowers(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Towers.Exists(t => t.IsInside(actor)))
            hints.Add("Soak the tower!", false);
        else if (Towers.Exists(t => t.NumInside(Module) < t.MinSoakers))
            hints.Add("Soak an empty tower!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Several players may safely soak, but an already covered tower must not attract every AI
        // while another tower remains empty. Keep an existing soaker in place; send others to gaps.
        var occupied = Towers.FindIndex(t => t.IsInside(actor));
        var destinations = occupied >= 0 ? new List<Tower> { Towers[occupied] } : Towers.FindAll(t => t.NumInside(Module) < t.MinSoakers);
        if (destinations.Count > 0)
        {
            var zones = destinations.Select(t => t.InvertedShapeDistance ?? t.Shape.InvertedDistance(t.Position, t.Rotation)).ToArray();
            hints.AddForbiddenZone(new SDIntersection(zones), destinations.Min(t => t.Activation));
        }
        if (Towers.Count > 0)
        {
            BitMask soakers = default;
            foreach (var (partySlot, member) in Raid.WithSlot())
                if (Towers.Exists(t => t.IsInside(member)))
                    soakers.Set(partySlot);
            hints.AddPredictedDamage(soakers, Towers.Min(t => t.Activation), DamageType);
        }
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.MeteorTower && !Towers.Exists(t => t.ActorID == actor.InstanceID))
            // All observed sets resolve 9.8-10.0s after the event objects appear.
            Towers.Add(new(actor.Position, 5f, maxSoakers: int.MaxValue, activation: WorldState.FutureTime(9.8d), actorID: actor.InstanceID));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ParticleBeam1 or (uint)AID.ParticleBeam2)
        {
            ++NumCasts;
            var position = new WPos(spell.TargetPos);
            Towers.RemoveAll(t => t.Position.AlmostEqual(position, 1f));
        }
    }

    public override void OnActorDestroyed(Actor actor) => Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
}
