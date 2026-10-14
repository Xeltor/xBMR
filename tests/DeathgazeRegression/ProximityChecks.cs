using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A31DeathgazeHollow;

static partial class Checks
{
    private static void ProximityImpactLifecycle()
    {
        using var module = Module();
        var world = module.WorldState;
        world.Actors.Actors[module.PrimaryActor.InstanceID] = module.PrimaryActor;
        var players = new[] { Actor(1, 0, new(300, 410), true), Actor(2, 0, new(300, 410), true), Actor(3, 0, new(300, 410), true) };
        Party(module, players);
        var component = Component<GenericAOEs>(module, "VoidBlizzardIV");
        BossComponent.GlobalHints Global()
        {
            BossComponent.GlobalHints result = [];
            component.AddGlobalHints(players[0], result);
            return result;
        }
        var visual = Cast(7287, module.PrimaryActor.Position, 2.7f);
        world.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID, visual));
        Check(Global().Single().Contains("Proximity damage next"), "boss preparation supplies earliest proximity response before helper positions exist");
        Check(component.ActiveAOEs(0, players[0]).Length == 0 && Hints(component, players[0]).PredictedDamage.Count == 0,
            "boss preparation does not guess impact centers or report its own visual finish as damage");
        world.Execute(new ActorState.OpCastInfo(module.PrimaryActor.InstanceID, null));
        var first = Create(module, Actor(47, (uint)OID.Helper, new(314.99194f, 420.58447f)));
        var second = Create(module, Actor(48, (uint)OID.Helper, new(304.98206f, 420.58447f)));
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(7307, first.Position, 4.7f)));
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(7307, second.Position, 4.7f)));
        var aoes = component.ActiveAOEs(0, players[0]).ToArray();
        Check(aoes.Length == 2 && aoes.Select(a => a.ActorID).ToHashSet().SetEquals([first.InstanceID, second.InstanceID]),
            "both native helper casts retain separate observed impact origins after visual completion");
        Check(aoes.All(a => !a.Risky && a.Shape is AOEShapeCircle { Radius: 40 } && a.Activation == Now.AddSeconds(5)),
            "effect range remains informational while helpers supply their actual future damage deadline");
        Check(Global().Single().Contains("5.0s") && Global().Single().Contains("opposite the impact markers"),
            "actual helper window has continuous countdown and a guide-supported response through the previous warning gap");
        var hints = Hints(component, players[0]);
        Check(hints.PredictedDamage.Count == 2 && hints.PredictedDamage.All(p => p.Activation == Now.AddSeconds(5)
            && p.Players[0] && p.Players[1] && p.Players[16]),
            "each real impact supplies future healing planning across all alliances");
        Check(hints.ForbiddenZones.Count == 0 && hints.GoalZones.Count == 0 && hints.ForcedMovement == null,
            "proximity guidance invents no safe-radius hard exclusion or outward preference that conflicts with ice preparation");
        world.Frame = new(Now.AddSeconds(0.5), 0, 0, 0, 0, 1);
        component.OnCastStarted(first, first.CastInfo!);
        Check(component.ActiveAOEs(0, players[0]).Length == 2 && component.ActiveAOEs(0, players[0]).ToArray().All(a => a.Activation == Now.AddSeconds(5)),
            "duplicate helper observation cannot duplicate damage planning or postpone established impact timing");
        Check(Global().Single().Contains("4.5s"), "helper countdown follows time rather than restarting at each hint query");
        world.Execute(new ActorState.OpCastEvent(second.InstanceID, Hit(7307)));
        Check(component.ActiveAOEs(0, players[0]).Length == 1 && component.ActiveAOEs(0, players[0])[0].ActorID == first.InstanceID
            && Hints(component, players[0]).PredictedDamage.Count == 1,
            "second helper effect retires only its own impact while the other warning remains");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(component.ActiveAOEs(0, players[0]).Length == 1, "resolved helper cast-end cannot remove its sibling impact");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(Global().Count == 0 && Hints(component, players[0]).PredictedDamage.Count == 0,
            "native cancelled helper removes the remaining countdown and healing forecast");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(7307, first.Position, 4.7f)));
        var replacement = Create(module, Actor(first.InstanceID, (uint)OID.Helper, new(319, 398.6f)));
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, players[0]).Length == 0, "replacement removes an old generation's pending impact without inventing a new one");
        world.Execute(new ActorState.OpCastInfo(replacement.InstanceID, Cast(7307, replacement.Position, 4.7f)));
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, players[0]).Length == 1 && component.ActiveAOEs(0, players[0])[0].Origin == replacement.Position,
            "stale generation callbacks cannot retire a replacement's new impact");
        world.Execute(new ActorState.OpDestroy(replacement.InstanceID));
        Check(Global().Count == 0, "destroyed helper retires its current-generation proximity warning");

        var preexisting = Actor(90, (uint)OID.Helper, new(305, 420.6f));
        preexisting.CastInfo = Cast(7307, preexisting.Position, 4.7f);
        preexisting.CastInfo.ElapsedTime = 3;
        world.Actors.Actors[preexisting.InstanceID] = preexisting;
        module.StateMachine.Reset();
        module.StateMachine.Start(Now.AddSeconds(0.5));
        component = Component<GenericAOEs>(module, "VoidBlizzardIV");
        Check(component.ActiveAOEs(0, players[0]).Length == 1 && component.ActiveAOEs(0, players[0])[0].Activation == Now.AddSeconds(2.5),
            "mid-cast bootstrap recovers remaining2s instead of restarting the full5s impact");
        world.Frame = new(Now.AddSeconds(4.6), 0, 0, 0, 0, 1);
        component.Update();
        Check(Global().Count == 0 && Hints(component, players[0]).PredictedDamage.Count == 0,
            "missing helper resolution bounds stale countdown and damage planning without claiming a safe expiry");
        world.Execute(new ActorState.OpDestroy(preexisting.InstanceID));
        module.StateMachine.Reset();
        module.StateMachine.Start(Now.AddSeconds(1));
        component = Component<GenericAOEs>(module, "VoidBlizzardIV");
        Check(Global().Count == 0 && component.ActiveAOEs(0, players[0]).Length == 0,
            "fresh module state cannot retain a previous proximity origin or countdown");
    }
}
