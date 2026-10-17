using BossMod;
using BossMod.Components;
using Pelican = BossMod.RealmReborn.Dungeon.D07Brayflox.D071GreatYellowPelican;
using Drake = BossMod.RealmReborn.Dungeon.D07Brayflox.D072InfernoDrake;

static partial class Checks
{
    static partial void SlumberLifecycle()
    {
        var ws = World(); var boss = Create(ws, 1, 0x1A7, new(113, -14)); var sable = Create(ws, 2, 0x1AA, new(113, -14));
        var pc = Player(ws, position: new(113, -10)); Combat(ws, boss);
        var cast = Cast(ws, sable, 508, 2.7f);
        using var module = new Pelican.D071GreatYellowPelican(ws, boss); module.Update();
        var slumber = AOE(module, 508);
        Check(slumber.Casters.Count == 1, "Pelican activation bootstraps Slumber Breath from Sable Back");
        Check(slumber.Shape is AOEShapeCone c && Math.Abs(c.Radius - 7.2f) < .001 && Math.Abs(c.HalfAngle.Rad - MathF.PI / 3) < .001, "Slumber nominal cone uses full120degree width and radius7.2");
        Check(Hints(module, pc).ForbiddenZones.Count == 1 && HasRisk(module, pc), "Slumber produces timed AI avoidance and player warning");
        Check(slumber.Shape.Check(sable.Position + new WDir(0, 7.19f), sable.Position, default)
            && !slumber.Shape.Check(sable.Position + new WDir(0, 7.21f), sable.Position, default), "Slumber nominal radial boundary probes distinguish both sides");
        slumber.OnCastStarted(sable, cast);
        Check(slumber.Casters.Count == 1, "Slumber duplicate callback does not duplicate guidance");
        var friendly = Create(ws, 3, 0x1AA, ally: true); Cast(ws, friendly, 508, 2.7f);
        var wrongEnemy = Create(ws, 4, 0x1A9); Cast(ws, wrongEnemy, 508, 2.7f);
        Cast(ws, boss, 508, 2.7f);
        Check(slumber.Casters.Count == 1, "Slumber source filter excludes friendly Sable Violet Back and boss");
        slumber.OnCastStarted(sable, new ActorCastInfo { Action = new(ActionType.Ability, 508), TotalTime = 2.7f });
        Check(slumber.Casters.Count == 1, "nonspell Slumber numeric ID cannot replace current hazard");
        Finish(ws, sable);
        Check(slumber.Casters.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "Slumber cancellation clears hazard and AI constraint");
        Cast(ws, sable, 508, 2.7f); ws.Execute(new ActorState.OpDead(sable.InstanceID, true));
        Check(slumber.Casters.Count == 0, "Sable death clears Slumber before finish callback");
        ws.Execute(new ActorState.OpDead(sable.InstanceID, false)); Finish(ws, sable); Cast(ws, sable, 508, 2.7f);
        ws.Execute(new ActorState.OpDestroy(sable.InstanceID));
        Check(slumber.Casters.Count == 0, "Sable destruction clears Slumber");
        var replacement = Create(ws, 2, 0x1AA);
        Check(slumber.Casters.Count == 0, "recreated Sable ID cannot revive old Slumber");
        Cast(ws, replacement, 508, 2.7f);
        Check(slumber.Casters.Count == 1, "replacement Sable fresh Slumber is handled");
        Check(Hints(module, pc, (replacement, -3)).PotentialTargets[0].Priority == -3, "Sable native passive exclusion survives encounter priority");
        Check(Hints(module, pc, (replacement, 0)).PotentialTargets[0].Priority == 2, "eligible Sable retains secondary add priority");
    }

