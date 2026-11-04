namespace BossMod.Stormblood.Alliance.A14Argath;

class Soulfix(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Soulfix, 12f);
class ColdbloodDeparture(BossModule module) : BossComponent(module)
{
    private const float StagingRadius = 18f;
    private DateTime _prepareUntil;
    private bool Preparing => _prepareUntil > WorldState.CurrentTime;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.ColdbloodDeparture))
            _prepareUntil = WorldState.FutureTime(2.75d);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is (uint)AID.Coldblood or (uint)AID.Coldblood2)
            _prepareUntil = default;
    }

    public override void OnActorTargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _prepareUntil = default;
    }

    public override void OnActorDeath(Actor actor) => OnActorTargetable(actor);
    public override void OnActorDestroyed(Actor actor) => OnActorTargetable(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Preparing)
            hints.Add(GnawingDread.IsMisdirected(actor, WorldState.CurrentTime)
                ? "Coldblood incoming. Watch where Argath lands and get ready to time the arrow."
                : "Coldblood incoming. Step away from the rim and watch where Argath lands.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Preparing && !GnawingDread.IsMisdirected(actor, WorldState.CurrentTime) && !actor.Position.InCircle(Arena.Center, StagingRadius))
            hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, StagingRadius, 4f));
    }
}

class ColdbloodProximity(BossModule module) : Components.ProximityAOEs(module, (uint)AID.Coldblood2, 20f)
{
    private bool AfterHeartless(DateTime activation) => Module.FindComponent<Heartless>()?.ResolvesBefore(activation) == true;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var casts = CollectionsMarshal.AsSpan(Casters);
        foreach (ref var cast in casts)
            cast.Color = AfterHeartless(cast.Activation) ? Colors.AOE : Colors.Danger;
        return casts;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Casters.Any(cast => AfterHeartless(cast.Activation)))
            hints.Add("Dodge the sword crosses, then move away from Coldblood.");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Casters.Any(cast => !AfterHeartless(cast.Activation) && cast.Check(actor.Position)))
            hints.Add("Move away from the Coldblood impact!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var aoe in ActiveAOEs(slot, actor))
        {
            var origin = aoe.Origin;
            hints.GoalZones.Add(p => Math.Min((p - origin).Length(), 40f));
        }
    }
}
class ColdbloodDonut(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Coldblood, new AOEShapeDonut(SafeRadius, 66f))
{
    private const float SafeRadius = 5f;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        foreach (var aoe in ActiveAOEs(slot, actor))
            hints.GoalZones.Add(AIHints.GoalSingleTarget(aoe.Origin, SafeRadius - 0.5f, 8f));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
        {
            if (aoe.Check(actor.Position))
            {
                hints.Add(GnawingDread.IsMisdirected(actor, WorldState.CurrentTime)
                    ? "Wait for the arrow to point into the safe circle, then move!"
                    : "Move into the Coldblood safe circle!");
                break;
            }
        }
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => Casters.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
}

class GnawingDread(BossModule module) : Components.CastHint(module, (uint)AID.GnawingDread, "Move inward before the spinning arrow.")
{
    private const float PreparationRadius = 8f;
    private WPos PreparationCenter => Arena.Center + (Casters[0].Position - Arena.Center) * 0.5f;

    internal static bool IsMisdirected(Actor actor, DateTime now)
        => actor.FindStatus((uint)SID.TemporaryMisdirection) is { } status && status.ExpireAt > now;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (actor.IsDeadOrDestroyed)
            return;

        if (IsMisdirected(actor, WorldState.CurrentTime))
            hints.AddSpecialMode(AIHints.SpecialMode.Misdirection, default);
        else if (Active)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(PreparationCenter, PreparationRadius, 8f));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (Active && !IsMisdirected(pc, WorldState.CurrentTime))
            Arena.ZoneCircleOutline(PreparationCenter, PreparationRadius, Colors.Safe);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.Remove(actor);
    public override void OnActorDeath(Actor actor) => Casters.Remove(actor);
}

class CrushWeapon(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CrushWeapon2, 6f);
class CripplingBlow(BossModule module) : Components.SingleTargetCast(module, (uint)AID.CripplingBlow);
class Raidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.FireIV, (uint)AID.UnholySacrifice])
{
    public override void OnActorDestroyed(Actor actor) => Casters.Remove(actor);
    public override void OnActorDeath(Actor actor) => Casters.Remove(actor);
}
class Trepidation(BossModule module) : Components.CastHint(module, (uint)AID.Trepidation, "Soak one meteor per player!");

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team, Xel", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6925)]
public class A14Argath(WorldState ws, Actor primary) : BossModule(ws, primary, new(106f, -386f), InnerPlatform)
{
    internal const float InnerHalfWidth = 24f;
    internal static readonly ArenaBoundsSquare InnerPlatform = new(InnerHalfWidth);
    internal static readonly ArenaBoundsSquare FullPlatform = new(26f);

    public override string[] PrePullHints =>
    [
        "The white mask means follow the command. The dark mask reverses it.",
        "Soak one meteor per player. If marked with a sword, bait your cross near the middle.",
        "Avoid the bleeding floor squares during Judgment Blade.",
        "Destroy Shades and Shards. During Gnawing Dread, time movement with the rotating arrow and avoid the outer rim.",
    ];

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        List<(WPos Position, float Range)>? approaches = null;
        var highest = 0;
        var attackRange = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if ((enemy.Priority < 0 && enemy.Priority != AIHints.Enemy.PriorityUndesirable)
                || !target.IsTargetable || target.IsDeadOrDestroyed || !Arena.InBounds(target.Position))
                continue;

            var priority = target.OID switch
            {
                (uint)OID.ShardOfEmptiness => 4,
                (uint)OID.Shade when target.CastInfo is { EventHappened: false } cast && cast.IsSpell(AID.UnholySacrifice) => 6,
                (uint)OID.Shade => 2,
                _ => 0,
            };
            if (priority == 0)
                continue;

            enemy.Priority = priority;

            var range = target.HitboxRadius + actor.HitboxRadius + attackRange;
            if (actor.Position.InCircle(target.Position, range))
                ++enemy.Priority;
            if (priority < highest)
                continue;

            if (priority > highest)
            {
                approaches?.Clear();
                highest = priority;
            }
            (approaches ??= []).Add((target.Position, range));
        }

        if (approaches is { Count: > 0 })
            hints.GoalZones.Add(position => approaches.Any(approach => position.InCircle(approach.Position, approach.Range)) ? 6f : 0f);
    }
}
