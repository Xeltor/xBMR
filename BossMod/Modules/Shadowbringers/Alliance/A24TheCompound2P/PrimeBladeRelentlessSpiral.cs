namespace BossMod.Shadowbringers.Alliance.A24TheCompound2P;

sealed class RelentlessSpiral(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RelentlessSpiral1, 8f);
sealed class PrimeBladeCircle(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PrimeBladeCircle1, 20f);
sealed class PrimeBladeRect(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PrimeBladeRect1, PrimeBladeTransfer.Rect);
sealed class PrimeBladeDonut(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PrimeBladeDonut1, PrimeBladeTransfer.Donut);

sealed class RelentlessSpiralTransfer(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [with(3)];
    private static readonly AOEShapeCircle circle = new(8f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == (uint)TetherID.Transfer2)
        {
            _aoes.Add(new(circle, source.Position.Quantized(), default, WorldState.FutureTime(8.6d)));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.RelentlessSpiral2)
        {
            _aoes.Clear();
        }
    }
}

sealed class PrimeBladeTransfer(BossModule module) : Components.GenericAOEs(module, warningText: "Dodge the transferred Prime Blade!")
{
    private sealed class TransferCast(ActorCastInfo spell, AOEShape shape, WPos origin, DateTime activation)
    {
        public readonly ActorCastInfo Spell = spell;
        public readonly AOEShape Shape = shape;
        public readonly WPos Origin = origin;
        public readonly DateTime Activation = activation;
        public WPos? Destination;
        public Angle? ArrivalFacing;
    }

    private readonly Dictionary<Actor, Actor> _links = [];
    private readonly Dictionary<Actor, TransferCast> _casts = [];
    private static readonly AOEShapeCircle Circle = new(20f);
    public static readonly AOEShapeDonut Donut = new(8f, 43f);
    public static readonly AOEShapeRect Rect = new(85f, 10f);

    private static AOEShape? Shape(uint action) => action switch
    {
        (uint)AID.PrimeBladeCircle2 => Circle,
        (uint)AID.PrimeBladeDonut2 => Donut,
        (uint)AID.PrimeBladeRect2 => Rect,
        _ => null
    };

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == (uint)TetherID.Transfer1 && WorldState.Actors.Find(tether.Target) is { } target)
            _links[source] = target;
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID == (uint)TetherID.Transfer1 && _links.GetValueOrDefault(source)?.InstanceID == tether.Target)
            _links.Remove(source);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (Shape(spell.Action.ID) is { } shape && _casts.GetValueOrDefault(caster)?.Spell != spell)
            _casts[caster] = new(spell, shape, caster.Position, Module.CastFinishAt(spell));
    }

    public override void Update()
    {
        foreach (var (caster, cast) in _casts.ToArray())
        {
            if (caster.IsDeadOrDestroyed || WorldState.CurrentTime > cast.Activation.AddSeconds(1))
            {
                _casts.Remove(caster);
                continue;
            }

            if (cast.Destination == null)
            {
                // Helper poses can arrive after the tether in the same frame.
                var destinations = _links.Where(l => !l.Key.IsDeadOrDestroyed && !l.Value.IsDeadOrDestroyed
                    && l.Value.Position.AlmostEqual(cast.Origin, 0.75f) && !l.Key.Position.AlmostEqual(cast.Origin, 1))
                    .Select(l => l.Key.Position.Quantized()).Distinct().Take(2).ToArray();
                if (destinations.Length == 1)
                    cast.Destination = destinations[0];
            }

            // The tether identifies the destination. The specific caster's first
            // observed pose there supplies the line direction.
            if (cast.Shape == Rect && cast.ArrivalFacing == null && cast.Destination is { } pos && caster.Position.AlmostEqual(pos, 1))
                cast.ArrivalFacing = caster.Rotation;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        List<AOEInstance> aoes = [];
        foreach (var (caster, cast) in _casts)
            if (cast.Destination is { } pos && (cast.Shape != Rect || cast.ArrivalFacing.HasValue))
                aoes.Add(new(cast.Shape, pos, cast.ArrivalFacing ?? default, cast.Activation, actorID: caster.InstanceID));
        return CollectionsMarshal.AsSpan(aoes);
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_casts.Values.Any(c => c.Shape == Rect && c.ArrivalFacing == null))
            hints.Add("Prime Blade: prepare to dodge the line after the teleport!");
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _casts.Remove(actor);
        _links.Remove(actor);
        foreach (var link in _links.Where(l => l.Value == actor).ToArray())
            _links.Remove(link.Key);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (_casts.GetValueOrDefault(caster)?.Spell == spell)
            _casts.Remove(caster);
        else if (spell.Action.ID is (uint)AID.ForcedTransfer1 or (uint)AID.ForcedTransfer2)
            _links.Clear(); // other mechanics use the same tether ID
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (_casts.GetValueOrDefault(caster)?.Spell.Action == spell.Action)
            _casts.Remove(caster);
    }
}
