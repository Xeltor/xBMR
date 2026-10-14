using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A34Scathach;

static partial class Checks
{
    private static void HoundVariants()
    {
        using var module = Module();
        var world = module.WorldState;
        Actor Hound(ulong id, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, (uint)OID.ShadowcourtHound, 0, 0, "", 0, ActorType.Enemy,
                Class.None, 60, new(position.X, 0, position.Z, 0), 4.44f, default, true, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        var first = Hound(70, module.Center + new WDir(-10, 0));
        var second = Hound(71, module.Center + new WDir(10, 0));
        var ram = Component<CastHint>(module, "TheRamsVoice");
        var firstRam = Cast(3343, first.Position, 2.7f);
        firstRam.Interruptible = true;
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, firstRam));
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(3343, second.Position, 2.7f)));
        var global = new BossComponent.GlobalHints();
        ram.AddGlobalHints(module.PrimaryActor, global);
        Check(ram.Casters.Count == 2 && global.Single().Contains("move away") && global.Single().Contains("interrupt"),
            "known Hound Ram's Voice variant gets an actionable instruction from native cast start");
        var ai = Hints(module, ram, module.PrimaryActor);
        Check(ai.ForbiddenZones.Count == 0 && ai.PredictedDamage.Count == 0,
            "Ram's Voice instruction does not assert an uncalibrated hard radius or unavoidable damage");
        ram.OnCastStarted(first, first.CastInfo!);
        Check(ram.Casters.Count == 2, "duplicate Ram's Voice callback does not retain an extra source after cancellation");
        var eligible = new AIHints.Enemy(first, 0, true);
        var interruptHints = new AIHints();
        interruptHints.PotentialTargets.Add(eligible);
        module.CalculateAIHints(0, module.PrimaryActor, default, interruptHints);
        Check(eligible.ShouldBeInterrupted, "native interruptible Ram's Voice uses the existing eligibility-gated Hound interrupt request");
        world.Execute(new ActorState.OpCastEvent(first.InstanceID, Hit(3343)));
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(ram.Casters.Count == 1 && ram.Casters[0] == second, "first Ram's Voice resolution leaves the other helper's cast instruction");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(!ram.Active, "native cancelled Ram's Voice clears its instruction without a damage event");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(3343, first.Position, 2.7f)));
        var replacement = Hound(first.InstanceID, first.Position);
        world.Execute(new ActorState.OpCastInfo(replacement.InstanceID, Cast(3343, replacement.Position, 2.7f)));
        ram.OnActorDestroyed(first);
        Check(ram.Casters.Count == 1 && ram.Casters[0] == replacement, "late old-generation cleanup preserves replacement Hound warning");
        world.Execute(new ActorState.OpDestroy(replacement.InstanceID));
        Check(!ram.Active, "native Hound destruction clears the current Ram's Voice source");

        first = Hound(70, module.Center + new WDir(-10, 0));
        foreach (var (aid, name) in new[] { (3342u, "MarrowDrain1"), (3340u, "MarrowDrain2"), (3341u, "MarrowDrain3") })
        {
            var cone = Component<SimpleAOEs>(module, name);
            world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(aid, first.Position, 2.7f, 30.Degrees())));
            world.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(aid, second.Position, 2.7f, 210.Degrees())));
            Check(cone.Casters.Count == 2 && cone.Casters.All(a => a.Shape is AOEShapeCone { Radius: 10.44f }),
                name + " reuses the exact known range6+hound-radius cone family for both native sources");
            Check(cone.Casters.All(a => a.Activation == module.CastFinishAt(first.CastInfo)), name + " exposes corrected future cast deadlines");
            Check(Hints(module, cone, module.PrimaryActor).ForbiddenZones.Count == 2, name + " supplies both native timed hazards");
            cone.OnCastStarted(first, first.CastInfo!);
            Check(cone.Casters.Count == 2, name + " duplicate callback preserves exactly one hazard per source");
            world.Execute(new ActorState.OpCastEvent(first.InstanceID, Hit(aid)));
            world.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
            Check(cone.Casters.Count == 1 && cone.Casters[0].ActorID == second.InstanceID, name + " actual hit retires only its helper");
            world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
            Check(cone.Casters.Count == 0, name + " native cast cancellation clears the remaining hazard");
            world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(aid, first.Position, 2.7f)));
            world.Execute(new ActorState.OpDestroy(first.InstanceID));
            Check(cone.Casters.Count == 0, name + " native caster destruction clears its hazard");
            first = Hound(70, module.Center + new WDir(-10, 0));
        }
        var dragon = Component<CastHint>(module, "TheDragonsVoice");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(3344, first.Position, 4.2f)));
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(3344, second.Position, 4.2f)));
        dragon.OnCastStarted(first, first.CastInfo!);
        var dragonGlobal = new BossComponent.GlobalHints();
        dragon.AddGlobalHints(module.PrimaryActor, dragonGlobal);
        Check(dragon.Casters.Count == 2 && dragonGlobal.Single().Contains("move close"),
            "native simultaneous Dragon's Voices preserve the opposite response without duplicate sources");
        var dragonAI = Hints(module, dragon, module.PrimaryActor);
        Check(dragonAI.ForbiddenZones.Count == 0 && dragonAI.PredictedDamage.Count == 0,
            "Dragon's Voice preserves its unknown safe inner area rather than forbidding the entire room");
        world.Execute(new ActorState.OpCastEvent(first.InstanceID, Hit(3344)));
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(dragon.Casters.Count == 1 && dragon.Casters[0] == second, "Dragon's Voice resolution only retires its own helper");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(!dragon.Active, "native interrupted Dragon's Voice cancels its instruction");
        world.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(3344, first.Position, 4.2f)));
        world.Execute(new ActorState.OpDestroy(first.InstanceID));
        Check(!dragon.Active, "native Dragon's Voice source destruction clears its instruction");
        using var reset = Module();
        Check(!Component<CastHint>(reset, "TheRamsVoice").Active && !Component<CastHint>(reset, "TheDragonsVoice").Active && Component<SimpleAOEs>(reset, "MarrowDrain3").Casters.Count == 0,
            "new module reset has no previous Hound warning or cone");
    }
}
