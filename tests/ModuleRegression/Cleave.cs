using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;
using Titan = BossMod.RealmReborn.Trial.T07TitanH;

static partial class Regression
{
    // Nominal component/planner contracts, not physical cleave measurements or live movement proof.
    private sealed class CleaveFixture : IDisposable
    {
        public readonly WorldState World = new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        public readonly Actor Boss, Tank, Ally;
        public readonly Titan.T07TitanH Module;
        public CleaveFixture(WPos allyPosition)
        {
            Boss = Create(1, (uint)Titan.OID.Boss, default);
            Boss.HitboxRadius = 5.25f;
            Tank = Create(100, 0, new(0, 7), ActorType.Player, Class.GNB);
            Ally = Create(101, 0, allyPosition, ActorType.Player, Class.WHM);
            World.Execute(new PartyState.OpModify(0, new(0, Tank.InstanceID, false)));
            World.Execute(new PartyState.OpModify(1, new(0, Ally.InstanceID, false)));
            World.Execute(new ActorState.OpTarget(Boss.InstanceID, Tank.InstanceID));
            World.Execute(new ActorState.OpCombat(Boss.InstanceID, true));
            Module = new(World, Boss);
            Module.Update();
        }
        public Actor Create(ulong id, uint oid, WPos position, ActorType type = ActorType.Enemy, Class job = Class.None)
        {
            World.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, job, 50,
                new Vector4(position.X, 0, position.Z, 0), .5f, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return World.Actors.Find(id)!;
        }
        public Cleave Cleave => Module.Components.OfType<Cleave>().Single();
        public AIHints Hints(Cleave? component = null, Actor? actor = null)
        {
            var hints = new AIHints { PathfindMapCenter = Module.Center, PathfindMapBounds = Module.Bounds };
            (component ?? Cleave).AddAIHints(0, actor ?? Tank, default, hints);
            return hints;
        }
        public void Dispose() => Module.Dispose();
    }

    private static void CleaveReach()
    {
        using var f = new CleaveFixture(new(0, 40.3f));
        Check(f.Cleave.Shape is AOEShapeCone { Radius: 21.25f } cone && Math.Abs(cone.HalfAngle.Rad - MathF.PI / 3) < 1e-6,
            "Titan Hard retains its modeled 21.25y/60-degree half-angle cleave");
        Check(f.Hints().ForbiddenZones.Count == 0, "40.3y ally cannot constrain Titan's current tank");
        var text = new BossComponent.TextHints();
        f.Cleave.AddHints(0, f.Tank, text);
        Check(text.Count == 0, "cleave recipient is not warned to flee their own cleave");
        foreach (var range in new[] { 21.24f, 21.25f, 21.26f, 40.3f })
        {
            f.Ally.PosRot.Z = range;
            Check(f.Hints().ForbiddenZones.Count == (range <= 21.25f ? 1 : 0), "point victim reach includes the boundary and excludes beyond it");
        }
        f.Ally.PosRot.Z = 40.3f;
        var hints = f.Hints();
        hints.GoalZones.Add(AIHints.GoalSingleTarget(f.Boss, Positional.Any, 2.6f));
        var navigation = NavigationDecision.Build(new(), Now, hints, f.Tank);
        Check(navigation.Destination == null || (navigation.Destination.Value - f.Tank.Position).Length() <= 1,
            "native navigation holds an already valid melee tank position with only a distant ally");
        // Selected positions from capture ef8372fe..., ordinal 0. This replays the
        // component input only; it does not recreate the full historical game frame.
        f.Boss.PosRot.Z = -16;
        f.Tank.PosRot.X = -1.5375499f; f.Tank.PosRot.Z = 6.6301455f;
        f.Ally.PosRot.X = .339f; f.Ally.PosRot.Z = 24.274f;
        Check(f.Hints().ForbiddenZones.Count == 0, "captured 40.275y party position cannot constrain the recipient's aim");
    }

