namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

// Spawn timelines identify weapons even though the actors are preallocated at arena center.
// Evolved uses the explicit tether chain; Apex uses the observed spawn order.
sealed class WeaponAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private sealed record Weapon(Actor Actor, AOEShape Shape);
    private readonly List<Weapon> _weapons = [];
    private readonly List<(Actor Caster, uint Action, AOEInstance AOE)> _casts = [];
    private readonly Dictionary<ulong, DateTime> _deadlines = [];
    private AOEInstance[] _output = [];
    private AOEShape? _drawn;
    private DateTime _drawActivation;
    private bool _apex;
    private DateTime _next;
    private static readonly AOEShapeCircle _axe = new(8f);
    private static readonly AOEShapeDonut _scythe = new(5f, 60f);
    private static readonly AOEShapeCross _sword = new(40f, 5f);

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        var shape = id switch { 4561 => (AOEShape)_sword, 4562 => _axe, 4563 => _scythe, _ => null };
        if (shape == null || actor.OID is not ((uint)OID.Axe) and not ((uint)OID.Scythe) and not ((uint)OID.Sword))
            return;
        _weapons.RemoveAll(w => w.Actor == actor);
        _weapons.Add(new(actor, shape));
        if (_apex && _next != default && !_deadlines.ContainsKey(actor.InstanceID))
        {
            _deadlines[actor.InstanceID] = _next;
            _next = _next.AddSeconds(3.65d);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var aid = (AID)spell.Action.ID;
        var drawn = aid switch
        {
            AID._Weaponskill_DrawSteel => (AOEShape)_axe,
            AID._Weaponskill_DrawSteel1 => _scythe,
            AID._Weaponskill_DrawSteel2 => _sword,
            _ => null
        };
        if (drawn != null)
        {
            _drawn = drawn;
            _drawActivation = Module.CastFinishAt(spell, 9.1d);
        }
        if (aid is AID._Weaponskill_TrophyWeapons or AID._Weaponskill_UltimateTrophyWeapons)
        {
            _weapons.Clear();
            _deadlines.Clear();
            _next = default;
            _apex = aid == AID._Weaponskill_UltimateTrophyWeapons;
        }
        if (aid is AID._Weaponskill_AssaultEvolvedCast or AID._Weaponskill_AssaultApex)
        {
            _deadlines.Clear();
            _next = Module.CastFinishAt(spell, 2.3d);
            if (_apex)
            {
                foreach (var w in _weapons)
                {
                    _deadlines[w.Actor.InstanceID] = _next;
                    _next = _next.AddSeconds(3.65d);
                }
            }
            else
            {
                var id = Module.PrimaryActor.Tether.Target;
                for (var i = 0; i < 3 && id != 0; ++i)
                {
                    var weapon = _weapons.Find(w => w.Actor.InstanceID == id);
                    if (weapon == null || _deadlines.ContainsKey(id))
                        break;
                    _deadlines[id] = _next;
                    _next = _next.AddSeconds(4.15d);
                    id = weapon.Actor.Tether.Target;
                }
            }
        }
        var shape = aid switch
        {
            AID._Weaponskill_SmashdownAxeAOE or AID._Weaponskill_AssaultEvolvedAxe => (AOEShape)_axe,
            AID._Weaponskill_SmashdownScytheAOE or AID._Weaponskill_AssaultEvolvedScythe => _scythe,
            AID._Weaponskill_SmashdownSwordAOE or AID._Weaponskill_AssaultEvolvedSword => _sword,
            _ => null
        };
        if (shape == null)
            return;
        _drawn = null;
        foreach (var w in _weapons.Where(w => w.Shape == shape && w.Actor.Position.AlmostEqual(spell.LocXZ, 1f)).ToArray())
        {
            _deadlines.Remove(w.Actor.InstanceID);
            _weapons.Remove(w);
        }
        _casts.RemoveAll(c => c.Caster == caster);
        _casts.Add((caster, spell.Action.ID, new(shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID)));
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var result = new List<AOEInstance>();
        result.AddRange(_casts.Select(c => c.AOE));
        if (_drawn != null)
            result.Add(new(_drawn, Module.PrimaryActor.Position, Module.PrimaryActor.Rotation, _drawActivation));
        // Keep later weapons visible, but let the active/next weapon own immediate movement.
        var next = _deadlines.Count != 0 ? _deadlines.Values.Min() : DateTime.MaxValue;
        foreach (var w in _weapons)
            if (_deadlines.TryGetValue(w.Actor.InstanceID, out var deadline))
                result.Add(new(w.Shape, w.Actor.Position, w.Actor.Rotation, deadline, risky: _casts.Count == 0 && deadline == next, actorID: w.Actor.InstanceID));
        _output = result.ToArray();
        Components.AOESequence.HighlightNext(_output);
        return _output;
    }
    public override void DrawArenaBackground(int pcSlot, Actor pc) => Components.AOESequence.Draw(Arena, ActiveAOEs(pcSlot, pc));
    public override void OnEventCast(Actor caster, ActorCastEvent spell) => _casts.RemoveAll(c => c.Caster == caster && c.Action == spell.Action.ID);
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        _casts.RemoveAll(c => c.Caster == caster && c.Action == spell.Action.ID);
        if (spell.Action.ID is (uint)AID._Weaponskill_AssaultEvolvedCast or (uint)AID._Weaponskill_AssaultApex or (uint)AID._Weaponskill_DrawSteel or (uint)AID._Weaponskill_DrawSteel1 or (uint)AID._Weaponskill_DrawSteel2
            && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
        {
            _deadlines.Clear();
            _drawn = null;
        }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _casts.RemoveAll(c => c.Caster == actor);
        _weapons.RemoveAll(w => w.Actor == actor);
        _deadlines.Remove(actor.InstanceID);
    }
}
