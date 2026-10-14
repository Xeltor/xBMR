using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A34Scathach;

static partial class Checks
{
    private static void WingsWarning()
    {
        A34Scathach Create(bool wings = false)
        {
            var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
            var boss = Actor(1, (uint)OID.Boss, new(0, -50));
            boss.IsTargetable = true;
            if (wings)
                boss.Statuses[0] = new((uint)SID.ShadowLimb, 0, DateTime.MaxValue, boss.InstanceID);
            world.Actors.Actors[boss.InstanceID] = boss;
            var module = new A34Scathach(world, boss);
            module.StateMachine.Start(Now);
            return module;
        }
        BossComponent.GlobalHints Global(BossModule module, BossComponent component)
        {
            var hints = new BossComponent.GlobalHints();
            component.AddGlobalHints(module.PrimaryActor, hints);
            return hints;
        }

        // Actual status-gain-to-hit leads for all three original7473 actions.
        // The second case is the later pattern whose exact pulse cue is unknown.
        foreach (var lead in new[] { 14.9267326, 56.5388237, 14.9487643 })
        {
            using var module = Create();
            var world = module.WorldState;
            var component = Component<GenericKnockback>(module, "ThirtySickles");
            world.Execute(new ActorState.OpStatus(module.PrimaryActor.InstanceID, 0,
                new((uint)SID.ShadowLimb, 0, DateTime.MaxValue, module.PrimaryActor.InstanceID)));
            Check(Global(module, component).SequenceEqual(["Knockback possible: keep a safe landing away from the edge"]),
                "wings status immediately supplies preparation for each recorded pulse without a guessed countdown");
            world.Frame = new(Now.AddSeconds(lead - 0.1), 0, 0, 0, 0, 1);
            Check(Global(module, component).Count == 1, "informational warning remains available before the recorded pulse, including the56.54second later interval");
            var ai = Hints(module, component, module.PrimaryActor);
            Check(component.ActiveKnockbacks(0, module.PrimaryActor).Length == 0 && ai.ForbiddenZones.Count == 0
                && ai.PredictedDamage.Count == 0 && ai.ImminentSpecialMode.mode == AIHints.SpecialMode.Normal,
                "wings alone creates no exact pulse, immediate hard avoidance or forced movement mode");
            world.Frame = new(Now.AddSeconds(lead), 0, 0, 0, 0, 1);
            world.Execute(new ActorState.OpCastEvent(module.PrimaryActor.InstanceID, Hit(7473)));
            Check(Global(module, component).Count == 1 && component.ActiveKnockbacks(0, module.PrimaryActor).Length == 0,
                "actual pulse does not fabricate a next timer or erase the current persistent wings instruction");
            world.Execute(new ActorState.OpStatus(module.PrimaryActor.InstanceID, 0, default));
            Check(Global(module, component).Count == 0, "actual status removal immediately retires possible-knockback guidance");
        }

        using var bootstrap = Create(true);
        var recovered = Component<GenericKnockback>(bootstrap, "ThirtySickles");
        Check(Global(bootstrap, recovered).Count == 1 && recovered.ActiveKnockbacks(0, bootstrap.PrimaryActor).Length == 0,
            "mid-window module bootstrap reads existing wings without restarting a pulse countdown");
        bootstrap.WorldState.Execute(new ActorState.OpDead(bootstrap.PrimaryActor.InstanceID, true));
        Check(Global(bootstrap, recovered).Count == 0, "boss death removes current-status preparation even before the status-loss packet");
        bootstrap.WorldState.Execute(new ActorState.OpDead(bootstrap.PrimaryActor.InstanceID, false));
        bootstrap.WorldState.Execute(new ActorState.OpTargetable(bootstrap.PrimaryActor.InstanceID, false));
        Check(Global(bootstrap, recovered).Count == 0, "untargetable cocoon phase does not claim an active-wing knockback response");
        bootstrap.WorldState.Execute(new ActorState.OpTargetable(bootstrap.PrimaryActor.InstanceID, true));
        bootstrap.WorldState.Execute(new ActorState.OpCreate(bootstrap.PrimaryActor.InstanceID, (uint)OID.Boss, 0, 0, "", 0,
            ActorType.Enemy, Class.None, 60, new(0, 0, -50, 0), 5, default, true, false, 0, 0, 0));
        Check(Global(bootstrap, recovered).Count == 0, "old primary generation cannot retain its wings warning after native actor replacement");
        using var reset = Create();
        Check(Global(reset, Component<GenericKnockback>(reset, "ThirtySickles")).Count == 0,
            "new module without current wings contains no preparation inherited from another pull");
    }
}
