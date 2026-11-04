namespace BossMod.RealmReborn.Alliance.A34CloudofDarkness;

class FloodOfDarkness(BossModule module) : BossComponent(module)
{
    private bool _waveSeen = module.Enemies((uint)OID.DarkCloud).Count != 0 || module.Enemies((uint)OID.DarkStorm).Count != 0;
    private DateTime _activation;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.HyperchargedCloud)
        {
            _waveSeen = true;
            _activation = default;
        }
        else if (!_waveSeen && actor.OID is (uint)OID.DarkCloud or (uint)OID.DarkStorm)
        {
            _waveSeen = true;
            // In two recordings Flood followed the first cloud spawn by 68.80–68.93s.
            // Use an early estimate for mitigation and keep the same deadline across the wave.
            _activation = WorldState.FutureTime(68.5);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.FloodOfDarkness)
        {
            _waveSeen = true;
            _activation = default;
        }
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDeadOrDestroyed || _activation != default && WorldState.CurrentTime > _activation.AddSeconds(3))
            _activation = default;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.Enemies((uint)OID.DarkCloud).Any(c => !c.IsDeadOrDestroyed) || Module.Enemies((uint)OID.DarkStorm).Any(c => !c.IsDeadOrDestroyed))
            hints.Add("Destroy incoming clouds before they reach the boss!");
        if (_activation != default && _activation <= WorldState.FutureTime(8))
            hints.Add("Flood of Darkness soon: prepare for raidwide damage!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_activation == default)
            return;
        BitMask affected = default;
        foreach (var (playerSlot, _) in Raid.WithSlot())
            affected.Set(playerSlot);
        if (affected != default)
            hints.AddPredictedDamage(affected, _activation);
    }
}
