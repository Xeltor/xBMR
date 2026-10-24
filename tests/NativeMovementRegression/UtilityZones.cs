using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void UtilityZones()
    {
        var map = Grid(5, 1);
        var baseline = Context(map, 0);
        var zone = new PlanningZone(3573, 1, "Ley Lines", baseline.Point(4), 0, 0.4f, 10, PlanningZoneBenefit.PersonalCasting);
        MovementPlanningContext Input(PlanningZone[] zones, PlanningAction[]? actions = null, bool[]? allowed = null,
            PlanningMobility[]? mobility = null, ulong resources = 0, float? delay = null)
            => new(baseline.Identity, map, 0, 0, allowed ?? [true, true, true, true, true], [0, 0, 0, 0, 0], actions ?? [],
                mobility ?? [], resources, committedDestination: baseline.Origin, zones: zones, lateDepartureMargin: delay);
        var context = Input([zone]);
        var plan = JointMovementSearch.Search(context, Exhaustive);
        Check(plan.UsefulDestination == zone.Center && plan.UsefulZones.Length == 1,
            "active utility benefit participates before first destination despite prior cosmetic continuity");
        var expired = Input([zone with { EndsIn = 2 }]);
        Check(JointMovementSearch.Search(expired, Exhaustive).UsefulDestination == baseline.Origin,
            "a zone that expires before walking arrival cannot attract movement");
        Check(!MovementPlanValidation.Validate(plan, expired, 0, true, context.Identity.CapturedTimestamp).Allowed,
            "exact executable validation rejects a vanished friendly benefit");
        var assigned = JointMovementSearch.Search(Input([zone], allowed: [true, true, true, true, false]), Exhaustive);
        Check(assigned.UsefulDestination != zone.Center && assigned.UsefulZones.IsEmpty,
            "utility zones cannot override independent destination assignments");
        var action = Action(map, 1, 1, [1, 0, 0, 0, 0], [1, 0, 0, 0, 0], cast: 1);
        var castFirst = JointMovementSearch.Search(Input([zone], [action]), Exhaustive);
        Check(castFirst.DeliveredActions == 1 && castFirst.DeliveryTimes[0] == 1 && castFirst.Stages[0].Kind == MovementStageKind.Cast
            && castFirst.UsefulDestination == zone.Center,
            "current attack is delivered without delay before useful zone recovery in one plan");
        var walkingPass = JointMovementSearch.Search(Input([zone], [action]), Exhaustive, includeMobilityActions: false, stopAtCurrentOpportunity: true);
        Check(walkingPass.DeliveredActions == castFirst.DeliveredActions && walkingPass.DeliveryTimes.SequenceEqual(castFirst.DeliveryTimes)
            && walkingPass.UsefulDestination == zone.Center && walkingPass.UsefulZones.Length == 1,
            "The first walking publication includes after-action field return without delaying the current spell or requiring a correction walk.");
        var charge = new PlanningMobility(0, 4, MovementStageKind.TargetDash, 100, 9, null, 0, 0.1f, 0, 0);
        var preserved = JointMovementSearch.Search(Input([zone with { EndsIn = 2 }], mobility: [charge], resources: 1), Exhaustive);
        Check(preserved.RemainingResources == 1 && preserved.UsefulDestination == baseline.Origin,
            "an unevidenced future cast benefit cannot spend a mobility charge");
        map.PixelMaxG[4] = 1;
        Check(JointMovementSearch.Search(Input([zone]), Exhaustive).UsefulDestination != zone.Center,
            "friendly ground effects do not make a future hazard safe");
        map.PixelMaxG[4] = float.MaxValue;
        map.PixelMaxG[0] = 9;
        var held = JointMovementSearch.Search(Input([zone with { EndsIn = 5 }], delay: 0.1f), Exhaustive);
        Check(held.DurationSeconds < 5 && !held.Stages.Any(s => s.Kind == MovementStageKind.Wait),
            "late departure cannot erase the zone benefit used to select the destination");
        var passage = zone with { Center = default, Radius = 8, Facing = 180.Degrees(), HalfAngle = 60.Degrees().Rad };
        Check(passage.Contains(new(0, -4), 1) && !passage.Contains(new(0, 4), 1)
            && !passage.Contains(new(7, 0), 1) && !passage.Contains(default, 1),
            "Passage uses the actual rear cone instead of the paladin center or a circular approximation");
        var later = zone with { EndsIn = 9.5f };
        Check(later.Same(zone, 0.5f) && !zone.Same(zone, 0.5f),
            "countdown observations remain equivalent but cannot renew the original effect lifetime");
        var source = new[] { zone };
        var frozen = Input(source);
        source[0] = zone with { Center = default };
        Check(frozen.Zones[0] == zone, "worker zone evidence is frozen independently of mutable capture buffers");
        map.PixelMaxG[0] = float.MaxValue;
        var point = zone with { Center = zone.Center + new WDir(0.1f, 0.1f), Radius = 0, CompleteFootprint = false };
        var limited = JointMovementSearch.Search(Input([point]), Exhaustive);
        Check(limited.UsefulDestination == point.Center && limited.Bound == MovementSearchBound.FriendlyEffectGeometry,
            "an observed exact point remains searchable without inventing the unverified full buff footprint");
        UtilityZoneObservations();
    }

    private static void UtilityZoneObservations()
    {
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        var actors = new ActorState();
        typeof(WorldState).GetField(nameof(WorldState.Actors))!.SetValue(world, actors);
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(actors));
        world.Frame.Timestamp = DateTime.UtcNow;
        var player = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        player.InstanceID = 1;
        player.Type = ActorType.Player;
        player.Class = Class.BLM;
        player.Level = 100;
        player.HPMP = new(50, 100, 0, 10000, 10000);
        player.InCombat = true;
        player.Statuses = new ActorStatus[Actor.NumStatuses];
        world.Party.Members[0] = new(1, 1, false);
        actors.Actors.Add(1, player);
        actors.Added.Fire(player);
        var settings = new BossMod.CombatAssistance.Config.AssistancePreferences();
        var hints = new AIHints();
        using var zones = new NativeUtilityZones(world);
        void Cast(uint id, System.Numerics.Vector3 point = default)
            => actors.CastEvent.Fire(player, new(new(ActionType.Spell, id), 1, 0.6f, 1, point, 1, 1, default));
        Cast(3573);
        Check(zones.Capture(player, hints, settings, true, null).Single().EndsIn == 20,
            "Ley Lines placement uses current twenty-second lifetime and preserves a valid zero coordinate");
        world.Frame.Timestamp = world.CurrentTime.AddSeconds(5);
        player.PosRot = new(10, 0, 10, 0);
        Cast(36988);
        var moved = zones.Capture(player, hints, settings, true, null).Single();
        Check(moved.Center == new WPos(10, 10) && moved.EndsIn == 15, "Retrace relocates the actual center without renewing expiry");
        var extended = new ObservedLeyLines(1, 10, new(10, 10), 0, world.CurrentTime.AddSeconds(20));
        Check(zones.Capture(player, hints, settings, true, extended).Single().EndsIn == 15,
            "Ground recovery cannot extend the original observed placement lifetime through status expiry jitter.");
        settings.ReturnToLeylines = false;
        Check(zones.Capture(player, hints, settings, true, null).IsEmpty, "abilities-only Ley Lines does not add a walking-zone objective");
        Cast(7439, new(4, 0, 4));
        Check(zones.Capture(player, hints, settings, true, null).Any(z => z.ActionId == 7439), "observed Earthly Star supports an injured party recipient");
        Cast(8324);
        Check(!zones.Capture(player, hints, settings, true, null).Any(z => z.ActionId == 7439), "early detonation retires the star immediately");
        Cast(188, new(5, 0, 5));
        Check(zones.Capture(player, hints, settings, false, null).IsEmpty, "manual and activation gates hide every utility movement preference");
        world.CurrentZone = 2;
        Check(zones.Capture(player, hints, settings, true, null).IsEmpty, "zone changes discard previous ground effects");
    }
}
