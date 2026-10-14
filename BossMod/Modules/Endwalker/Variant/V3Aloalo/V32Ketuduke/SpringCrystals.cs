namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

sealed class SpringCrystals(BossModule module) : Components.GenericAOEs(module)
{
    private sealed class Crystal(Actor actor)
    {
        public readonly Actor Actor = actor;
        public readonly WPos Origin = actor.Position;
        public bool Bubbled;
        public ActorCastInfo? Wind;
        public ActorCastInfo? Cast;
        public AOEInstance? AOE;
    }

    private readonly List<Crystal> crystals = [];
    private readonly List<AOEInstance> visible = [];
    private static readonly AOEShapeCircle sphere = new(8f);
    private static readonly AOEShapeRect centeredRect = new(38f, 5f, 38f);
    private static readonly AOEShapeRect castRect = new(76f, 5f);

    private static bool IsCrystal(Actor actor) => actor.OID is (uint)OID.SpringCrystalSphere or (uint)OID.SpringCrystalRect;
    private static bool IsSaturate(uint id) => id is (uint)AID.SaturateSphere or (uint)AID.SaturateSphereFast or (uint)AID.SaturateRect or (uint)AID.SaturateRectFast;
    private static AOEShape EarlyShape(Actor actor) => actor.OID == (uint)OID.SpringCrystalSphere ? sphere : centeredRect;
    private bool Live(Actor actor) => !actor.IsDestroyed && WorldState.Actors.Find(actor.InstanceID) == actor;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        visible.Clear();
        foreach (var crystal in crystals)
            if (Live(crystal.Actor) && crystal.AOE is { } aoe)
                visible.Add(aoe);
        return CollectionsMarshal.AsSpan(visible);
    }

    public override void Update() => crystals.RemoveAll(c => !Live(c.Actor));

    public override void OnActorCreated(Actor actor)
    {
        if (IsCrystal(actor) && !crystals.Any(c => c.Actor == actor))
        {
            crystals.RemoveAll(c => c.Actor.InstanceID == actor.InstanceID);
            crystals.Add(new(actor));
        }
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.Bubble && status.Extra == 0xC8 && crystals.Find(c => c.Actor == actor) is { } crystal && !crystal.Bubbled)
        {
            crystal.Bubbled = true;
            // Bubble Net invalidates the entire wave's static deadline. Wait for
            // an observed wind direction; don't guess movement towards center.
            foreach (var entry in crystals)
                if (entry.Wind == null)
                    entry.AOE = null;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var id = spell.Action.ID;
        if (id == (uint)AID.BubbleNet)
        {
            foreach (var entry in crystals)
                entry.AOE = null;
        }
        else if (id == (uint)AID.FlukeTyphoonAOE)
        {
            foreach (var entry in crystals)
            {
                if (entry.Wind == spell)
                    continue;
                entry.Wind = spell;
                var origin = entry.Origin + (entry.Bubbled ? 20f * spell.Rotation.ToDirection() : default);
                entry.AOE = new(EarlyShape(entry.Actor), origin, entry.Actor.Rotation, Module.CastFinishAt(spell, 3.1d), actorID: entry.Actor.InstanceID);
            }
        }
        else if (IsSaturate(id))
        {
            OnActorCreated(caster); // Also supports activation during an existing cast.
            if (crystals.Find(c => c.Actor == caster) is { } crystal)
            {
                crystal.Cast = spell;
                // Rectangular casts start at the far end of the 76-yalm line;
                // their cast location is not the crystal's current position.
                crystal.AOE = new(caster.OID == (uint)OID.SpringCrystalSphere ? sphere : castRect,
                    spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID);
            }
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        var id = spell.Action.ID;
        if (id is (uint)AID.SphereAppear or (uint)AID.RectAppear && crystals.Find(c => c.Actor == caster) is { } crystal)
        {
            crystal.AOE = new(EarlyShape(caster), crystal.Origin, caster.Rotation, WorldState.FutureTime(14d), actorID: caster.InstanceID);
        }
        else if (IsSaturate(id))
        {
            if (crystals.RemoveAll(c => c.Actor == caster) != 0)
                ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor) => crystals.RemoveAll(c => c.Actor == actor);

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && IsSaturate(spell.Action.ID))
            crystals.RemoveAll(c => c.Actor == caster && c.Cast == spell);
    }
}
