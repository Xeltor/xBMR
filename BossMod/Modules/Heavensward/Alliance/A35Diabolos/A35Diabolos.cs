namespace BossMod.Heavensward.Alliance.A35Diabolos;

class Nightmare(BossModule module) : Components.CastGaze(module, (uint)AID.Nightmare);
// Icon 62 precedes the instant hit by 5.30s in the recorded pull.
class NightTerror(BossModule module) : Components.StackWithIcon(module, (uint)IconID.Icon62, (uint)AID.NightTerror, 10, 5.2, 8)
{
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == StackAction)
            CastCounter = 0; // a previous late hit without an assignment cannot poison the next single-hit stack
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == StackAction)
            CastCounter = 0;
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == StackIcon)
        {
            if (Stacks.Any(s => s.Target == actor))
                return; // duplicate cues must not postpone the response deadline
            Stacks.RemoveAll(s => s.Target.InstanceID == actor.InstanceID);
        }
        base.OnEventIcon(actor, iconID, targetID);
    }

    public override void Update()
    {
        base.Update();
        Stacks.RemoveAll(s => s.Activation.AddSeconds(2) < WorldState.CurrentTime || s.Target.IsDestroyed);
    }
}
// Boss7184 is a non-damaging visual. The simultaneous helper7185 cast owns
// the actual raidwide deadline, about1s later; do not predict a second hit here.
class RuinousOmen1(BossModule module) : Components.CastHint(module, (uint)AID.RuinousOmen1, "Raidwide: Ruinous Omen (delayed impact)");
class RuinousOmen2(BossModule module) : Components.RaidwideCast(module, (uint)AID.RuinousOmen2);
class UltimateTerror(BossModule module) : Components.SimpleAOEs(module, (uint)AID.UltimateTerror, new AOEShapeDonut(5, 19.5f));

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 220, NameID = 5526)]
public class A35Diabolos(WorldState ws, Actor primary) : BossModule(ws, primary, new(-350, -445), new ArenaBoundsCircle(35))
{
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var firstSummon = hints.PotentialTargets
            .Where(e => e.Priority >= 0 && e.Actor.IsTargetable && !e.Actor.IsDeadOrDestroyed
                && e.Actor.OID == (uint)OID.Deathgate && e.Actor.CastInfo is { Action.ID: (uint)AID.VoidCall, RemainingTime: > 0 })
            .Select(e => e.Actor.CastInfo!.NPCRemainingTime).DefaultIfEmpty(float.MaxValue).Min();
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && enemy.Actor.IsTargetable && !enemy.Actor.IsDeadOrDestroyed)
            {
                if (enemy.Actor.OID == (uint)OID.Deathgate)
                {
                    var urgent = enemy.Actor.CastInfo is { Action.ID: (uint)AID.VoidCall, RemainingTime: > 0 } call
                        && call.NPCRemainingTime <= firstSummon + 0.05f;
                    enemy.Priority = Math.Max(enemy.Priority, urgent ? 3 : 2);
                }
                else if (enemy.Actor.OID == (uint)OID.Lifegate)
                    enemy.Priority = Math.Max(enemy.Priority, 1);
            }
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.Deathgate));
        Arena.Actors(Enemies((uint)OID.Lifegate));
    }
}
