namespace BossMod.Stormblood.Alliance.A11Mateus;

class IceAzerBlizzard(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Blizzard, "Ice Azer Blizzard incoming.",
    AIHints.PredictedDamageType.None)
{
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
        else
            Casters.Remove(actor);
    }
}

class HypothermalCombustion(BossModule module) : Components.SimpleAOEs(module, (uint)AID.HypothermalCombustion, 9.04f);
class DarkBlizzardIII(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID.DarkBlizzardIII, 5f);
class Frosting(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Frosting, 8f);
class Hydrowave(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Hydrowave, 4f);
class Chill(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Chill, Chill.Cone)
{
    internal static readonly AOEShapeCone Cone = new(41f, 10f.Degrees());
}
class WhiteWhisper(BossModule module) : Components.CastHint(module, (uint)AID.TheWhiteWhisper,
    "Destroy the Blizzard III pillar before White Whisper!", showCastTimeLeft: true)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (TetheredPillar(actor) != null)
            hints.Add("Destroy the Blizzard III pillar tethered to you.");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (TetheredPillar(pc) is { } pillar)
        {
            Arena.Actor(pillar, Colors.Object);
            Arena.AddLine(pc.Position, pillar.Position, Colors.Object);
        }
    }

    internal Actor? TetheredPillar(Actor actor)
        => !Module.PrimaryActor.IsDeadOrDestroyed && !actor.IsDeadOrDestroyed && actor.Tether.ID == (uint)TetherID.KiteTether
            && WorldState.Actors.Find(actor.Tether.Target) is { OID: (uint)OID.BlizzardIII, IsDeadOrDestroyed: false } pillar
            && Arena.InBounds(pillar.Position) ? pillar : null;

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Casters.Clear();
        else
            Casters.Remove(actor);
    }
}

class BlizzardIV(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BlizzardIV, 15f)
{
    // The six recorded impacts span 42 ms. Group close impacts without
    // merging a separately timed volley into the first mitigation window.
    private const double SameVolleyWindow = 0.25d;
    private DateTime _dualcastForecast;
    private DateTime _impactForecast;
    private DateTime Forecast => _impactForecast != default ? _impactForecast : _dualcastForecast;

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Casters.Count != 0)
            hints.Add("Blizzard IV incoming. Move away from the impact markers.");
        else if (Forecast != default)
            hints.Add("Blizzard IV next. Watch for the impact markers.");
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var casts = CollectionsMarshal.AsSpan(Casters);
        if (!casts.IsEmpty)
        {
            var firstVolleyEnd = casts[0].Activation.AddSeconds(SameVolleyWindow);
            foreach (ref var cast in casts)
                cast.Color = cast.Activation <= firstVolleyEnd ? Colors.Danger : Colors.AOE;
        }
        return casts;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Casters.Count == 0)
            return;
        var firstVolleyEnd = Casters[0].Activation.AddSeconds(SameVolleyWindow);
        foreach (var cast in Casters)
        {
            if (cast.Activation > firstVolleyEnd)
                break;
            if (cast.Check(actor.Position))
            {
                hints.Add("Move away from this Blizzard IV volley!");
                break;
            }
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Casters.Count == 0 && Forecast == default)
            return;
        var affected = Raid.WithSlot().Mask();
        if (Casters.Count > 0)
        {
            var firstVolleyEnd = Casters[0].Activation.AddSeconds(SameVolleyWindow);
            var origins = Casters.TakeWhile(cast => cast.Activation <= firstVolleyEnd).Select(cast => cast.Origin).ToArray();
            hints.GoalZones.Add(p => Math.Min(origins.Min(origin => (p - origin).Length()), 24f));

            if (affected != default)
            {
                var volleyEnd = DateTime.MinValue;
                foreach (var cast in Casters)
                {
                    if (cast.Activation <= volleyEnd)
                        continue;
                    hints.AddPredictedDamage(affected, cast.Activation);
                    volleyEnd = cast.Activation.AddSeconds(SameVolleyWindow);
                }
            }
        }
        else if (Forecast != default && affected != default)
        {
            hints.AddPredictedDamage(affected, Forecast);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (spell.Action.ID == WatchedAction)
        {
            _impactForecast = _dualcastForecast = default;
            Casters.RemoveAll(aoe => aoe.ActorID == caster.InstanceID);
        }
        else if (caster.OID == (uint)OID.Boss && Casters.Count == 0)
        {
            if (spell.Action.ID == (uint)AID.Dualcast)
                _impactForecast = WorldState.FutureTime(14.8d);
            else if (spell.Action.ID == (uint)AID.SummonBlizzard)
                _impactForecast = WorldState.FutureTime(11.8d);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID == WatchedAction)
        {
            _impactForecast = _dualcastForecast = default;
            SortHelpers.SortAOEByActivation(Casters);
        }
        else if (spell.Action.ID == (uint)AID.Dualcast && caster.OID == (uint)OID.Boss && Casters.Count == 0)
            _dualcastForecast = Module.CastFinishAt(spell, 14.8d);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (spell.Action.ID == (uint)AID.Dualcast && caster.OID == (uint)OID.Boss)
            _dualcastForecast = default;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
        {
            _impactForecast = _dualcastForecast = default;
            Casters.Clear();
        }
        else if (actor.OID == (uint)OID.BlizzardIV)
            Casters.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        if (now > _impactForecast.AddSeconds(0.75d))
            _impactForecast = default;
        if (now > _dualcastForecast.AddSeconds(0.75d))
            _dualcastForecast = default;
    }
}

class FlashFreeze(BossModule module) : Components.Cleave(module, (uint)AID.FlashFreeze, new AOEShapeCone(16.5f, 60f.Degrees()), [(uint)OID.Boss], activeWhileCasting: false);

