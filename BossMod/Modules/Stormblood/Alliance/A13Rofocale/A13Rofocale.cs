namespace BossMod.Stormblood.Alliance.A13Rofocale;

class CryOfVictory(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.CryOfVictory1, (uint)AID.CryOfVictory2], new AOEShapeCone(60f, 90f.Degrees()))
{
    private bool _afterSubjugation;
    private DateTime _prepareUntil;
    private WPos? _trampleReturn;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (caster != Module.PrimaryActor)
            return;

        ClearPreparation();
        if (_afterSubjugation && spell.IsSpell(AID.Trample1))
        {
            _trampleReturn = spell.LocXZ;
            _prepareUntil = Module.CastFinishAt(spell, 4.5d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.Trample1) && !spell.EventHappened)
            ClearPreparation();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.IsSpell(AID.HeavenlySubjugation2) || spell.IsSpell(AID.HeavenlySubjugation3))
            _afterSubjugation = true;
        else if (caster == Module.PrimaryActor && spell.IsSpell(AID.Chariot))
            _prepareUntil = WorldState.FutureTime(3d);
        else if (spell.Action.ID is (uint)AID.CryOfVictory1 or (uint)AID.CryOfVictory2)
            Casters.RemoveAll(cone => cone.ActorID == caster.InstanceID);
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_prepareUntil > WorldState.CurrentTime)
            hints.Add("Cry of Victory follows. Stay near Rofocale when safe so you can get behind him.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (_prepareUntil > WorldState.CurrentTime)
            hints.GoalZones.Add(AIHints.GoalProximity(_trampleReturn ?? Module.PrimaryActor.Position, 35f, 6f));
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _afterSubjugation = false;
            ClearPreparation();
            Casters.Clear();
        }
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    private void ClearPreparation()
    {
        _prepareUntil = default;
        _trampleReturn = null;
    }
}

class Chariot(BossModule module) : Components.BaitAwayChargeCast(module, (uint)AID.Chariot, HalfWidth)
{
    private const float HalfWidth = 5f;
    private DateTime _unconfirmedUntil;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        var boss = Module.PrimaryActor;
        if (iconID != (uint)IconID.Chariot || CurrentBaits.Count != 0 || boss.IsDeadOrDestroyed)
            return;

        var target = WorldState.Actors.Find(targetID) ?? actor;
        if (Raid.FindSlot(target.InstanceID) < 0 || target.IsDeadOrDestroyed)
            return;

        // Both recorded casts begin about 5.25s after the mark and hit at 8.22s.
        _unconfirmedUntil = WorldState.FutureTime(6d);
        var shape = new AOEShapeRect((target.Position - boss.Position).Length(), HalfWidth);
        CurrentBaits.Add(new(boss, target, shape, WorldState.FutureTime(7.8d)));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor || !spell.IsSpell(AID.Chariot))
            return;

        ClearBait();
        base.OnCastStarted(caster, spell);
    }

    public override void Update()
    {
        if (_unconfirmedUntil != default && WorldState.CurrentTime >= _unconfirmedUntil)
            ClearBait();
        base.Update();
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IsBaitTarget(actor))
            hints.Add("Shorten Chariot by moving toward Rofocale. Keep the lane clear of allies.");
        else
            base.AddHints(slot, actor, hints);
    }

    protected override void AddTargetSpecificHints(Actor actor, ref Bait bait, AIHints hints)
    {
        var origin = bait.Source.Position;
        var shorten = AIHints.GoalProximity(origin, Math.Max(12f, actor.DistanceToPoint(origin) + 2f), 8f);
        var peers = Raid.WithoutSlot(excludeNPCs: true).Where(player => player != actor)
            .Select(player => player.Position).ToArray();
        hints.GoalZones.Add(position =>
        {
            // A short charge can stop before a player on the same bearing.
            // Prefer a clear lane without enforcing a hard constraint on moving allies.
            var clear = !peers.Any(peer => peer.InRect(origin, position, HalfWidth));
            return shorten(position) + (clear ? 8f : 0f);
        });
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.Chariot))
            ClearBait();
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor || CurrentBaits.Any(bait => bait.Target == actor))
            ClearBait();
    }

    private void ClearBait()
    {
        _unconfirmedUntil = default;
        CurrentBaits.Clear();
    }
}

class CrushWeapon(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CrushWeapon2, 6f);
class Karma(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Karma, new AOEShapeCone(30f, 45f.Degrees()))
{
    public override void OnActorDeath(Actor actor) => Casters.RemoveAll(cast => cast.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}
class UnholyDarkness(BossModule module) : Components.SimpleAOEs(module, (uint)AID.UnholyDarkness, 8f);
class PompAndCircumstance(BossModule module) : Components.ProximityAOEs(module, (uint)AID.PompAndCircumstance, 15f)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var aoe in ActiveAOEs(slot, actor))
        {
            var origin = aoe.Origin;
            hints.GoalZones.Add(p => Math.Min((p - origin).Length(), 30f));
        }
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6941)]
public class A13Rofocale(WorldState ws, Actor primary) : BossModule(ws, primary, new(106f, -191f), new ArenaBoundsCircle(29.5f))
{
    public override string[] PrePullHints =>
    [
        "Shorten Chariot if marked, then get behind Cry of Victory.",
        "During Trample, use an inner gap or stay outside the outlined figure eight.",
        "Enter your alliance's Archaeodemon prison when the adds appear. Interact with Dark Circles during Dark Geas.",
    ];
}
