using BossMod;

static partial class Checks
{
    private static void GateIntermissionLifecycle()
    {
        var world = World();
        Actor Create(ulong id, uint oid, bool targetable = true)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
                new(-350, 1, -445, 0), 3.2f, new(100, 100, 0, 100, 100), targetable, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        var first = Create(90, 6410, false);
        var primary = Create(1, 6407);
        var player = Actor(80, 0, new(-350, -445), Class.SGE);
        Member(world, player, 0);
        using var module = new BossMod.Heavensward.Alliance.A35Diabolos.A35Diabolos(world, primary);
        module.StateMachine.Start(Now);
        var component = Component<BossComponent>(module, "GateIntermission");
        BossComponent.GlobalHints Guidance()
        {
            BossComponent.GlobalHints hints = [];
            component.AddGlobalHints(player, hints);
            return hints;
        }
        void Start(Actor gate, float elapsed = 0)
            => world.Execute(new ActorState.OpCastInfo(gate.InstanceID, new()
            {
                Action = new(ActionType.Spell, 7188), TotalTime = 8.7f, ElapsedTime = elapsed,
                Location = new(-350, 1, -445)
            }));
        Check(Guidance().Single().Contains("forming"), "bootstrap warns from the existing gate before it becomes targetable");
        world.Execute(new ActorState.OpTargetable(first.InstanceID, true));
        Check(Guidance().Single().Contains("before they summon"), "native targetability turns preparation into actionable gate destruction");
        var preparation = new AIHints();
        preparation.PotentialTargets.Add(new(first, 0, false));
        module.CalculateAIHints(0, player, default, preparation);
        Check(preparation.PotentialTargets[0].Priority == 2, "targetable gate is prioritized before its summon cast starts");
        Start(first);
        var second = Create(91, 6410);
        Start(second, 1);
        Check(Guidance().Count == 2 && Guidance().Any(h => h.Contains("8.0s")), "simultaneous gates expose the earliest actual summon finish");
        second.CastInfo!.ElapsedTime = 2;
        Check(Guidance().Any(h => h.Contains("7.0s")), "summon urgency follows native cast progress without a spawn timer");
        world.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(Guidance().Any(h => h.Contains("9.0s")), "one cancellation keeps the other gate's live summon warning");
        world.Execute(new ActorState.OpDead(first.InstanceID, true));
        Check(Guidance().Count == 1 && Guidance().All(h => !h.Contains("Void Call")), "dying source stops summon urgency without hiding another live gate");
        world.Execute(new ActorState.OpDestroy(second.InstanceID));
        Check(Guidance().Count == 0, "last live gate removal clears group guidance even if a dead source retains CastInfo");

        var old = first;
        first = Create(first.InstanceID, 6410);
        Start(first, 3);
        component.OnActorDestroyed(old);
        Check(Guidance().Any(h => h.Contains("6.0s")), "late old-generation destruction cannot erase a replacement's current cast");
        var lifegate = Create(92, 6412);
        Check(Guidance().Any(h => h.Contains("health refills")), "Lifegate appearance supplies the intermission response without guessing wave timing");
        world.Execute(new ActorState.OpHPMP(lifegate.InstanceID, new(0, 200, 0, 100, 100)));
        world.Execute(new ActorState.OpHPMP(lifegate.InstanceID, new(200, 200, 0, 100, 100)));
        Check(Guidance().Count(h => h.Contains("Lifegate")) == 1, "native health refill preserves one current Lifegate instruction");

        var later = Create(94, 6410);
        Start(later, 1);
        AIHints Priorities(int firstPriority = 0)
        {
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(first, firstPriority, false));
            hints.PotentialTargets.Add(new(later, 0, false));
            hints.PotentialTargets.Add(new(lifegate, 0, false));
            module.CalculateAIHints(0, player, default, hints);
            return hints;
        }
        var timing = Priorities();
        Check(timing.PotentialTargets.Select(e => e.Priority).SequenceEqual([3, 2, 1]),
            "actual earlier summon deadline guides both AIs ahead of another gate and Lifegate");
        later.CastInfo!.ElapsedTime = 5;
        timing = Priorities();
        Check(timing.PotentialTargets.Select(e => e.Priority).SequenceEqual([2, 3, 1]),
            "urgency follows current native remaining times instead of actor order or spawn order");
        timing = Priorities(-4);
        Check(timing.PotentialTargets[0].Priority == -4 && timing.PotentialTargets[1].Priority == 3,
            "a forbidden earlier target cannot prevent prioritizing an eligible summon cast");
        world.Execute(new ActorState.OpDestroy(later.InstanceID));

        foreach (var priority in new[] { 0, 7, -1, -2, -3, -4 })
        {
            var hints = new AIHints();
            hints.PotentialTargets.Add(new(first, priority, false));
            hints.PotentialTargets.Add(new(lifegate, priority, false));
            module.CalculateAIHints(0, player, default, hints);
            Check(hints.PotentialTargets[0].Priority == (priority >= 0 ? Math.Max(3, priority) : priority)
                && hints.PotentialTargets[1].Priority == (priority >= 0 ? Math.Max(1, priority) : priority),
                "gate priorities preserve higher and negative ownership: " + priority);
        }
        var ai = Hints(module, component, 0, player);
        Check(ai.ForbiddenZones.Count == 0 && ai.GoalZones.Count == 0 && ai.PredictedDamage.Count == 0 && ai.ForcedMovement == null,
            "intermission guidance invents no hazard footprint, damage forecast or forced movement");
        module.StateMachine.Reset();
        module.StateMachine.Start(world.CurrentTime);
        component = Component<BossComponent>(module, "GateIntermission");
        Check(Guidance().Any(h => h.Contains("6.0s")), "mid-cast phase restart recovers current gate progress rather than restarting a countdown");
        Create(first.InstanceID, 0x19A);
        world.Execute(new ActorState.OpDestroy(lifegate.InstanceID));
        Check(Guidance().Count == 0, "unrelated replacement and Lifegate removal leave no stale intermission state");
        Start(Create(93, 6410));
        module.StateMachine.Reset();
        Check(module.CalculateGlobalHints(player).Count == 0, "inactive phase has no intermission warnings");
    }
}