// Fit the full arena with cells whose diagonal is smaller than a 0.7-yalm air bubble radius.
[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "The Combat Reborn Team (Malediktus, LTS)", GroupType = BossModuleInfo.GroupType.CFC, GroupID = 281, NameID = 6929)]
public class A11Mateus(WorldState ws, Actor primary) : BossModule(ws, primary, new(-320f, 240f), new ArenaBoundsSquare(29.5f, mapResolution: 59f / 124))
{
    public override string[] PrePullHints =>
    [
        "Kill the Ice Azers and step away from their explosions.",
        "Flash-freeze cleaves toward the tank between Mateus's casts.",
        "During Unbind, pop the water orbs and put three players in each spout.",
        "Aqua Spheres by alliance: A southwest, B north, C southeast.",
        "If Breathless builds up, enter an air bubble from a fallen Flume Toad. Keep Snowpierce away from bubbles.",
        "Take one Azure Guard per alliance and keep them apart. Group for Frostwave after the last guard falls.",
    ];

    private static readonly uint[] trash = [(uint)OID.IceAzer, (uint)OID.IceSlave, (uint)OID.FlumeToad, (uint)OID.AquaSphere, (uint)OID.AzureGuard, (uint)OID.BlizzardIII];

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(this, trash);
    }

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var group = Raid.AllianceGroupForSlot(slot);
        var stagingForSphere = FindComponent<AquaSpheres>()?.StagingPosition(slot) != null;
        var air = FindComponent<Froth>();
        var needsAirSource = air?.NeedsAirSource(actor) == true;
        var tetheredPillar = FindComponent<WhiteWhisper>()?.TetheredPillar(actor);
        var range = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
        var count = hints.PotentialTargets.Count;
        for (var i = 0; i < count; ++i)
        {
            var e = hints.PotentialTargets[i];
            if (e.Actor.OID == (uint)OID.AquaSphere)
            {
                if (CanEngagePhaseAdd(e, Arena))
                {
                    var assigned = group < 0 || AllianceSector(Arena.Center, e.Actor.Position) == group;
                    if (assigned || !stagingForSphere)
                        e.Priority = assigned ? 4 : 3;
                }
                continue;
            }
            if (e.Actor.OID == (uint)OID.BlizzardIII)
            {
                // The pillar can already be attacked before it acquires local enmity.
                if (CanEngagePhaseAdd(e, Arena))
                {
                    e.Priority = 5;
                    if (actor.Position.InCircle(e.Actor.Position, e.Actor.HitboxRadius + range))
                        e.Priority += e.Actor == tetheredPillar ? 2 : 1;
                }
                continue;
            }
            if (e.Actor.OID == (uint)OID.FlumeToad)
            {
                // These phase objectives can be targetable before the local party has enmity.
                if (CanEngagePhaseAdd(e, Arena))
                {
                    e.Priority = 4;
                    if (needsAirSource)
                        e.Priority = actor.Position.InCircle(e.Actor.Position, Froth.AirSourceApproachRadius(actor, e.Actor)) ? 9 : 8;
                }
                continue;
            }

            if (e.Actor.OID == (uint)OID.IceAzer)
            {
                if (CanEngagePhaseAdd(e, Arena))
                    e.Priority = 2;
                continue;
            }

            if (e.Priority < 0 || e.Actor.OID == (uint)OID.AzureGuard)
                continue;

            e.Priority = e.Actor.OID == (uint)OID.IceSlave ? 2 : 0;
        }

        if (needsAirSource || air?.RecoveryDeadline(actor, FindComponent<Snowpierce>()?.FirstActivation(actor) ?? default) != null)
            return;

        var highest = hints.PotentialTargets.Count > 0 ? hints.PotentialTargets.Max(enemy => enemy.Priority) : 0;
        if (hints.PotentialTargets.Any(enemy => enemy.Priority == highest && enemy.Actor.OID == (uint)OID.BlizzardIII && CanEngagePhaseAdd(enemy, Arena)))
        {
            var pillars = hints.PotentialTargets.Where(enemy => enemy.Actor.OID == (uint)OID.BlizzardIII && CanEngagePhaseAdd(enemy, Arena))
                .Select(enemy => (enemy.Actor.Position, Radius: enemy.Actor.HitboxRadius + range, Assigned: enemy.Actor == tetheredPillar)).ToArray();
            hints.GoalZones.Add(position =>
            {
                var score = 0f;
                foreach (var pillar in pillars)
                    if (position.InCircle(pillar.Position, pillar.Radius))
                        score = Math.Max(score, pillar.Assigned ? 10f : 8f);
                return score;
            });
            return;
        }
        if (highest is not (3 or 4))
            return;
        var spheres = hints.PotentialTargets.Where(enemy => enemy.Priority == highest && enemy.Actor.OID == (uint)OID.AquaSphere
            && CanEngagePhaseAdd(enemy, Arena)).Select(enemy => (enemy.Actor.Position, Radius: enemy.Actor.HitboxRadius + range)).ToArray();
        if (spheres.Length > 0)
            hints.GoalZones.Add(position => spheres.Any(sphere => position.InCircle(sphere.Position, sphere.Radius)) ? 6f : 0f);
    }

    internal static bool CanEngagePhaseAdd(AIHints.Enemy enemy, MiniArena arena) =>
        (enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable)
        && enemy.Actor.IsTargetable && !enemy.Actor.IsDeadOrDestroyed && arena.InBounds(enemy.Actor.Position);

    internal static int AllianceSector(WPos center, WPos position) => position.Z < center.Z ? 1 : position.X < center.X ? 0 : 2;
}
