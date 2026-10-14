using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Checks
{
    private static void HollowNox()
    {
        var world = World();
        using var module = Hollow(world);
        var nox = Component<GenericChasingAOEs>(module, "Nox");
        var players = new[] { Actor(20, 0, module.Center, Class.WHM), Actor(21, 0, module.Center + new WDir(1, 0), Class.BRD), Actor(22, 0, module.Center + new WDir(20, 0), Class.RDM) };
        for (var i = 0; i < players.Length; ++i)
        {
            Member(world, players[i], i * 8);
            world.Execute(new ActorState.OpIcon(players[i].InstanceID, 197, players[i].InstanceID));
        }
        Check(nox.Chasers.Count == 3 && nox.Chasers.All(c => c.NumRemaining == 6 && c.NextActivation == Now.AddSeconds(5)), "all three alliance markers expose early six-hit Nox forecasts");
        world.Frame = new(Now.AddSeconds(.1), 0, 0, 0, 0, 1);
        world.Execute(new ActorState.OpIcon(players[0].InstanceID, 197, players[0].InstanceID));
        Check(nox.Chasers.Count == 3 && nox.Chasers[0].NextActivation == Now.AddSeconds(5), "repeated marker cannot postpone its first deadline");
        var helpers = new List<Actor>();
        foreach (var player in players)
        {
            var helper = Actor((ulong)(30 + helpers.Count), 0x19A, player.Position);
            world.Execute(new ActorState.OpCreate(helper.InstanceID, helper.OID, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
                helper.PosRot, .5f, helper.HPMP, true, false, 0, 0, 0));
            helper = world.Actors.Find(helper.InstanceID)!;
            helpers.Add(helper);
            var cast = new ActorCastInfo { Action = new(ActionType.Spell, 7196), Location = new(player.Position.X, 1, player.Position.Z), TotalTime = 4.7f };
            world.Execute(new ActorState.OpCastInfo(helper.InstanceID, cast));
        }
        Check(nox.ActiveAOEs(0, players[0]).Length == 3 && nox.Chasers.All(c => c.MoveDist == 0), "actual first casts lock their separate initial locations");
        world.Frame = new(Now.AddSeconds(5), 0, 0, 0, 0, 1);
        for (var i = 0; i < helpers.Count; ++i)
        {
            world.Execute(new ActorState.OpCastEvent(helpers[i].InstanceID, Hit(7196, players[i].Position)));
            world.Execute(new ActorState.OpCastInfo(helpers[i].InstanceID, null));
        }
        Check(nox.Chasers.Count == 3 && nox.Chasers.All(c => c.NumRemaining == 5 && c.MoveDist == 4.5f), "successful first hits retain all five repeats after cast completion");
        for (var pulse = 0; pulse < 5; ++pulse)
        {
            world.Frame = new(Now.AddSeconds(6 + pulse), 0, 0, 0, 0, 1);
            foreach (var player in players)
                world.Execute(new ActorState.OpCastEvent(helpers[0].InstanceID, Hit(7195, player.Position)));
            Check(pulse == 4 ? nox.Chasers.Count == 0 : nox.Chasers.Count == 3 && nox.Chasers.All(c => c.NumRemaining == 4 - pulse), "simultaneous overlapping Nox pulses consume each sequence once: " + pulse);
            if (pulse != 4)
                Check(nox.Chasers.All(c => Math.Abs((c.NextActivation - world.CurrentTime).TotalSeconds - .95) < .00001), "repeat planning deadline is early enough for observed one-second cadence");
        }
        Check(Hints(module, nox, 0, players[0]).ForbiddenZones.Count == 0, "all three sixth hits retire their own AI constraints");

        world.Frame = new(Now.AddSeconds(20), 0, 0, 0, 0, 1);
        world.Execute(new ActorState.OpIcon(players[0].InstanceID, 197, players[0].InstanceID));
        world.Execute(new ActorState.OpCastInfo(helpers[0].InstanceID, new() { Action = new(ActionType.Spell, 7196), Location = new(players[0].Position.X, 1, players[0].Position.Z), TotalTime = 4.7f }));
        world.Execute(new ActorState.OpCastInfo(helpers[0].InstanceID, null));
        Check(nox.Chasers.Count == 0, "cancelled initial cast removes only its unconfirmed chase");
        world.Execute(new ActorState.OpIcon(players[0].InstanceID, 197, players[0].InstanceID));
        world.Frame = new(Now.AddSeconds(28), 0, 0, 0, 0, 1);
        nox.Update();
        Check(nox.Chasers.Count == 0, "missing first cast/strike cannot keep an old target forever");

        SingleStackRecovery();
    }

    private static void SingleStackRecovery()
    {
        foreach (var hollow in new[] { false, true })
        {
            var world = World();
            var target = Actor(20, 0, new(-350, -445), Class.SGE);
            Member(world, target, 0);
            using BossModule module = hollow ? Hollow(world) : new BossMod.Heavensward.Alliance.A35Diabolos.A35Diabolos(world, Actor(1, 0x1907, target.Position));
            if (!hollow) module.StateMachine.Start(Now);
            var stack = Component<StackWithIcon>(module, hollow ? "HollowNight" : "NightTerror");
            var action = hollow ? 7197u : 7187u;
            stack.OnEventCast(module.PrimaryActor, Hit(action, target.Position, target.InstanceID));
            Check(stack.CastCounter == 0, "empty late stack resolution cannot poison the next wave");
            stack.OnEventIcon(target, hollow ? 91u : 62u, target.InstanceID);
            stack.OnEventCast(module.PrimaryActor, Hit(action, target.Position, target.InstanceID));
            Check(stack.Stacks.Count == 0 && stack.CastCounter == 0, "fresh single-hit stack resolves after prior empty hit");
        }
    }
}