    private static void CleaveAim()
    {
        using var f = new CleaveFixture(new(0, 15));
        var hints = f.Hints();
        Check(hints.ForbiddenZones.Count == 1, "reachable ally creates one recipient aiming restriction");
        var zone = hints.ForbiddenZones[0].shapeDistance;
        foreach (var range in new[] { 5f, 21.26f, 30f, 1000f })
        {
            Check(zone.Distance(new(0, range)) < 0, "backing away on the same aim cannot satisfy ally protection");
            Check(zone.Distance(new(0, -range)) > 0, "aiming away from the ally remains valid at every range");
        }
        Check(zone.ConservativeLipschitz is > 0 and < 1.001f, "angular restriction supports conservative swept route checks");
        var inside = 59.9f.Degrees().ToDirection() * 7;
        var outside = 60.1f.Degrees().ToDirection() * 7;
        Check(zone.Distance(default(WPos) + inside) < 0 && zone.Distance(default(WPos) + outside) > 0,
            "recipient aim preserves the modeled angular boundary");
        hints.GoalZones.Add(AIHints.GoalSingleTarget(f.Boss, Positional.Any, 2.6f));
        var navigation = NavigationDecision.Build(new(), Now, hints, f.Tank);
        Check(navigation.Destination is { } destination && zone.Distance(destination) > 0 && (destination - f.Boss.Position).Length() < 10,
            "native navigation corrects aim within tanking range instead of backing beyond attack reach");
        Check(navigation.Destination is { } safe && f.Module.Bounds.Contains(safe - f.Module.Center),
            "native recipient destination remains within arena bounds");
        var offset = new Cleave(f.Module, 1, new AOEShapeCone(21.25f, 30f.Degrees(), 90f.Degrees()));
        var offsetZone = f.Hints(offset).ForbiddenZones.Single().shapeDistance;
        Check(offsetZone.Distance(new(-7, 0)) < 0 && offsetZone.Distance(new(7, 0)) > 0,
            "aiming restriction respects cone direction offset");
        f.Boss.PosRot.X = f.Ally.PosRot.X = 100;
        f.Boss.PosRot.Z = 200; f.Ally.PosRot.Z = 215;
        var translated = f.Hints().ForbiddenZones.Single().shapeDistance;
        Check(translated.Distance(new(100, 230)) < 0 && translated.Distance(new(100, 170)) > 0,
            "translated source retains radial-independent aim");
    }

