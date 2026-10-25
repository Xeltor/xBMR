using BossMod;
using BossMod.CombatAssistance.Planning;

public static partial class Checks
{
    private static void PartyContact()
    {
        FastPullContact();
        var policy = new NativePartyContact();
        var now = DateTime.UtcNow;
        var observation = new PartyContactObservation(now, 7, new(10, 0), 2, true, false, true, false, true);
        Check(policy.Observe(observation, default, 6) == null, "A distant tank alone is not evidence of an advancing pull.");
        observation = observation with { Now = now.AddSeconds(0.5), Position = new(13, 0) };
        Check(policy.Observe(observation, default, 6) is { TargetId: 7, Radius: 6 },
            "An observed advancing multi-enemy pull establishes a useful contact region.");
        Check(policy.Observe(observation with { Now = now.AddSeconds(1), Position = new(13, 0) }, new(7, 0), 6) == null,
            "Reaching the contact radius stops recovery without an extra centering walk.");
        Check(policy.Observe(observation with { Now = now.AddSeconds(1.1), Position = new(13.6f, 0) }, new(7, 0), 6) == null,
            "Small changes inside the restart margin do not cause repeated correction moves.");
        Check(policy.Observe(observation with { Now = now.AddSeconds(1.2), Allowed = false }, default, 6) == null,
            "Manual or native movement-authority loss clears contact recovery.");
        Check(policy.Observe(observation with { Now = now.AddSeconds(2), Position = new(50, 0) }, default, 6) == null,
            "Regaining authority cannot reuse motion observed before handoff.");
        Check(policy.Observe(observation with { Now = now.AddSeconds(2.1), Position = new(80, 0) }, default, 6) == null,
            "A tank teleport does not establish advancing-pull movement.");
        Check(policy.Observe(observation with { BossActive = true }, default, 6) == null,
            "An authored active boss excludes dungeon contact recovery.");
        Check(policy.Observe(observation with { PlayerTank = true }, default, 6) == null,
            "Tank players do not chase a second tank through this policy.");
        Check(policy.Observe(observation with { Position = new(float.NaN, 0) }, default, 6) == null,
            "Invalid observation geometry cannot enter planning.");

        policy.Reset();
        Check(policy.Observe(observation with { Now = now.AddSeconds(4), TankInCombat = false }, default, 6) == null,
            "An idle tank never establishes automatic between-pull following.");
        var gates = new PartyContactAuthority(true, true, true, true, true);
        Check(gates.Allowed, "Opted-in combat pull contact participates within native movement authority.");
        Check(!(gates with { SelfLeader = false }).Allowed,
            "A saved or runtime explicit follow leader excludes automatic tank contact.");
        Check(!(gates with { NativeAuthority = false }).Allowed && !(gates with { Enabled = false }).Allowed
            && !(gates with { MaintainPull = false }).Allowed,
            "Manual handoff, disabled assistance, saved opt-outs and native prohibitions clear combat contact.");
        Check(!(gates with { InCombat = false }).Allowed,
            "Combat contact cannot start or continue following between pulls.");

        policy.Reset();
        var oldZone = observation with { Zone = 1, Player = 1, Now = now.AddSeconds(6) };
        policy.Observe(oldZone, default, 6);
        Check(policy.Observe(oldZone with { Now = now.AddSeconds(6.5), Position = new(16, 0) }, default, 6) != null,
            "A fresh pull establishes movement evidence within its actor and zone.");
        Check(policy.Observe(oldZone with { Zone = 2, Now = now.AddSeconds(6.6), Position = new(50, 0) }, default, 6) == null,
            "A zone change cannot reuse recent pull movement even when the tank identity remains the same.");
        Check(policy.Observe(oldZone with { Zone = 2, Now = now.AddSeconds(7.1), Position = new(53, 0) }, default, 6) != null,
            "Movement observed after zoning may independently establish a new pull.");
        Check(policy.Observe(oldZone with { Zone = 2, Player = 2, Now = now.AddSeconds(7.2), Position = new(53, 0) }, default, 6) == null,
            "A player identity change cannot inherit another actor's contact episode.");

        var map = Grid(5, 1);
        var origin = Context(map, 0);
        var contact = new PlanningRange(7, Point(map, 4), 1);
        MovementPlanningContext Capture(PlanningAction[] actions, PlanningRange? region = null)
            => new(origin.Identity, map, 0, 0, [true, true, true, true, true], new float[5], actions, [], partyContact: region ?? contact);
        var result = JointMovementSearch.Search(Capture([]), Exhaustive);
        Check(result.UsefulDestination == Point(map, 3) && result.PartyContact == contact,
            "Contact is selected within the joint search and stops at the useful boundary.");
        var action = Action(map, 0.25f, 1, [1, 0, 0, 0, 0], [1, 0, 0, 0, 0]);
        result = JointMovementSearch.Search(Capture([action]), Exhaustive);
        Check(result.DeliveredActions == 1 && result.DeliveryTimes[0] == 0,
            "Closing party distance cannot sacrifice a selected action or its required recipient.");
        var contactPlan = JointMovementSearch.Search(Capture([]), Exhaustive);
        var stopped = new MovementPlanningContext(origin.Identity, map, 0, 0,
            [true, true, true, true, true], new float[5], [], [],
            committedDestination: NativeMovementHost.ContactContinuity(contact, null, origin.Origin, contactPlan.UsefulDestination));
        Check(!MovementPlanValidation.Validate(contactPlan, stopped, 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "Removing a contact objective immediately invalidates its previous committed route.");
        Check(JointMovementSearch.Search(stopped, Exhaustive).UsefulDestination == origin.Origin,
            "Ending combat contact anchors continuity at the current pose instead of finishing the retired contact walk.");
        var otherContact = contact with { TargetId = 8 };
        Check(NativeMovementHost.RetiredContact(contact, otherContact)
            && NativeMovementHost.ContactContinuity(contact, otherContact, origin.Origin, contactPlan.UsefulDestination) == origin.Origin,
            "A different tank cannot inherit the previous contact owner's committed destination.");
        Check(!NativeMovementHost.RetiredContact(contact, contact with { Center = Point(map, 3) })
            && NativeMovementHost.ContactContinuity(contact, contact with { Center = Point(map, 3) }, origin.Origin, contactPlan.UsefulDestination)
                == contactPlan.UsefulDestination,
            "Ordinary movement of the same observed tank retains continuity while the joint search updates the region.");
        map.PixelMaxG[3] = 0;
        map.PixelMaxG[4] = 0;
        result = JointMovementSearch.Search(Capture([]), Exhaustive);
        Check(result.UsefulDestination == Point(map, 2), "Party contact cannot outweigh native safety or disconnected geometry.");
        try
        {
            Capture([], contact with { Radius = float.NaN });
            Check(false, "Invalid contact evidence must reject capture.");
        }
        catch (ArgumentException) { Check(true, "Invalid contact evidence rejects capture."); }
    }

    private static void FastPullContact()
    {
        var map = Grid(41, 1);
        var origin = Context(map, 0, speed: 6);
        var contact = new PlanningRange(7, Point(map, 40), 10);
        MovementPlanningContext Input(ulong charges, PlanningAction[]? actions = null)
            => new(origin.Identity, map, 0, 0, Enumerable.Repeat(true, 41).ToArray(), new float[41], actions ?? [], [], charges,
                partyContact: contact, mobilityMechanics: [new(155, MobilityMechanic.Target, 30, 0, 0, 0, .2f, 0,
                    [new(7, Point(map, 30), 0)])],
                utilities: [new(3, PlanningUtilityEffect.MovementSpeed, 10, .6f, 0, 1, 7.8f)]);
        MovementPlan Fast(MovementPlanningContext input)
            => JointMovementSearch.Search(input, Exhaustive with { Expansions = 128 },
                stopAtCurrentOpportunity: true, includeSpatialMobilityActions: false);
        var walk = Fast(Input(0));
        var sprint = Fast(Input(0x10));
        var relayInput = Input(1);
        var relay = Fast(relayInput);
        Check(contact.Loss(walk.UsefulDestination) == 0 && contact.Loss(sprint.UsefulDestination) == 0
            && contact.Loss(relay.UsefulDestination) == 0 && sprint.DurationSeconds < walk.DurationSeconds
            && relay.DurationSeconds < sprint.DurationSeconds && relay.Stages.Any(s => s.ActionId == 155),
            "An advancing tank receives prompt walking, Sprint and friendly-relay alternatives compared by useful arrival, rather than always conserving mobility while falling behind.");
        Check(MovementPlanValidation.Validate(relay, relayInput, 0, true, origin.Identity.CapturedTimestamp).Allowed,
            "Pull pursuit retains complete route and real anchor validation.");
        var alreadyInAttackRange = new MovementPlanningContext(origin.Identity, map, 0, 0,
            Enumerable.Repeat(true, 41).ToArray(), new float[41], [], [],
            range: new(10, origin.Origin, 25, RecoverExecution: true), partyContact: contact);
        Check(Fast(alreadyInAttackRange).UsefulDestination != origin.Origin,
            "An already available ranged attack cannot terminate the initial search before advancing-pull contact is considered.");
        var cast = Action(map, 10, 0, new ulong[41], Enumerable.Repeat(1f, 41).ToArray(), cast: 1);
        var castInput = Input(0x11, [cast]);
        var castThenFollow = Fast(castInput);
        Check(castThenFollow.DeliveredActions == 1 && castThenFollow.DeliveryTimes[0] == 1
            && contact.Loss(castThenFollow.UsefulDestination) == 0,
            "Following the pull preserves the current cast and its recipients before closing tank distance.");
        var shifted = new MovementPlanningContext(origin.Identity, map, 0, 0, Enumerable.Repeat(true, 41).ToArray(),
            new float[41], [], [], partyContact: contact with { Center = new(200, .5f) });
        var progress = Fast(shifted);
        Check(progress.HasVerifiedRoute && progress.UsefulDestination.X > origin.Origin.X,
            "A tank beyond the local map still yields safe forward progress instead of waiting for complete contact.");
    }
}
