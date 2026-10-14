namespace BossMod.Heavensward.Alliance.A34Scathach;

class ThirtyCries(BossModule module) : Components.StackWithIcon(module, (uint)IconID.ThirtyCries, (uint)AID.ThirtyCries, 12, 5.1d)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        Update();
        if (Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed
            || iconID == (uint)IconID.ThirtyCries && Stacks.Exists(s => s.Target == actor))
            return;
        base.OnEventIcon(actor, iconID, targetID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        var singleHit = spell.Action.ID == (uint)AID.ThirtyCries;
        if (singleHit)
            CastCounter = 0;
        base.OnEventCast(caster, spell);
        if (singleHit)
            CastCounter = 0; // an orphaned late hit must not poison the next stack's single-hit counter
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed)
            Stacks.Clear();
        else
            Stacks.RemoveAll(s => s.Target.IsDead || s.Target.IsDestroyed || WorldState.CurrentTime > s.Activation.AddSeconds(2d));
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Stacks.Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Stacks.Clear();
        else
            Stacks.RemoveAll(s => s.Target == actor);
    }
}
class ThirtySouls(BossModule module) : Components.RaidwideCast(module, (uint)AID.ThirtySouls);
class ThirtyArrows2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ThirtyArrows2, new AOEShapeRect(35.5f, 4));
class ThirtyArrows1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ThirtyArrows1, 8);
class Shadethrust(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Shadethrust, new AOEShapeRect(45, 2.5f))
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            Casters.RemoveAll(a => a.ActorID == caster.InstanceID);
            // The location is the aim point; the cleave originates at Scathach.
            Casters.Add(new(Shape, caster.Position, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID));
        }
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}

class Shadesmite2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Shadesmite2, 3);
class Shadesmite3(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Shadesmite3, 3);

class Pitfall(BossModule module) : Components.RaidwideCast(module, (uint)AID.Pitfall, "Proximity damage: move away from impact; Connla incoming!");
class FullSwing(BossModule module) : Components.RaidwideCast(module, (uint)AID.FullSwing);

class BigHug(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BigHug, new AOEShapeRect(5.25f, 1.5f));

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 5515)]
public class A34Scathach(WorldState ws, Actor primary) : BossModule(ws, primary, new(0, -50), new ArenaBoundsCircle(30))
{
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            // Keep native eligibility (invulnerability, dying/forbidden targets) and manual control.
            if (enemy.Priority < 0)
                continue;
            enemy.Priority = enemy.Actor.OID switch
            {
                (uint)OID.Connla2 or (uint)OID.ShadowLimb => 2,
                (uint)OID.ShadowcourtJester or (uint)OID.ChimeraPoppet or (uint)OID.ShadowcourtHound => 1,
                _ => enemy.Priority
            };
            if (enemy.Actor.OID == (uint)OID.ShadowcourtHound && enemy.Actor.CastInfo is { Interruptible: true })
                enemy.ShouldBeInterrupted = true;
        }
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Connla));
        Arena.Actors(Enemies((uint)OID.Connla2));
        Arena.Actors(Enemies((uint)OID.ShadowLimb));
        Arena.Actors(Enemies((uint)OID.ShadowcourtJester));
        Arena.Actors(Enemies((uint)OID.ChimeraPoppet));
        Arena.Actors(Enemies((uint)OID.ShadowcourtHound));
    }
}
