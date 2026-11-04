using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private const string CompoundPrefix = "Shadowbringers.Alliance.A24TheCompound2P.";

    private static void CompoundPrimeBladeTransfer()
    {
        var center = new WPos(200, -700);
        var landing = new WPos(225, -725);
        var module = Module(CompoundPrefix + "A24TheCompound2P", center);
        var transfer = Component<GenericAOEs>(module, CompoundPrefix + "PrimeBladeTransfer");
        var boss = Actor(100, 11974, center);
        var origin = Actor(101, 9020, center);
        var destination = Actor(102, 9020, landing);
        var player = Actor(1, 0, center, true);
        module.WorldState.Actors.Actors[origin.InstanceID] = origin;
        var link = new ActorTetherInfo(116, origin.InstanceID);
        transfer.OnTethered(destination, link);
        var cast = Cast(20889, center);
        cast.Rotation = new(-2.9155f);
        transfer.OnCastStarted(boss, cast);
        transfer.Update();
        Check(transfer.ActiveAOEs(0, player).Length == 0, "the unresolved transferred line does not guess the destination facing");
        var global = new BossComponent.GlobalHints();
        transfer.AddGlobalHints(player, global);
        Check(global.Single().Contains("teleport"), "the cast still warns players before arrival");
        transfer.OnUntethered(destination, link);
        boss.PosRot = new(landing.X, -480, landing.Z, -1.1065f);
        transfer.Update();
        var line = transfer.ActiveAOEs(0, player).ToArray().Single();
        Check(line.ActorID == boss.InstanceID && line.Origin.AlmostEqual(landing, 0.03f) && line.Rotation == boss.Rotation && line.Activation == module.CastFinishAt(cast),
            "the associated caster's arrival pose supplies the line and keeps its original deadline");
        Check(line.Check(new(211.15f, -708.57f)) && !line.Check(landing + cast.Rotation.ToDirection() * 25),
            "recorded destination facing covers a listed target while rejecting the old cast heading");
        var hints = new AIHints();
        transfer.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Single().shapeDistance.Contains(new(211.15f, -708.57f)), "AI consumes the same transferred line");
        transfer.OnCastFinished(Actor(200, 11974, landing), cast);
        Check(transfer.ActiveAOEs(0, player).Length == 1, "an unrelated caster cannot cancel the observed line");
        transfer.OnEventCast(boss, Effect(20889));
        Check(transfer.ActiveAOEs(0, player).Length == 0, "matching impact clears the line");
    }

    private static void CompoundDisdainSequence()
    {
        var center = new WPos(200, -700);
        var module = Module(CompoundPrefix + "A24TheCompound2P", center);
        var stack = Component<GenericStackSpread>(module, CompoundPrefix + "ThreePartsDisdainStack");
        var knockback = Component<GenericKnockback>(module, CompoundPrefix + "ThreePartsDisdainKnockback");
        var boss = Actor(100, 11974, center);
        var target = Actor(1, 0, center + new WDir(0, 1), true);
        var participant = Actor(2, 0, target.Position + new WDir(1, 0), true);
        module.WorldState.Actors.Actors[target.InstanceID] = target;
        var cast = Cast(20891, target.Position);
        cast.TargetID = target.InstanceID;
        stack.OnCastStarted(boss, cast);
        stack.OnCastStarted(boss, cast);
        var initial = knockback.ActiveKnockbacks(0, target).ToArray();
        Check(stack.Stacks.Count == 1 && initial.Select(k => k.Distance).SequenceEqual([8f, 8f, 12f]), "one stack owns three ordered displacement forecasts");
        Check(initial[2].Activation == initial[0].Activation.AddSeconds(1.09).AddSeconds(1.38), "third hit uses cumulative delay");
        module.WorldState.Frame = new(Now.AddSeconds(10), 0, 0, 0, 0, 1);
        stack.OnEventCast(boss, Effect(20891));
        stack.OnEventCast(boss, Effect(20891));
        var second = knockback.ActiveKnockbacks(0, target).ToArray();
        Check(second.Length == 2 && second[0].Activation == Now.AddSeconds(10).AddSeconds(1.09),
            "first impact anchors the second deadline and duplicate impact cannot advance it again");
        module.WorldState.Frame = new(Now.AddSeconds(11.1), 0, 0, 0, 0, 1);
        stack.OnEventCast(boss, Effect(20892));
        var third = knockback.ActiveKnockbacks(0, target).ToArray().Single();
        Check(third.Distance == 12 && third.Activation == module.WorldState.FutureTime(1.38), "second impact anchors the 12-yalm final push");
        var hints = new AIHints();
        knockback.AddAIHints(0, participant, default, hints);
        var prep = hints.ForbiddenZones.Single();
        Check(prep.activation == third.Activation && prep.shapeDistance.Contains(center + new WDir(0, 18))
            && !prep.shapeDistance.Contains(participant.Position), "participant prepares for a safe landing at the next deadline");
        stack.OnEventCast(boss, Effect(20893));
        Check(stack.Stacks.Count == 0 && knockback.ActiveKnockbacks(0, target).Length == 0, "third hit clears both stack and knockback");
        stack.OnCastStarted(boss, cast);
        stack.OnCastFinished(boss, cast);
        Check(knockback.ActiveKnockbacks(0, target).Length == 0, "interrupted stack clears a new sequence before its first hit");
    }
}
