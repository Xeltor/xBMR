namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

// Exact installed EObj2007621 -> b1048.sgb C059 -> AVFX trigger/emitter/RGB proof is recorded in the encounter notes.
// These are colour observations and point labels, not a verified toggle radius or an automated crossing assignment.
class Debilitator(BossModule module) : BossComponent(module)
{
    private enum PuddleColor { Unknown, Red, Blue }
    private readonly Dictionary<ulong, (Actor Actor, PuddleColor Color)> _puddles = [];
    private bool _preparing;
    private bool _resolved;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 2007621 && (!_puddles.TryGetValue(actor.InstanceID, out var old) || old.Actor != actor))
            _puddles[actor.InstanceID] = (actor, PuddleColor.Unknown);
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (_resolved || actor.OID != 2007621 || WorldState.Actors.Find(actor.InstanceID) != actor)
            return;
        // BossModule packs (shared timeline state << 16) | immediate timeline bitmask.
        // C059 selector3 -> trigger2 -> emitter1 (blue); selector2 -> trigger1 -> emitter0 (red).
        if (state is 0x00010002 or 0x00040008)
            _puddles.Remove(actor.InstanceID);
        else
            _puddles[actor.InstanceID] = (actor, state switch
            {
                0x00100020 or 0x04000800 => PuddleColor.Blue,
                0x00400080 or 0x01000200 => PuddleColor.Red,
                _ => PuddleColor.Unknown // Never retain an old colour after an unrecognized observation.
            });
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_puddles.TryGetValue(actor.InstanceID, out var old) && old.Actor == actor)
            _puddles.Remove(actor.InstanceID);
    }

    public override void Update()
    {
        foreach (var (id, puddle) in _puddles.ToArray())
            if (puddle.Actor.IsDestroyed || WorldState.Actors.Find(id) != puddle.Actor)
                _puddles.Remove(id);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Debilitator)
        {
            _preparing = true;
            _resolved = false;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Debilitator)
            _preparing = false;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Flameflow2 or (uint)AID.Flameflow3)
        {
            _preparing = false;
            _resolved = true;
            _puddles.Clear();
        }
    }

    private static PuddleColor DesiredColor(Actor actor)
    {
        var fire = actor.FindStatus((uint)SID.FireResistanceDownII) != null;
        var water = actor.FindStatus((uint)SID.WaterResistanceDownII) != null;
        return fire == water ? PuddleColor.Unknown : fire ? PuddleColor.Blue : PuddleColor.Red;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_preparing)
            hints.Add("Debilitator: prepare to change puddles to the opposite element!");
        var desired = DesiredColor(actor);
        if (!_resolved && desired != PuddleColor.Unknown && _puddles.Count != 0)
        {
            var unknown = _puddles.Values.Count(p => p.Color == PuddleColor.Unknown);
            var wrong = _puddles.Values.Count(p => p.Color != PuddleColor.Unknown && p.Color != desired);
            hints.Add($"Puddles: {wrong} wrong, {unknown} unknown (need {(desired == PuddleColor.Red ? "red/fire" : "blue/water")})");
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_resolved)
            return;
        switch (DesiredColor(actor))
        {
            case PuddleColor.Blue: hints.Add("Turn puddles blue (water) before Flameflow!", false); break;
            case PuddleColor.Red: hints.Add("Turn puddles red (fire) before Flameflow!", false); break;
        }
    }

    // Shared by the renderer and callback regression checks; labels are anchored to exact object positions.
    public IEnumerable<(Actor Actor, string Label, uint Color)> PuddleMarkers(Actor player)
    {
        var desired = DesiredColor(player);
        if (_resolved || desired == PuddleColor.Unknown)
            yield break;
        foreach (var puddle in _puddles.Values)
            yield return (puddle.Actor, puddle.Color switch
            {
                PuddleColor.Unknown => "?: unknown",
                PuddleColor.Red => desired == PuddleColor.Red ? "RED: keep" : "RED: change",
                _ => desired == PuddleColor.Blue ? "BLUE: keep" : "BLUE: change"
            }, puddle.Color == PuddleColor.Unknown ? Colors.Object : puddle.Color == desired ? Colors.Safe : Colors.Danger);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var marker in PuddleMarkers(pc))
            Arena.TextWorld(marker.Actor.Position, marker.Label, marker.Color);
    }
}
