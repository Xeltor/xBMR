namespace BossMod.RealmReborn.Dungeon.D07Brayflox.D074Aiatar;

public enum OID : uint
{
    Boss = 0x38C5, // x1
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 870, // Boss->player, no cast

    SalivousSnap = 28659, // Boss->player, 5.0s cast, tankbuster
    ToxicVomit = 28656, // Boss->self, 3.0s cast, visual
    ToxicVomitAOE = 28657, // Helper->self, 5.0s cast, range 2 aoe
    Burst = 28658, // Helper->self, 9.0s cast, range 10 aoe
    DragonBreath = 28660 // Boss->self, 3.0s cast, range 30 width 8 rect
}

class SalivousSnap(BossModule module) : Components.SingleTargetCast(module, (uint)AID.SalivousSnap);
class ToxicVomit(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ToxicVomitAOE, 2);
// Keep every timed hazard for AI planning; only the earliest cohort is drawn.
class Burst(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Burst, 10)
{
    public ReadOnlySpan<AOEInstance> PreviewAOEs(int slot, Actor actor)
    {
        var aoes = ActiveAOEs(slot, actor);
        if (aoes.Length == 0)
            return aoes;
        var latest = aoes[0].Activation.AddSeconds(0.25); // display grouping only
        var count = 1;
        while (count < aoes.Length && aoes[count].Activation <= latest)
            ++count;
        return aoes[..count];
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (ref readonly var aoe in PreviewAOEs(slot, actor))
            if (aoe.Risky && aoe.Check(actor.Position) && ArenaProjectionLayerParticipantApplies(actor, aoe.ArenaProjectionLayer, aoe.RestrictToArenaProjectionLayer))
            {
                hints.Add(WarningText);
                break;
            }
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in PreviewAOEs(pcSlot, pc))
        {
            using var layer = Arena.WorldProjectionLayer(aoe.ArenaProjectionLayer, aoe.RestrictToArenaProjectionLayer);
            aoe.Shape.Draw(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.Helper && !caster.IsAlly && !caster.IsDeadOrDestroyed && spell.IsSpell(AID.Burst))
        {
            Casters.RemoveAll(a => a.ActorID == caster.InstanceID);
            base.OnCastStarted(caster, spell);
            Casters.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
        }
    }

    public override void OnActorDeath(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(a => a.ActorID == actor.InstanceID);
}
class DragonBreath(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DragonBreath, new AOEShapeRect(30, 4));

class D074AiatarStates : StateMachineBuilder
{
    public D074AiatarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<SalivousSnap>()
            .ActivateOnEnter<ToxicVomit>()
            .ActivateOnEnter<Burst>()
            .ActivateOnEnter<DragonBreath>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "Malediktus", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 8, NameID = 1279)]
public class D074Aiatar(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    public static readonly ArenaBoundsCustom arena = new([new Circle(new(-26, -236), 19.5f)], [new Rectangle(new(-46.75f, -236), 2, 20)]);
}
