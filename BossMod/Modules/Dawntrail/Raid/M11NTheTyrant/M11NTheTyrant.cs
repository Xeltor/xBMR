namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

sealed class CrownOfArcadia(BossModule module) : Components.RaidwideCast(module, (uint)AID._Weaponskill_CrownOfArcadia);
sealed class DanceOfDomination(BossModule module) : BossComponent(module)
{
    private readonly List<DateTime> _hits = [];
    private uint _sequence;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID._Weaponskill_DanceOfDomination1)
            return;
        _hits.Clear();
        _sequence = 0;
        var first = Module.CastFinishAt(spell);
        foreach (var offset in new double[] { 0d, 0.6d, 1.2d, 1.8d, 2.4d, 3d, 4.1d })
            _hits.Add(first.AddSeconds(offset));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID._Weaponskill_DanceOfDomination1 or (uint)AID._Weaponskill_DanceOfDomination2 or (uint)AID._Weaponskill_DanceOfDomination3
            && _hits.Count != 0 && _sequence != spell.GlobalSequence)
        {
            _sequence = spell.GlobalSequence;
            _hits.RemoveAt(0);
            if (spell.Action.ID == (uint)AID._Weaponskill_DanceOfDomination3)
                _hits.Clear();
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID._Weaponskill_DanceOfDomination1 && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            _hits.Clear();
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_hits.Count != 0)
            hints.Add("Repeated raidwide damage");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        BitMask party = default;
        foreach (var member in Raid.WithSlot())
            party.Set(member.Item1);
        foreach (var activation in _hits)
            hints.AddPredictedDamage(party, activation, AIHints.PredictedDamageType.Raidwide);
    }
}
sealed class FlatlinerRaidwide(BossModule module) : Components.RaidwideCast(module, (uint)AID._Weaponskill_FlatlinerKnockup);
sealed class Charybdistopia(BossModule module) : Components.RaidwideCast(module, (uint)AID._Spell_Charybdistopia);
sealed class OneAndOnly(BossModule module) : Components.RaidwideCast(module, (uint)AID._Weaponskill_OneAndOnly1);
sealed class Cometite(BossModule module) : Components.SequencedAOEs(module, [(uint)AID._Spell_Cometite], new AOEShapeCircle(4f));
sealed class Explosion1(BossModule module) : Components.SequencedAOEs(module, [(uint)AID._Weaponskill_Explosion, (uint)AID._Weaponskill_Explosion1], new AOEShapeRect(60f, 5f));
sealed class WallOfFireExplosion(BossModule module) : Components.SimpleAOEs(module, (uint)AID._Weaponskill_Explosion2, new AOEShapeRect(60f, 3f));
sealed class RawSteelTankBuster(BossModule module) : Components.IconSharedTankbuster(module, (uint)IconID.RawSteelSharedTankbuster, (uint)AID._Weaponskill_RawSteel1, 6f, activationDelay: 6.2d);
sealed class RawSteelSpreads(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID._Weaponskill_Impact, 6f);
sealed class CosmicKiss(BossModule module) : Components.CastTowers(module, (uint)AID._Ability_CosmicKiss, 4f, 1, 1);
sealed class MassiveMeteor(BossModule module) : Components.StackWithCastTargets(module, (uint)AID._Ability_MassiveMeteor, 6f, 4, 8);
sealed class MajesticMeteor(BossModule module) : Components.SequencedAOEs(module, [(uint)AID._Spell_MajesticMeteor1], new AOEShapeCircle(6f));
// 22y is the inherited proximity avoidance policy, not the 60y damage range or a measured lethal threshold.
sealed class MammothMeteor(BossModule module) : Components.ProximityAOEs(module, (uint)AID._Spell_MammothMeteor, 22f);
sealed class FireAndFury(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID._Weaponskill_FireAndFuryCone1, (uint)AID._Weaponskill_FireAndFuryCone2], new AOEShapeCone(60f, 45f.Degrees()));
sealed class GreatWallOfFire(BossModule module) : Components.IconSharedTankbuster(module, (uint)IconID.WallOfFireTankbuster, (uint)AID._Weaponskill_GreatWallOfFire1, new AOEShapeRect(60f, 3f), activationDelay: 5.9d);

