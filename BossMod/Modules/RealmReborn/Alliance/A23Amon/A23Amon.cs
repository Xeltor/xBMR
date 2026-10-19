namespace BossMod.RealmReborn.Alliance.A23Amon;

class BlizzagaForte(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BlizzagaForte, 10f);
class Darkness(BossModule module) : Components.CastHint(module, (uint)AID.Darkness, "Darkness: get behind the casting add or outside its reach!", true)
{
    public override void OnActorDestroyed(Actor actor) => Casters.Remove(actor);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.DeepFreezeLegacy) != null)
            return;
        foreach (var caster in Casters)
            if (!caster.IsDeadOrDestroyed && caster.CastInfo is { } cast)
                // The range is supplied by Action1875 and the live add hitbox.
                // Rear-position response replaces an unsupported inherited cone angle.
                hints.AddForbiddenZone(new SDIntersection([new SDCircle(caster.Position, 6 + caster.HitboxRadius),
                    new SDHalfPlane(caster.Position, -cast.Rotation.ToDirection())]), Module.CastFinishAt(cast, -0.2));
    }
}
class ColoraturaPositioning(BossModule module) : Components.FrontalPositioning(module, 8)
{
    protected override bool AppliesToActor(Actor actor) => base.AppliesToActor(actor)
        && actor.FindStatus(SID.DeepFreeze) == null && actor.FindStatus(SID.DeepFreezeLegacy) == null
        && !Module.Enemies((uint)OID.DimensionalCompression).Any(a => !a.IsDeadOrDestroyed && a.Tether.Target == actor.InstanceID);
}

class CurtainCall(BossModule module) : Components.GenericLineOfSightAOE(module, (uint)AID.CurtainCall, 100, safeInsideHitbox: false)
{
    public override void Update()
    {
        Safezones.Clear();
        IgnoredPlayers = default;
        foreach (var (slot, actor) in Raid.WithSlot())
            if (actor.FindStatus(SID.DeepFreeze) != null || actor.FindStatus(SID.DeepFreezeLegacy) != null)
                IgnoredPlayers.Set(slot);
        var boss = Module.PrimaryActor;
        if (boss.IsDeadOrDestroyed || boss.CastInfo is not { } cast || cast.Action.ID != WatchedAction)
        {
            Modify(null, []);
            return;
        }
        Modify(boss.Position, Module.Enemies((uint)OID.IceCage).Where(c => !c.IsDeadOrDestroyed
            && (c.Position - boss.Position).LengthSq() > boss.HitboxRadius * boss.HitboxRadius)
            .Select(c => (c.Position, c.HitboxRadius)), Module.CastFinishAt(cast, -0.5));
        AddSafezone(NextExplosion);
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == WatchedAction)
            Update();
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == WatchedAction)
        {
            Safezones.Clear();
            Modify(null, []);
        }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (IgnoredPlayers[slot])
            return;
        if (Origin is { } origin && Safezones.Count == 0)
            hints.AddForbiddenZone(new SDCircle(origin, MaxRange), NextExplosion);
        else
            base.AddAIHints(slot, actor, assignment, hints);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (IgnoredPlayers[slot])
            return;
        if (Origin != null && Safezones.Count == 0)
            hints.Add("Curtain Call: no intact ice outside Amon - cover unavailable!");
        else
            base.AddHints(slot, actor, hints);
    }
}

class ThundagaForte1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ThundagaForte1, 6f);
class ThundagaForte2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ThundagaForte2, 6f);

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 102, NameID = 2821)]
public class A23Amon(WorldState ws, Actor primary) : BossModule(ws, primary, new(default, -200f), new ArenaBoundsCircle(30f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.KumKum));
        Arena.Actors(Enemies((uint)OID.Kichiknebik));
        Arena.Actors(Enemies((uint)OID.ExperimentalByProduct66));
        Arena.Actors(Enemies((uint)OID.ExperimentalByProduct33));
    }
}
