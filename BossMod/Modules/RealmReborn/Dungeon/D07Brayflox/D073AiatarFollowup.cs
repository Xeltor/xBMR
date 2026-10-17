namespace BossMod.RealmReborn.Dungeon.D07Brayflox.D073AiatarFollowup;

public enum OID : uint
{
    Boss = 0x1AD, // Aiatar in Hellbender's arena, distinct from final Aiatar 0x38C5
}

public enum AID : uint
{
    SalivousSnap = 29617, // Boss -> tank, 5.0s single-target cast
    DragonBreath = 29618, // Boss -> self, 3.0s cast, range 30 width 8 rectangle
}

sealed class SalivousSnap(BossModule module) : Components.SingleTargetCast(module, (uint)AID.SalivousSnap)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.SalivousSnap))
        {
            Casters.Clear(); // one primary caster; bootstrap/repeated starts replace the instance
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.SalivousSnap))
            base.OnCastFinished(caster, spell);
    }

    public override void OnActorDeath(Actor actor) => Casters.Remove(actor);
    public override void OnActorDestroyed(Actor actor) => Casters.Remove(actor);
}

sealed class DragonBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DragonBreath, new AOEShapeRect(30f, 4f))
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.DragonBreath))
        {
            Casters.Clear();
            base.OnCastStarted(caster, spell);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.DragonBreath))
            base.OnCastFinished(caster, spell);
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
    }
}

sealed class D073AiatarFollowupStates : StateMachineBuilder
{
    public D073AiatarFollowupStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<SalivousSnap>()
            .ActivateOnEnter<DragonBreath>();
    }
}

// Reuse the existing Hellbender floor rather than deriving one from player paths.
// Its physical boundary and exact cast snapshots remain uncalibrated: keep WIP.
[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.CFC, GroupID = 8, NameID = 1279, PrimaryActorOID = (uint)OID.Boss)]
public sealed class D073AiatarFollowup(WorldState ws, Actor primary) : BossModule(ws, primary, D073Hellbender.D120Hellbender.arena.Center, D073Hellbender.D120Hellbender.arena)
{
    protected override bool CheckPull() => WorldState.CurrentZone == 1041 && WorldState.CurrentCFCID == 8 && !PrimaryActor.IsAlly && !PrimaryActor.IsDeadOrDestroyed && PrimaryActor.IsTargetable && PrimaryActor.InCombat;

    // Aiatar becomes untargetable before Salivous Snap finishes, then flies away.
    // Keep its cast guidance until the normal finish/cancel or actor destruction.
    public override bool CheckReset() => WorldState.CurrentZone != 1041 || WorldState.CurrentCFCID != 8;
}