// All three moving spreads remain active until each target's third hit, independently of ground explosions.
sealed class VoidStardust(BossModule module) : Components.UniformStackSpread(module, 0f, 4f)
{
    private readonly Dictionary<ulong, int> _hits = [];
    private readonly HashSet<(ulong Caster, uint Sequence)> _events = [];
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != (uint)IconID.VoidStardustSpread || _hits.ContainsKey(actor.InstanceID))
            return;
        _hits[actor.InstanceID] = 0;
        AddSpread(actor, WorldState.FutureTime(4.9d));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID._Spell_VoidStardust)
        {
            _hits.Clear();
            _events.Clear();
            Spreads.Clear();
        }
        if (spell.Action.ID == (uint)AID._Spell_Comet && WorldState.Actors.Find(spell.TargetID) is { } target)
        {
            Spreads.RemoveAll(s => s.Target == target);
            AddSpread(target, Module.CastFinishAt(spell));
            _hits.TryAdd(target.InstanceID, 0);
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID._Spell_Comet) and not ((uint)AID._Spell_Comet1) || !_hits.TryGetValue(spell.MainTargetID, out var n))
            return;
        if (spell.GlobalSequence != 0 && !_events.Add((caster.InstanceID, spell.GlobalSequence)))
            return;
        Spreads.RemoveAll(s => s.Target.InstanceID == spell.MainTargetID);
        if (++n < 3 && WorldState.Actors.Find(spell.MainTargetID) is { IsDestroyed: false } target)
        {
            _hits[spell.MainTargetID] = n;
            AddSpread(target, WorldState.FutureTime(2.1d));
        }
        else
            _hits.Remove(spell.MainTargetID);
    }
    public override void Update()
    {
        Spreads.RemoveAll(s => s.Target.IsDestroyed || WorldState.Actors.Find(s.Target.InstanceID) != s.Target);
        foreach (var id in _hits.Keys.ToArray())
            if (WorldState.Actors.Find(id) == null)
                _hits.Remove(id);
    }
}

sealed class Maelstrom(BossModule module) : Components.GenericAOEs(module)
{
    private AOEInstance[] _aoes = [];
    private static readonly AOEShapeCircle _shape = new(5f);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes = Module.Enemies((uint)OID.Maelstrom).Where(a => !a.IsDestroyed && !a.IsDead).Select(a => new AOEInstance(_shape, a.Position, actorID: a.InstanceID)).ToArray();
        return _aoes;
    }
}

// Both angular waves must have their own deadlines; unioning them at time zero forbids the entire arena.
sealed class PowerfulGust(BossModule module) : Components.SequencedAOEs(module, [(uint)AID._Spell_PowerfulGust], new AOEShapeCone(60f, 22.5f.Degrees()));

sealed class HeartbreakKick(BossModule module) : Components.GenericTowers(module, damageType: AIHints.PredictedDamageType.Shared)
{
    private int _hits;
    private uint _sequence;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID._Weaponskill_HeartbreakKick)
            return;
        _hits = 0;
        _sequence = 0;
        Towers.Clear();
        Towers.Add(new(Arena.Center, 4f, 8, 8, activation: Module.CastFinishAt(spell, 1.1d), actorID: caster.InstanceID));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID._Weaponskill_HeartbreakKick1 && Towers.Count != 0 && (_hits == 0 || _sequence != spell.GlobalSequence))
        {
            _sequence = spell.GlobalSequence;
            if (++_hits == 5)
                Towers.Clear();
            else
            {
                var tower = Towers[0];
                tower.Activation = WorldState.FutureTime(2d);
                Towers[0] = tower;
            }
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID._Weaponskill_HeartbreakKick && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            Towers.Clear(); // interrupted windup; successful cast finish precedes the five helper pulses
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = (uint)OID.Boss, Contributors = "VeraNala, wen, Topas, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 1072u, NameID = 14305u)]
public sealed class M11NTheTyrant(WorldState ws, Actor primary) : BossModule(ws, primary, new(100f, 100f), ArenaChanges.InitialBounds);
