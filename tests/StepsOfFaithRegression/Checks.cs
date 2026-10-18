using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Pathfinding;
using BossMod.RealmReborn.Quest.MSQ.TheStepsOfFaith;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 18, 17, 34, 34, DateTimeKind.Utc);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        ++assertions;
    }
    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, ActorType type = ActorType.Enemy, bool ally = false)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.DRG, 50,
            new Vector4(pos.X, 0, pos.Z, MathF.PI), .5f, new(5104, 5104, 0, 10000, 10000), true, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }
    private static ActorCastInfo Cast(WorldState ws, Actor caster, AID aid, float duration, WPos? pos = null, ulong target = 0)
    {
        var p = pos ?? caster.Position;
        ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, new ActorCastInfo
        {
            Action = new(ActionType.Spell, (uint)aid), Location = new(p.X, 0, p.Z), Rotation = 180f.Degrees(), TotalTime = duration, TargetID = target
        }));
        return caster.CastInfo!;
    }
    private static void Hit(WorldState ws, Actor caster, AID aid, WPos? pos = null)
    {
        var p = pos ?? caster.Position;
        ws.Execute(new ActorState.OpCastEvent(caster.InstanceID,
            new(new(ActionType.Spell, (uint)aid), 0, 0, 0, new(p.X, 0, p.Z), 1, 0, default)));
    }
    private static void Finish(WorldState ws, Actor caster)
    {
        caster.CastInfo!.ElapsedTime = caster.CastInfo.TotalTime;
        ws.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
    }
    private static GenericAOEs Component(BossModule module, string name) => module.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == name);
    private static GenericAOEs.AOEInstance[] AOEs(BossModule module, Actor pc, string name) => Component(module, name).ActiveAOEs(0, pc).ToArray();
    private static AIHints Hints(BossModule module, Actor pc)
    {
        var h = new AIHints(); module.CalculateAIHints(0, pc, default, h); return h;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 1068, CurrentCFCID = 885 };
        var pc = Create(ws, 5, 0, new(7.38f, -170.77f), ActorType.Player);
        var boss = Create(ws, 29, (uint)OID.Boss, new(0, -140)); boss.InCombat = true;
        var helper = Create(ws, 46, (uint)OID.Helper, boss.Position, ActorType.Helper);
        var other = Create(ws, 45, (uint)OID.Helper, boss.Position, ActorType.Helper);
        using var module = new Vishap(ws, boss); module.Update();
        Check(BossModuleRegistry.FindByOID((uint)OID.Boss)?.ModuleType == module.GetType(), "generated registry activates Vishap");
        Check(module.Components.Count == 20, "all encounter components activate");

        // Recorded final failure: a visual and Fall of Man start together; neither damaging action has a cast start.
        Cast(ws, boss, AID.ScorchingBreathVisual, 14.7f);
        Check(AOEs(module, pc, "ScorchingBreath").Length == 1, "visual gives immediate early warning");
        Cast(ws, helper, AID.FallOfMan, 19.7f);
        var final = AOEs(module, pc, "ScorchingBreath").Single();
        Check(final.Check(pc.Position), "recorded lethal position is forbidden before Fall of Man");
        Check(!final.Check(new(11, -170)) && !final.Check(new(-11, -170)), "both side lanes remain available");
        Check(final.Check(new(0, -220)), "90y initial blast covers its far end");
        Check(Math.Abs((final.Activation - Now).TotalSeconds - 19.7) < .01, "helper supplies conservative future deadline");
        Hit(ws, helper, AID.FallOfMan); Finish(ws, helper); Finish(ws, boss);
        Check(AOEs(module, pc, "ScorchingBreath").Length == 1, "initial hit and cast finishes retain channel guidance");
        Check(!AOEs(module, pc, "ScorchingBreath")[0].Check(new(0, -190)), "40y channel restores space beyond its end");
        for (var i = 0; i < 9; ++i) Hit(ws, helper, AID.ScorchingBreath);
        Check(AOEs(module, pc, "ScorchingBreath").Single().Check(pc.Position), "all repeated pulses retain the lethal lane");
        ws.Execute(new ActorState.OpDestroy(helper.InstanceID)); module.Update();
        Check(AOEs(module, pc, "ScorchingBreath").Length == 0, "channel source destruction clears warning");
        helper = Create(ws, 46, (uint)OID.Helper, boss.Position, ActorType.Helper);
        Cast(ws, helper, AID.FallOfMan, 20); ws.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(AOEs(module, pc, "ScorchingBreath").Length == 0, "cancelled final cast clears warning");

        // Earlier casts use two different breath warnings and interleaved helper pulses.
        foreach (var aid in new[] { AID.FlameBreath1, AID.FlameBreath3 })
        {
            Cast(ws, helper, aid, 4.7f);
            var breath = AOEs(module, pc, "FlameBreath").Single();
            Check(Math.Abs((breath.Activation - ws.CurrentTime).TotalSeconds - 5) < .01, "breath deadline is the warning finish, not an extra second late");
            Check(breath.Check(new(0, -170)) && !breath.Check(new(11, -170)), "breath lane and safe flank use width 20");
            Check(breath.Check(new(0, -250)) == (aid == AID.FlameBreath1), "long initial breath and shorter ward breath have separate lengths");
            Hit(ws, helper, aid); Finish(ws, helper);
            Hit(ws, other, AID.FlameBreathChannel);
            module.Update();
            Check(AOEs(module, pc, "FlameBreath").Length == 1, "cast finish and another helper's pulse preserve channel");
            ws.Frame = new(ws.CurrentTime.AddSeconds(1), 0, 0, 0, 0, 1); module.Update();
            Check(AOEs(module, pc, "FlameBreath").Length == 0, "channel clears after observed pulses cease");
        }
        Cast(ws, helper, AID.FlameBreath1, 5); ws.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(AOEs(module, pc, "FlameBreath").Length == 0, "cancelled breath has no ghost lane");

        pc.PosRot = new(0, 0, 245, 0);
        boss.PosRot = new(0, 0, 340, MathF.PI); helper.PosRot = boss.PosRot;
        Cast(ws, boss, AID.CauterizeVisual, 30.2f); Cast(ws, helper, AID.FlameBreath, 34.7f);
        var charge = AOEs(module, pc, "Cauterize").Single();
        Check(charge.Check(new(0, 220)) && !charge.Check(new(0, 170)), "charge covers bridge above the retreat safe area");
        Check((charge.Activation - ws.CurrentTime).TotalSeconds < 34.1, "charge warning accounts for hits preceding helper cast finish");
        var chargeHints = Hints(module, pc); chargeHints.Normalize();
        var chargeStep = NavigationDecision.Build(new(), ws.CurrentTime, chargeHints, pc);
        Check(chargeStep.Destination is WPos cd && cd.Z < pc.Position.Z, "native navigation chooses a retreat from the charge in the full corridor");
        for (var i = 0; i < 6; ++i) Hit(ws, other, AID.Cauterize);
        Finish(ws, boss); Finish(ws, helper);
        Check(AOEs(module, pc, "Cauterize").Length == 1, "all six charge hits retain guidance through the final helper");
        ws.Frame = new(ws.CurrentTime.AddSeconds(1), 0, 0, 0, 0, 1); module.Update();
        Check(AOEs(module, pc, "Cauterize").Length == 0, "resolved charge clears");

        foreach (var (index, state, min, max) in new (byte, uint, float, float)[] { (3, 0x00020001, 120, 200), (0, 0x00800040, -40, 200), (4, 0x00020001, -40, 40), (1, 0x00800040, -200, 40), (6, 0x00020001, -200, -120) })
        {
            ws.Execute(new WorldState.OpMapEffect(index, state));
            Check(module.Arena.InBounds(new(21.9f, min + .1f)) && module.Arena.InBounds(new(-21.9f, max - .1f)), "whole current ward corridor is navigable");
            Check(!module.Arena.InBounds(new(22.1f, (min + max) / 2)) && !module.Arena.InBounds(new(0, min - .1f)), "sidewalls and phase end remain bounded");
        }
        ws.Execute(new WorldState.OpMapEffect(0, 0x00800040)); pc.PosRot = new(0, 0, 160, 0);
        var retreat = Hints(module, pc);
        Check(retreat.ForbiddenZones.Any(z => z.shapeDistance.Distance(pc.Position) <= 0) && module.Arena.InBounds(new(0, 24)), "retreat constrains current position and exposes reachable next ward");
        retreat.Normalize();
        var retreatStep = NavigationDecision.Build(new(), ws.CurrentTime, retreat, pc);
        Check(retreatStep.Destination is WPos rd && rd.Z < 25, "native navigation reaches the next ward trigger across the full transition corridor: destination=" + retreatStep.Destination + ", next=" + retreatStep.NextWaypoint + ", time=" + retreatStep.TimeToGoal);

        boss.PosRot = new(0, 0, 180, MathF.PI); boss.IsTargetable = false;
        Hit(ws, boss, AID.Twingaze);
        Check(AOEs(module, pc, "BlazingFire").Length == 4, "retreat predicts all four fixed fireballs before damage");
        Hit(ws, boss, AID.BlazingFire, new(12, 82));
        Check(AOEs(module, pc, "BlazingFire").Length == 3, "individual impact removes only its prediction");
        boss.IsTargetable = true; module.Update();
        Check(AOEs(module, pc, "BlazingFire").Length == 0, "next combat phase clears retreat predictions");

        boss.PosRot = new(0, 0, -140, MathF.PI); helper.PosRot = boss.PosRot; other.PosRot = boss.PosRot;
        var third = Create(ws, 42, (uint)OID.Helper, boss.Position, ActorType.Helper);
        Cast(ws, helper, AID.Earthshaker, 6.2f); Cast(ws, other, AID.Earthshaker, 7.7f); Cast(ws, third, AID.Earthshaker, 9.2f);
        Check(AOEs(module, pc, "Earthshaker").Length == 3, "all three staggered cones remain in AI predictions");
        Finish(ws, helper);
        Check(AOEs(module, pc, "Earthshaker").Length == 2, "resolved cone opens its lane while later cones remain");
        Finish(ws, other); Finish(ws, third);

        helper.PosRot = new(-6, 0, -5.5f, MathF.PI);
        Cast(ws, helper, AID.EarthrisingCast, 6.7f); module.Update();
        var exa = (Exaflare)Component(module, "Earthrising");
        Check(exa.Lines.Count == 1 && exa.ActiveAOEs(0, pc).Length == 2, "exaflare radar previews just current and next explosion");
        var exaHints = new AIHints(); exa.AddAIHints(0, pc, default, exaHints);
        Check(exaHints.ForbiddenZones.Count == 5, "AI receives all five exaflare deadlines");
        for (var i = 0; i < 5; ++i)
        {
            helper.PosRot = new(-6, 0, -5.5f - 7.5f * i, MathF.PI);
            Hit(ws, helper, i == 0 ? AID.EarthrisingCast : AID.EarthrisingRepeat); module.Update();
        }
        Check(exa.Lines.Count == 0 && exa.ActiveAOEs(0, pc).Length == 0, "all exaflare steps retire without a ghost line");
        Finish(ws, helper);

        ws.Execute(new WorldState.OpMapEffect(6, 0x00020001));
        // Knockback needs a safe landing two seconds before the ground circles resolve.
        pc.PosRot = new(0, 0, -150, 0); helper.PosRot = new(0, 0, -120, MathF.PI);
        Cast(ws, other, AID.Flamisphere, 8, new(0, -170)); Cast(ws, helper, AID.BodySlam, 6);
        var slam = module.Components.OfType<SimpleKnockbacks>().Single(c => c.WatchedAction == (uint)AID.BodySlam);
        Check(slam.DestinationUnsafe(0, pc, new(0, -170)), "knockback destination inside fire is unsafe");
        var slamHints = new AIHints(); slam.AddAIHints(0, pc, default, slamHints);
        Check(slamHints.ForbiddenZones.Any(z => z.shapeDistance.Distance(pc.Position) <= 0), "BMR AI forbids a start whose knockback lands in fire");
        Check(slamHints.ForbiddenZones.All(z => z.shapeDistance.Distance(new(15, -150)) > 0), "safe lateral knockback landing stays available");
        Finish(ws, helper); Finish(ws, other);

        Cast(ws, helper, AID.FireballSpread, 6, target: pc.InstanceID);
        Check(module.Components.OfType<SpreadFromCastTargets>().Single().Spreads.Count == 1, "player spread assignment is retained");
        Finish(ws, helper);
        Check(module.Components.OfType<SpreadFromCastTargets>().Single().Spreads.Count == 0, "spread resolution clears assignment");
        Cast(ws, helper, AID.Touchdown, 6);
        Check(module.Components.OfType<SimpleKnockbacks>().Single(c => c.WatchedAction == (uint)AID.Touchdown).Casters.Count == 1, "touchdown retains radial knockback guidance");
        Finish(ws, helper);
        Cast(ws, helper, AID.SidewiseSlice, 8);
        Check(AOEs(module, pc, "SidewiseSlice").Length == 1, "unobserved Sidewise Slice remains supported");
        Finish(ws, helper);
        Cast(ws, helper, AID.DragonStomp, 2);
        Check(Hints(module, pc).PredictedDamage.Count != 0 || module.Components.OfType<RaidwideCast>().Any(c => c.Casters.Count != 0), "stomp has unavoidable damage guidance");
        Finish(ws, helper);
        var add = Create(ws, 99, (uint)OID.HordeShieldDragon, new(0, -170));
        var targets = new AIHints(); targets.PotentialTargets.Add(new(boss, 0, false)); targets.PotentialTargets.Add(new(add, 0, false));
        module.CalculateAIHints(0, pc, default, targets);
        Check(targets.PotentialTargets[1].Priority > targets.PotentialTargets[0].Priority, "live horde adds get priority over boss");
        targets.PotentialTargets[1].Priority = -2; module.CalculateAIHints(0, pc, default, targets);
        Check(targets.PotentialTargets[1].Priority == -2, "negative target safety priority is preserved");
        ws.Execute(new ActorState.OpDead(boss.InstanceID, true)); module.Update();
        Check(module.Components.Count == 0, "encounter completion clears all mechanic state");
        boss.IsDead = false; boss.CastInfo = null;
        using var retry = new Vishap(ws, boss); retry.Update();
        Check(AOEs(retry, pc, "ScorchingBreath").Length == 0 && AOEs(retry, pc, "BlazingFire").Length == 0, "fresh module never inherits warnings");
        Console.WriteLine($"Steps of Faith synthetic regression: {assertions} checks passed; geometry and live play remain separate.");
    }
}