    private static void CleaveRecipients()
    {
        using var f = new CleaveFixture(new(0, 15));
        foreach (var job in new[] { Class.WHM, Class.DRG, Class.WAR })
        {
            f.Ally.Class = job;
            var hints = f.Hints(actor: f.Ally);
            Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance.Distance(f.Ally.Position) < 0,
                "healer, DPS and nonholding tank all avoid the ordinary cleave");
            Check(hints.ForbiddenZones[0].shapeDistance.Distance(new(0, 22)) > 0,
                "ordinary victim hazard retains finite attack reach");
        }
        f.World.Execute(new ActorState.OpTarget(f.Boss.InstanceID, f.Ally.InstanceID));
        Check(f.Hints().ForbiddenZones.Single().shapeDistance.Distance(f.Tank.Position) < 0,
            "old tank immediately loses recipient exemption after a target switch");
        f.Tank.PosRot.Z = 40;
        Check(f.Hints(actor: f.Ally).ForbiddenZones.Count == 0, "new target immediately receives range-filtered aim guidance");
        f.World.Execute(new ActorState.OpTarget(f.Boss.InstanceID, 9999));
        Check(f.Hints().ForbiddenZones.Count == 0, "unresolved target does not retain stale recipient guidance");
    }

    private static void CleavePetsAndLifecycle()
    {
        using var f = new CleaveFixture(new(0, 22));
        f.Ally.Type = ActorType.Pet; f.Ally.HitboxRadius = 1;
        Check(f.Hints().ForbiddenZones.Count == 0, "pets remain excluded by default");
        f.Cleave.AllowPetTargets = true;
        Check(f.Hints().ForbiddenZones.Count == 1, "eligible pet hitbox overlapping the outer cap is protected");
        f.Ally.PosRot.Z = 22.26f;
        Check(f.Hints().ForbiddenZones.Count == 0, "pet hitbox wholly beyond attack reach creates no restriction");
        f.Ally.PosRot.Z = 22.24f;
        var cap = f.Hints().ForbiddenZones.Single().shapeDistance;
        Check(cap.Distance(default(WPos) + 60.1f.Degrees().ToDirection() * 7) < 0
            && cap.Distance(default(WPos) + 62f.Degrees().ToDirection() * 7) > 0,
            "pet angular expansion is clipped by victim reach at the outer cap");
        f.Ally.PosRot.Z = .5f;
        Check(f.Hints().ForbiddenZones.Count == 0, "unavoidable source overlap does not ban the recipient from the entire arena");
        f.Ally.PosRot.Z = 15; f.Ally.IsDead = true;
        Check(f.Hints().ForbiddenZones.Count == 0, "dead allies do not constrain aim");
        f.Ally.IsDead = false;
        f.Cleave.NextExpected = Now.AddSeconds(5);
        Check(f.Hints().ForbiddenZones.Single().activation == f.Cleave.NextExpected, "aiming constraint retains its activation time");
        f.Boss.IsTargetable = false;
        Check(f.Hints().ForbiddenZones.Count == 0, "untargetable sources retire their guidance");
        f.Boss.IsTargetable = true; f.Boss.IsDead = true;
        Check(f.Hints().ForbiddenZones.Count == 0, "dead sources retire their guidance");
        f.Boss.IsDead = false;
        var casting = new Cleave(f.Module, 1, f.Cleave.Shape, activeWhileCasting: false) { AllowPetTargets = true };
        f.Boss.CastInfo = Cast(1364, default);
        Check(f.Hints(casting).ForbiddenZones.Count == 0, "activeWhileCasting gate still suppresses persistent cleaves");
        f.Boss.CastInfo = null;
        Check(f.Hints(casting).ForbiddenZones.Count == 1, "casting gate restores guidance when casting stops");
    }

    private static void CleaveOtherHazards()
    {
        using var f = new CleaveFixture(new(0, 40.3f));
        var hints = f.Hints();
        var other = new SDCircle(f.Tank.Position, 2);
        hints.AddForbiddenZone(other, Now.AddSeconds(3));
        f.Cleave.AddAIHints(0, f.Tank, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && ReferenceEquals(hints.ForbiddenZones[0].shapeDistance, other),
            "recipient exemption does not remove an overlapping unrelated hazard");
        var frontal = new SimpleAOEs(f.Module, 999, new AOEShapeCone(20, 60f.Degrees()));
        var cast = Cast(999, default); f.Boss.CastInfo = cast;
        frontal.OnCastStarted(f.Boss, cast);
        frontal.AddAIHints(0, f.Tank, default, hints);
        Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones[1].shapeDistance.Distance(f.Tank.Position) < 0
            && hints.ForbiddenZones[1].activation > Now, "timed dodgeable frontal still forbids the current tank at its deadline");
        Check(ReferenceEquals(hints.PathfindMapBounds, f.Module.Bounds) && hints.ActionsToExecute.Entries.Count == 0,
            "cleave guidance preserves the arena and queues no game actions or control handoff");
        var circle = new Cleave(f.Module, 1, new AOEShapeCircle(5), originAtTarget: true);
        f.Ally.PosRot.Z = 10;
        var separation = f.Hints(circle).ForbiddenZones.Single().shapeDistance;
        Check(separation.Distance(f.Tank.Position) < 0 && separation.Distance(new(0, 0)) > 0,
            "target-centered circles retain finite separation rather than angular guidance");
        var second = f.Create(2, (uint)Titan.OID.Boss, new(0, -5));
        f.World.Execute(new ActorState.OpTarget(second.InstanceID, f.Ally.InstanceID));
        var multiple = f.Hints();
        Check(multiple.ForbiddenZones.Count == 2 && multiple.ForbiddenZones.Any(z => z.shapeDistance.Distance(f.Tank.Position) < 0),
            "holding one enemy does not exempt the tank from another enemy's cleave");
    }

    private static void CleaveVictimGeometry()
    {
        using var f = new CleaveFixture(new(0, 5));
        // Independent closest-point tests on the attack geometry, rather than
        // expectations obtained from the candidate's aiming-distance implementation.
        foreach (var radius in new[] { 0f, .6f, 2.5f })
        foreach (var distance in new[] { 2.1f, 3.9f, 5f, 8f, 10f, 10.1f, 10.18f, 10.3f, 11.1f, 12f, 21.3f, 40.3f })
        {
            if (distance <= radius) continue; // no aim can protect an overlapping victim
            f.Ally.Type = radius > 0 ? ActorType.Pet : ActorType.Player;
            f.Ally.HitboxRadius = radius;
            f.Ally.PosRot.Z = distance;
            foreach (var back in new[] { 0f, 3f })
            {
                var component = new Cleave(f.Module, 1, new AOEShapeRect(10, 2, back, 17f.Degrees())) { AllowPetTargets = true };
                var hints = f.Hints(component);
                for (var degrees = 0; degrees < 360; ++degrees)
                {
                    var aim = (degrees + .37f).Degrees();
                    var attackDir = aim + 17f.Degrees();
                    var localX = distance * attackDir.Sin();
                    var localZ = distance * attackDir.Cos();
                    var qx = Math.Abs(localX) - 2;
                    var qz = Math.Abs(localZ - (10 - back) / 2) - (10 + back) / 2;
                    var expected = MathF.Sqrt(MathF.Pow(Math.Max(qx, 0), 2) + MathF.Pow(Math.Max(qz, 0), 2))
                        + Math.Min(Math.Max(qx, qz), 0) - radius;
                    if (Math.Abs(expected) < .0001f) continue;
                    foreach (var tankRange in new[] { 7f, 100f })
                    {
                        var tankPosition = default(WPos) + aim.ToDirection() * tankRange;
                        var forbidden = hints.ForbiddenZones.Any(z => z.shapeDistance.Distance(tankPosition) < 0);
                        Check(forbidden == (expected < 0), $"finite rectangle aim agrees with victim collision: d={distance}, r={radius}, back={back}, aim={degrees}, tankRange={tankRange}");
                    }
                }
            }
            foreach (var halfAngle in new[] { 30f, 60f, 120f })
            {
                var component = new Cleave(f.Module, 1, new AOEShapeCone(10, halfAngle.Degrees())) { AllowPetTargets = true };
                var hints = f.Hints(component);
                for (var degrees = 0; degrees < 360; ++degrees)
                {
                    var aim = (degrees + .37f).Degrees();
                    var delta = Math.Min(degrees + .37f, 360 - (degrees + .37f));
                    float expected;
                    if (delta <= halfAngle)
                        expected = distance <= 10 ? -1 : distance - 10;
                    else
                    {
                        var angle = (delta - halfAngle).Degrees();
                        var projected = Math.Clamp(distance * angle.Cos(), 0, 10);
                        expected = MathF.Sqrt(distance * distance + projected * projected - 2 * distance * projected * angle.Cos());
                    }
                    expected -= radius;
                    if (Math.Abs(expected) < .0001f) continue;
                    var point = default(WPos) + aim.ToDirection() * 100;
                    Check(hints.ForbiddenZones.Any(z => z.shapeDistance.Distance(point) < 0) == (expected < 0),
                        $"finite cone aim agrees with victim collision: d={distance}, r={radius}, halfAngle={halfAngle}, aim={degrees}");
                }
            }
        }
    }
}
