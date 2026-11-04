namespace BossMod.Stormblood.Alliance.A12Hashmal;

class CommandTowerImpacts(BossModule module) : Components.CastCounter(module, (uint)AID.Impact)
{
    private const uint TowerVisualLayout = 7075821;
    private readonly DateTime[] _impacts = new DateTime[2];
    private Actor? _visual;
    private int _next;
    private uint _lastSequence;

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_impacts.Any(t => t != default))
            hints.Add("Command Tower Impact incoming. Prepare for raidwide damage.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_impacts.All(t => t == default))
            return;
        var affected = Raid.WithSlot().Mask();
        if (affected == default)
            return;
        foreach (var time in _impacts)
            if (time != default)
                hints.AddPredictedDamage(affected, time);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID != (uint)OID.CommandTowerVisual || actor.LayoutID != TowerVisualLayout)
            return;

        switch (state)
        {
            case 0x00040008 when _visual == null:
                Predict(actor, 14.9d);
                break;
            case 0x00100020 when _visual == null:
                Predict(actor, 10.95d);
                break;
            case 0x00400080 when _next == 0:
                if (_visual == null)
                    Predict(actor, 0d);
                _impacts[0] = WorldState.CurrentTime;
                break;
            case 0x01000200 when _next < 2:
                _visual = actor;
                _next = 1;
                _impacts[0] = default;
                _impacts[1] = WorldState.CurrentTime;
                break;
            case 0x04000800 or 0x00010002:
                Clear();
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction || _visual == null || _next >= _impacts.Length
            || spell.GlobalSequence != 0 && spell.GlobalSequence == _lastSequence)
            return;

        base.OnEventCast(caster, spell);
        _lastSequence = spell.GlobalSequence;
        _impacts[_next++] = default;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == _visual || actor.OID is (uint)OID.Boss or (uint)OID.CommandTower2)
            Clear();
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorTargetable(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
            Clear();
    }

    public override void Update()
    {
        for (var i = _next; i < _impacts.Length; ++i)
            if (_impacts[i] != default && WorldState.CurrentTime > _impacts[i].AddSeconds(0.75d))
                _impacts[i] = default;
    }

    private void Predict(Actor visual, double untilFirst)
    {
        _visual = visual;
        _impacts[0] = WorldState.FutureTime(untilFirst);
        _impacts[1] = WorldState.FutureTime(untilFirst + 5d);
    }

    private void Clear()
    {
        _visual = null;
        _next = 0;
        _lastSequence = 0;
        Array.Clear(_impacts);
    }
}