    static partial void LevinshowerGuidance()
    {
        var ws = World(); var boss = Create(ws, 1, 0x3DE, new(-10, -88));
        var add = Create(ws, 2, 0x3DF, new(-10, -84)); add.PosRot.W = -90.Degrees().Rad;
        var brayflox = Create(ws, 3, 0x1A6, new(-10, -80), ally: true, targetable: false);
        var pc = Player(ws, position: new(-10, -82)); var tank = Player(ws, 101, new(-10, -88), slot: 1);
        add.TargetID = tank.InstanceID; Combat(ws, boss);
        using var module = new Drake.D072InfernoDrake(ws, boss); module.Update();
        var levin = module.Components.OfType<GenericAOEs>().Single(c => c.WatchedAction == 985);
        var before = levin.ActiveAOEs(0, pc).ToArray();
        Check(before.Length == 1 && before[0].Activation == default && before[0].Risky, "living Tempest supplies conservative early guidance before the halfsecond cast");
        Check(before[0].Check(pc.Position) && !before[0].Check(tank.Position), "prewarning aims at live friendly Brayflox despite stale tank TargetID and caster facing");
        Check(Hints(module, pc).ForbiddenZones.Count == 1 && HasRisk(module, pc), "prewarning reaches AI and player guidance before a cast exists");
        Check(before[0].Shape is AOEShapeCone policy && Math.Abs(policy.Radius - 8.16f) < .001 && Math.Abs(policy.HalfAngle.Rad - MathF.PI / 2) < .001, "Levinshower policy is explicitly a frontsemicircle rather than a calibrated physical angle");
        var cast = Cast(ws, add, 985, .2f, rotation: 90.Degrees());
        var actual = levin.ActiveAOEs(0, pc).ToArray();
        Check(actual.Length == 1 && Math.Abs((actual[0].Activation - Now).TotalSeconds - .5) < .001, "actual short cast replaces policy with native corrected deadline");
        Check(actual[0].Check(new(-6, -84)) && !actual[0].Check(new(-14, -84)), "actual cast rotation overrides stale tank TargetID and Brayflox preview direction");
        levin.OnCastStarted(add, cast);
        Check(levin.ActiveAOEs(0, pc).Length == 1, "duplicate Levinshower callback does not duplicate hazard");
        var oldDeadline = actual[0].Activation;
        ws.Frame = new(Now.AddSeconds(.25), 0, 0, 0, 0, 1); cast.ElapsedTime = .25f;
        levin.OnCastStarted(add, cast);
        Check(levin.ActiveAOEs(0, pc)[0].Activation == oldDeadline, "samecast duplicate in raw timer tail cannot move its deadline");
        ws.Frame = new(Now, 0, 0, 0, 0, 1);
        var replacementCast = Cast(ws, add, 985, .4f, rotation: -90.Degrees());
        levin.OnCastFinished(add, cast);
        Check(levin.ActiveAOEs(0, pc)[0].Activation > oldDeadline && levin.ActiveAOEs(0, pc)[0].Check(new(-14, -84)), "late finish for replaced cast cannot retire new cast guidance");
        Finish(ws, add);
        Check(levin.ActiveAOEs(0, pc).Length == 1 && levin.ActiveAOEs(0, pc)[0].Activation == default, "cast cancellation returns to persistent early policy while add lives");
        var friendly = Create(ws, 4, 0x3DF, ally: true); Cast(ws, friendly, 985, .2f);
        var stranger = Create(ws, 5, 0x9999); Cast(ws, stranger, 985, .2f);
        Cast(ws, boss, 985, .2f);
        Check(levin.ActiveAOEs(0, pc).Length == 1 && levin.ActiveAOEs(0, pc)[0].Activation == default, "same numeric spell from friendly add unrelated actor or boss does not create cast deadline");
        levin.OnCastStarted(add, new ActorCastInfo { Action = new(ActionType.Ability, 985), TotalTime = .2f });
        Check(levin.ActiveAOEs(0, pc)[0].Activation == default, "nonspell Levinshower ID leaves conservative preview unchanged");
        ws.Execute(new ActorState.OpDestroy(brayflox.InstanceID));
        Check(module.CalculateGlobalHints(pc).Any(h => h.Contains("direction uncertain", StringComparison.Ordinal)), "missing Brayflox direction is explicitly uncertain");
        Check(levin.ActiveAOEs(0, pc).Length == 1, "missing NPC cannot erase early hazard policy");
        Create(ws, 3, 0x1A6, new(-10, -80), ally: true, targetable: false);
        Check(!module.CalculateGlobalHints(pc).Any(h => h.Contains("direction uncertain", StringComparison.Ordinal)), "fresh friendly NPC restores directional context");
        ws.Execute(new ActorState.OpTargetable(add.InstanceID, false));
        Check(levin.ActiveAOEs(0, pc).Length == 1, "untargetable living Tempest still presents cleave danger");
        Cast(ws, add, 985, .2f); ws.Execute(new ActorState.OpDead(add.InstanceID, true));
        Check(levin.ActiveAOEs(0, pc).Length == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "dead Tempest removes cast and conservative guidance");
        ws.Execute(new ActorState.OpDead(add.InstanceID, false)); Finish(ws, add); Cast(ws, add, 985, .2f);
        ws.Execute(new ActorState.OpDestroy(add.InstanceID));
        Check(levin.ActiveAOEs(0, pc).Length == 0, "destroyed Tempest removes all guidance");
        var replacement = Create(ws, 2, 0x3DF);
        Check(levin.ActiveAOEs(0, pc).Length == 1 && levin.ActiveAOEs(0, pc)[0].Activation == default, "numeric actor reuse starts fresh policy without old cast deadline");
        Cast(ws, replacement, 985, .2f);
        Check(levin.ActiveAOEs(0, pc).Length == 1 && levin.ActiveAOEs(0, pc)[0].Activation != default, "replacement actor fresh cast gains a new deadline");
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0 && Hints(module, pc).ForbiddenZones.Count == 0, "boss phase exit clears earlypolicy and actualcast constraints");

        var boot = World(); var bootBoss = Create(boot, 1, 0x3DE); var bootAdd = Create(boot, 2, 0x3DF); var bootPc = Player(boot);
        Combat(boot, bootBoss); Cast(boot, bootAdd, 985, .2f);
        using var bootModule = new Drake.D072InfernoDrake(boot, bootBoss); bootModule.Update();
        var bootLevin = bootModule.Components.OfType<GenericAOEs>().Single(c => c.WatchedAction == 985);
        Check(bootLevin.ActiveAOEs(0, bootPc).Length == 1 && bootLevin.ActiveAOEs(0, bootPc)[0].Activation != default, "activation bootstraps already-running Levinshower rather than erasing deadline");
    }
}
