namespace BossMod.Stormblood.Alliance.A13Rofocale;

class Embrace(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Embrace2, 3f)
{
    private readonly List<AOEInstance> _visible = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _visible.Clear();
        foreach (var cast in base.ActiveAOEs(slot, actor))
            _visible.Add(cast);

        foreach (var trap in Module.Enemies((uint)OID.Embrace))
        {
            if (!trap.IsDeadOrDestroyed && !_visible.Any(a => a.Origin.AlmostEqual(trap.Position, 0.5f)))
                _visible.Add(new(Shape, trap.Position, actorID: trap.InstanceID));
        }

        return CollectionsMarshal.AsSpan(_visible);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var (partySlot, member) in Raid.WithSlot(excludeAlliance: true, excludeNPCs: true))
            if (member.PendingDispels.Count == 0 && member.FindStatus((uint)SID.Bind) is { } bind && bind.ExpireAt > WorldState.CurrentTime)
                hints.ShouldCleanse.Set(partySlot);
    }
}
