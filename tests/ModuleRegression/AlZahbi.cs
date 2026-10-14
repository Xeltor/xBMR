using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private const string AlZahbiPrefix = "Dawntrail.Alliance.A31AlZahbi.";

    private static void AlZahbiWave1()
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var first = Actor(100, 0x4DA4, new(710, 720));
        var second = Actor(101, 0x4DA4, new(712, 720));
        first.InCombat = second.InCombat = true;
        world.Actors.Actors.Add(first.InstanceID, first);
        world.Actors.Actors.Add(second.InstanceID, second);
        using var module = BossModuleRegistry.CreateModuleForActor(world, first)!;
        Check(module.GetType().Name == "A31AlZahbiWave1", "wave 1 is registered before Abazohn exists");
        module.Update();
        Check(module.Components.Any(c => c.GetType().Name == "Earthshatter"), "wave 1 activates its AOE component");
        Check(module.Components.All(c => c.GetType().Name != "LeapingCleave"), "wave 1 cannot retain later-wave knockback state");
        first.IsDead = true;
        module.Update();
        Check(module.StateMachine.ActiveState != null, "death of the primary Acrolith does not hide surviving wave 1 enemies");
        second.IsDead = true;
        module.Update();
        Check(module.StateMachine.ActiveState == null && module.Components.Count == 0, "wave 1 retires before the later-wave module takes over");
    }

    private static void AlZahbiKnockback()
    {
        var module = Module(AlZahbiPrefix + "A31AlZahbi", new(721, 720));
        module.Arena.Bounds = new ArenaBoundsRect(25, 20);
        var component = Component<SimpleKnockbacks>(module, AlZahbiPrefix + "LeapingCleave");
        var caster = Actor(100, 0x233C, new(721, 720));
        var player = Actor(1, 0, new(721, 721), true);
        module.WorldState.Party.Members[0] = new(0, player.InstanceID, false);
        var actors = new Actor?[PartyState.MaxAllies];
        actors[0] = player;
        Set(module.WorldState.Party, "_actors", actors);
        var cast = Cast(50481, caster.Position);
        var hints = new AIHints();
        component.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "idle Leaping Cleave has no restriction");
        component.OnCastStarted(caster, cast);
        component.OnCastStarted(caster, cast);
        Check(component.Casters.Count == 1, "repeated cast start keeps a single knockback");
        component.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 1, "targetless Leaping Cleave supplies AI landing safety");
        var zone = hints.ForbiddenZones[0];
        Check(zone.activation == module.CastFinishAt(cast), "landing safety uses the cast deadline");
        Check(zone.shapeDistance.Contains(new(721, 720)), "undefined direction at the source is forbidden");
        Check(zone.shapeDistance.Contains(player.Position), "northward push crosses the short edge");
        Check(!zone.shapeDistance.Contains(new(722, 720)), "eastward push can land safely");
        Check(!zone.shapeDistance.Contains(new(722, 721)), "diagonal landing remains available");
        foreach (var start in new[] { new WPos(721, 721), new WPos(721, 719), new WPos(728, 720), new WPos(714, 720) })
        {
            player.PosRot = new Vector4(start.X, 0, start.Z, 0);
            hints.PathfindMapBounds = module.Arena.Bounds;
            hints.PathfindMapCenter = module.Arena.Center;
            hints.Normalize();
            var decision = BossMod.Pathfinding.NavigationDecision.Build(new(), Now, hints, player);
            Check(decision.Destination is WPos destination && !zone.shapeDistance.Contains(destination),
                "native AI navigation moves each unsafe start to a legal knockback setup");
        }
        // Independently project starts throughout the rectangle, including points near its edges.
        foreach (var origin in new[] { new WPos(721, 720), new WPos(720.9735f, 719.99695f) })
        {
            component.OnCastStarted(caster, Cast(50481, origin));
            hints = new();
            component.AddAIHints(0, player, default, hints);
            for (var x = 697; x < 746; x += 2)
                for (var z = 701; z < 740; z += 2)
                {
                    var dx = x - origin.X;
                    var dz = z - origin.Z;
                    var length = Math.Sqrt(dx * dx + dz * dz);
                    var landingX = x + 22 * dx / length;
                    var landingZ = z + 22 * dz / length;
                    var unsafeLanding = length == 0 || Math.Abs(landingX - 721) > 25 || Math.Abs(landingZ - 720) > 20;
                    Check(hints.ForbiddenZones[0].shapeDistance.Contains(new(x, z)) == unsafeLanding,
                        "AI predicate agrees with rectangular landing geometry");
                }
        }
        var status = new ActorStatus((uint)BossMod.WHM.SID.Surecast, 0, Now.AddSeconds(20), 0);
        component.OnStatusGain(player, ref status);
        hints = new();
        component.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "observed immunity covering impact removes the restriction");
        status.ExpireAt = Now.AddSeconds(1);
        component.OnStatusGain(player, ref status);
        hints = new();
        component.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 1, "immunity expiring before impact retains landing safety");
        component.OnCastFinished(caster, cast);
        Check(component.Casters.Count == 0, "cast finish or cancellation clears the knockback");
        component.OnCastStarted(caster, cast);
        component.OnActorDestroyed(caster);
        Check(component.Casters.Count == 0, "helper destruction clears the knockback");
    }

    private static void AlZahbiExaflares()
    {
        var module = Module(AlZahbiPrefix + "A31AlZahbi", new(721, 720));
        var component = Component<Exaflare>(module, AlZahbiPrefix + "DanceToDust");
        var player = Actor(1, 0, new(721, 720), true);
        foreach (var angle in new[] { 0, 90, 180, 270, 45, 135, 225, 315 })
        {
            var direction = angle.Degrees();
            var origin = new WPos(720, 720) + direction.ToDirection() * 7;
            var caster = Actor((ulong)(100 + angle), 0x233C, origin);
            caster.PosRot = new Vector4(origin.X, 0, origin.Z, direction.Rad);
            var cast = Cast(50096, origin);
            cast.Rotation = direction;
            component.OnCastStarted(caster, cast);
            component.OnCastStarted(caster, cast);
        }
        component.Update();
        Check(component.Lines.Count == 8, "concurrent lines are distinct and duplicate starts do not duplicate them");
        var north = Actor(100, 0x233C, new(720, 727));
        var first = Cast(50096, north.Position);
        component.OnEventCast(north, Effect(50096));
        component.OnCastFinished(north, first);
        component.Update();
        Check(component.Lines.Count == 8, "successful first effect survives the following cast end");
        Check(component.Lines[0].Next == new WPos(720, 736), "next explosion is nine yalms away");
        Check(component.Lines[0].NextExplosion == Now.AddSeconds(1.1), "next explosion is predicted 1.1 seconds later");
        north.PosRot = new Vector4(720, 0, 736, 0);
        component.OnEventCast(north, Effect(50097));
        component.Update();
        Check(component.Lines.Count == 7 && component.Lines.All(l => l.ExplosionsLeft > 0), "final explosion removes its line");
        component.OnEventCast(north, Effect(50097));
        Check(component.Lines.Count == 7, "duplicate final effect cannot consume another source");
        foreach (var angle in new[] { 90, 180, 270, 45, 135, 225, 315 })
            component.OnActorDestroyed(Actor((ulong)(100 + angle), 0x233C, default));
        component.Update();
        Check(component.ActiveAOEs(0, player).Length == 0 && !component.Active, "completed or despawned lines leave no phantom AOEs");
        component.OnCastStarted(north, first);
        component.OnCastFinished(north, first);
        component.Update();
        Check(!component.Active, "cancelled initial cast clears its line");
        component.OnCastStarted(north, first);
        component.Update();
        Check(component.Lines.Count == 1, "a new wave can reuse the same helper");
    }
}
