using BossMod;
using BossMod.CombatAssistance.Planning;
using BossMod.Pathfinding;
using Lakelord = BossMod.Shadowbringers.Dungeon.D02DohnMheg.D031AencThon;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void IngressRecovery()
    {
        RecoveryDeferral();
        DohnMhegBridgeRecovery();
        var now = DateTime.UtcNow;
        var hints = new AIHints();
        Vector3? Flat(Vector3 p, Vector3 direction, float _) => direction.Y < -0.9f ? new(p.X, 0, p.Z) : null;
        bool Route(Func<Vector3, Vector3, float, Vector3?>? ray = null, Func<Vector3, bool>? loaded = null)
            => NativeIngressRecovery.Validate(default, new(6, 0), 0.5f, 6, hints, now, ray ?? Flat, loaded ?? (_ => true), out _);
        Check(Route(), "native entry accepts an observed supported corridor");
        Check(!Route(loaded: p => p.X < 4), "entry requires loaded collision along the entire route");
        Check(!Route((p, d, _) => d.Y < -0.9f && p.X is > 2 and < 4 ? null : Flat(p, d, 0)),
            "supported endpoints do not conceal a hole in the middle of entry");
        Check(!Route((p, d, _) => d.Y < -0.9f ? Flat(p, d, 0) : p.X > 3 ? p + d * 0.2f : null),
            "entry checks walls beyond the initial movement step");
        Check(!Route((_, _, _) => null), "missing current floor evidence withholds entry");
        hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, now.AddSeconds(0.5), default);
        Check(!Route(), "an upcoming authored movement prohibition applies before arena entry");
        hints.ImminentSpecialMode = (AIHints.SpecialMode.Pyretic, now, default);
        Check(!Route(), "active pyretic cannot be bypassed by being outside the arena");
        hints.ImminentSpecialMode = default;
        hints.ForbiddenZones.Add((new SDCircle(new(3, 0), 0.75f), now.AddSeconds(5), 1));
        Check(Route(), "entry may cross a warning when the complete route clears its deadline");
        hints.ForbiddenZones[0] = (new SDCircle(new(3, 0), 0.75f), now, 1);
        Check(!Route(), "active warning blocks the same corridor");
        hints.ForbiddenZones[0] = (new SDCircle(new(6, 0), 0.75f), now.AddSeconds(10), 1);
        Check(!Route(), "entry cannot stop in a future warning without onward escape");
        hints.ForbiddenZones.Clear();
        hints.TemporaryObstacles.Add(new SDCircle(new(3, 0), 0.75f));
        Check(!Route(), "authored temporary obstacles remain authoritative during entry");
        hints.TemporaryObstacles.Clear();
        hints.PathfindMapArenaProjectionLayer = 1;
        Check(!Route(), "unsupported projected entry never becomes a blind straight walk");
        hints.PathfindMapArenaProjectionLayer = null;
        hints.Teleporters.Add(new(new(3, 0), new(6, 0), 1, false));
        Check(!Route(), "entry does not mistake an authored teleporter for ordinary ground");
    }

    private static void DohnMhegBridgeRecovery()
    {
        // Recorded at the last bend, with the boss untargetable and no action
        // selected while xBMR kept deferring the native route toward the lyre.
        var (world, player, module) = ProjectionFixture();
        player.PosRot = new(-115.26202f, -144.53662f, -244.64418f, 2.9939642f);
        var lyre = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        lyre.InstanceID = 2;
        lyre.OID = (uint)Lakelord.OID.LiarsLyre;
        lyre.IsTargetable = true;
        lyre.PosRot = new(-128.5268f, -144.5215f, -259.02197f, 0);
        typeof(BossModule).GetField(nameof(BossModule.RelevantEnemies))!.SetValue(module,
            new Dictionary<uint, List<Actor>> { [(uint)Lakelord.OID.LiarsLyre] = [lyre] });
        module.Arena.Center = Lakelord.D033AencThon.BuildArena().center;
        var bridge = new Lakelord.FunambulistsFantasia(module);
        var hints = new AIHints();
        bridge.OnCastFinished(lyre, new ActorCastInfo { Action = ActionID.MakeSpell(Lakelord.AID.FunambulistsFantasia) });
        bridge.AddAIHints(0, player, default, hints);
        Check(hints.UseNativeNavigation && hints.ForbidDashes && hints.GoalZones.Count == 1, "the bridge keeps the lyre goal and forbids player dashes");

        var identity = new NavigationIdentity(1, world.CurrentTime, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
            player.Position, false, 6, 0, null, player.InstanceID, 821, 649);
        var input = new NavigationDecision.CapturedInput(new(module.Bounds, module.Center, null, default), [],
            [], NavigationDecision.CaptureGoals(hints), true, [], identity, true);
        var native = NativeNavigationSnapshot.Capture(input);
        var intent = new MovementIntent();
        var steerNatively = NativeMovementHost.NativeSteeringReason(hints, intent, null, world.CurrentTime) != null;
        Check(steerNatively && NativeMovementHost.NativeSteeringReason(hints, intent with { ExplicitDestination = player.Position },
            null, world.CurrentTime) == null, "the bridge uses native steering under the existing movement authority");
        var context = new MovementPlanningContext(identity, native.CopyStrictMap(), 0, 0,
            Enumerable.Repeat(true, native.Cells).ToArray(), native.CopyPresentation(), [], [], native: native, optimize: !steerNatively);
        var recovery = NativeMovementSearch.Recover(context, new());
        var publication = recovery.NativeRecovery!.Value.Publication!;
        var executor = new MovementPlanExecutor();
        var step = executor.AdvanceRecovery(recovery, identity, true, native.CanExecuteRecovery(publication, player.Position),
            timestamp: identity.CapturedTimestamp);
        Check(publication is { NativeMaxGoalReached: true, WitnessComplete: true }
            && recovery.UsefulDestination.InCircle(lyre.Position, 1) && !NativeMovementHost.CanDeferRecovery(context, recovery)
            && step.Kind == MovementExecutionKind.Walk && step.Waypoint == publication.Destination,
            "native recovery follows the recorded bridge route to the lyre without waiting for attack planning");
        Check(executor.AdvanceRecovery(recovery, identity, false, true, timestamp: identity.CapturedTimestamp)
            .Kind == MovementExecutionKind.Invalidated, "bridge navigation yields to manual movement authority");
        bridge.OnCastFinished(lyre, new ActorCastInfo { Action = ActionID.MakeSpell(Lakelord.AID.Finale) });
        hints = new();
        bridge.AddAIHints(0, player, default, hints);
        Check(!hints.UseNativeNavigation && !hints.ForbidDashes && hints.GoalZones.Count == 0, "Finale ending clears the bridge-specific instructions");
    }

    private static void RecoveryDeferral()
    {
        // Match both reported arena types: an origin beyond the rectangular map,
        // and an origin inside the map rectangle but outside the circular arena.
        foreach (var (bounds, origin) in new (ArenaBounds, WPos)[]
        {
            (new ArenaBoundsSquare(20), new(0, 21)),
            (new ArenaBoundsCircle(15), new(11, 11))
        })
        {
            var identity = new NavigationIdentity(1, DateTime.UtcNow, DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(),
                origin, false, 6, 0, null, 7, 1, 1);
            var input = new NavigationDecision.CapturedInput(new(bounds, default, null, default), [], [],
                new([]), true, [], identity, true);
            var native = NativeNavigationSnapshot.Capture(input);
            MovementPlanningContext ContextAt(NativeNavigationSnapshot snapshot)
                => new(snapshot.Identity, snapshot.CopyStrictMap(), 0, 0,
                    Enumerable.Repeat(true, snapshot.Cells).ToArray(), new float[snapshot.Cells], [], [], native: snapshot);
            var context = ContextAt(native);
            var recovery = NativeMovementSearch.Recover(context, new());
            Check(!NativeMovementHost.CanDeferRecovery(context, recovery),
                "An outside origin cannot borrow positive leeway from the clamped native map to wait for optimization.");
            var inside = NativeNavigationSnapshot.Capture(input with { Identity = identity with { Origin = default } });
            var insideContext = ContextAt(inside);
            Check(NativeMovementHost.CanDeferRecovery(insideContext, NativeMovementSearch.Recover(insideContext, new())),
                "An actually safe origin retains its optional optimization and casting window.");
            var blocked = NativeNavigationSnapshot.Capture(input with
            {
                Identity = identity with { Origin = default }, Obstacles = [new SDCircle(default, 2)]
            });
            var blockedContext = ContextAt(blocked);
            var positiveButBlocked = NativeMovementSearch.Recover(blockedContext, new());
            positiveButBlocked = positiveButBlocked with
            { NativeRecovery = new NavigationDecision { LeewaySeconds = float.MaxValue } };
            Check(!NativeMovementHost.CanDeferRecovery(blockedContext, positiveButBlocked),
                "Positive recovery leeway alone cannot authorize waiting in blocked physical geometry.");
        }
    }
}
