namespace BossMod.RealmReborn.Alliance.A13KingBehemoth;

// Comets can appear after the cast begins. Rebuild cover from current actors, not a cast-start snapshot.
class EclipticMeteor(BossModule module) : Components.GenericLineOfSightAOE(module, (uint)AID.EclipticMeteor, 100f, safeInsideHitbox: false)
{
    public override void Update()
    {
        Safezones.Clear();
        var boss = Module.PrimaryActor;
        if (boss.IsDeadOrDestroyed || boss.CastInfo is not { } cast || cast.Action.ID != WatchedAction)
        {
            Modify(null, []);
            return;
        }
        Modify(boss.Position, Module.Enemies((uint)OID.Comet)
            .Where(c => !c.IsDeadOrDestroyed && (c.Position - boss.Position).LengthSq() > boss.HitboxRadius * boss.HitboxRadius)
            .Select(c => (c.Position, c.HitboxRadius)), Module.CastFinishAt(cast));
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

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Origin != null && Safezones.Count == 0)
            hints.Add("Ecliptic Meteor: no intact comet outside the boss - cover unavailable!");
        else
            base.AddHints(slot, actor, hints);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Origin is { } origin && Safezones.Count == 0)
            hints.AddForbiddenZone(new SDCircle(origin, MaxRange), NextExplosion);
        else
            base.AddAIHints(slot, actor, assignment, hints);
    }
}

class CometMarkers(BossModule module) : Components.GenericBaitAway(module, centerAtTarget: true)
{
    public override void OnEventVFX(Actor actor, uint vfxID, ulong targetID)
    {
        if (vfxID == 298 && actor.Type == ActorType.Player)
        {
            CurrentBaits.RemoveAll(b => b.Target == actor);
            // Observed marker-to-impact is about 13s; use 12s while the exact ground lock is unverified.
            CurrentBaits.Add(new(actor, actor, new AOEShapeCircle(4), WorldState.FutureTime(12)));
        }
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Target.IsDeadOrDestroyed
        || WorldState.Actors.Find(b.Target.InstanceID) != b.Target || WorldState.CurrentTime > b.Activation.AddSeconds(2));

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // All eight observed Comet impacts list the independently marked player first.
        // Retire that marker only; the other comets can still be falling.
        if (spell.Action.ID == (uint)AID.Comet && spell.Targets.Count != 0)
            CurrentBaits.RemoveAll(b => b.Target.InstanceID == spell.Targets[0].ID);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor))
            hints.Add("Drop comet outside Behemoth's hitbox and away from other players", false);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var bait in CurrentBaits)
            if (bait.Target == actor)
            {
                hints.AddForbiddenZone(new SDCircle(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius), bait.Activation);
                // This bait is self-centered, so GenericBaitAway cannot derive a source-to-target
                // spread route. Keep the observed4y Comet blast away from other living players.
                foreach (var other in Raid.WithoutSlot())
                    if (other != actor && other.Type == ActorType.Player && Arena.InBounds(other.Position))
                        hints.AddForbiddenZone(new SDCircle(other.Position, 4), bait.Activation);
            }
    }
}

class Adds(BossModule module) : BossComponent(module)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            if (enemy.Actor.OID == (uint)OID.Comet)
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
            else if (enemy.Actor.InCombat && enemy.Priority >= 0)
                enemy.Priority = enemy.Actor.OID switch { (uint)OID.IronGiant => 3, (uint)OID.Puroboros => 2, _ => enemy.Priority };
        }
    }
}

class SelfDestruct(BossModule module) : Components.SimpleAOEs(module, (uint)AID.SelfDestruct, 8.4f);
class GrandSword(BossModule module) : Components.Cleave(module, (uint)AID.GrandSword, new AOEShapeCone(15, 60.Degrees()), [(uint)OID.IronGiant], activeWhileCasting: false);
class CharybdisAOE(BossModule module) : Components.VoidzoneAtCastTarget(module, 6f, (uint)AID.CharybdisVisual, GetVoidzones, 0.1f)
{
    private static Actor[] GetVoidzones(BossModule module)
    {
        var enemies = module.Enemies((uint)OID.Charybdis);
        var count = enemies.Count;
        if (count == 0)
            return [];

        var voidzones = new Actor[count];
        var index = 0;
        for (var i = 0; i < count; ++i)
        {
            var z = enemies[i];
            if (z.EventState != 7)
                voidzones[index++] = z;
        }
        return voidzones[..index];
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 92, NameID = 727)]
public class A13KingBehemoth(WorldState ws, Actor primary) : BossModule(ws, primary, new(-110, -368.384f), new ArenaBoundsCircle(30))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.IronGiant));
        Arena.Actors(Enemies((uint)OID.Puroboros));
    }
}
