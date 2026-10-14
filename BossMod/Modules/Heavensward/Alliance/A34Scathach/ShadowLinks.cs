namespace BossMod.Heavensward.Alliance.A34Scathach;

class ShadowLinks(BossModule module) : Components.StayMove(module)
{
    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != (uint)OID.ShadowLinksHelper)
            return;
        if (state == 0x00040008u)
            Array.Fill(PlayerStates, new(Requirement.Stay2, WorldState.CurrentTime));
        else if (state == 0x00010002u)
            Array.Clear(PlayerStates);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (PlayerStates[slot].Requirement == Requirement.Stay2)
            hints.Add("Don't move!", actor.Position != actor.PrevPosition);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.ShadowLinksHelper)
            Array.Clear(PlayerStates);
    }

}

class ShadowLimbs(BossModule module) : Components.GenericGaze(module)
{
    private readonly List<Eye> _eyes = [];

    public override ReadOnlySpan<Eye> ActiveEyes(int slot, Actor actor)
    {
        _eyes.Clear();
        foreach (var limb in Module.Enemies((uint)OID.ShadowLimb))
            if (!limb.IsDead && !limb.IsDestroyed && limb.Tether.Target == actor.InstanceID && limb.Tether.ID is (uint)TetherID.Tether1 or (uint)TetherID.Tether2)
                _eyes.Add(new(limb.Position, WorldState.CurrentTime, inverted: true, actorID: limb.InstanceID));
        return CollectionsMarshal.AsSpan(_eyes);
    }
}
