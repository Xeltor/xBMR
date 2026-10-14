using BossMod;
using BossMod.Heavensward.Alliance.A34Scathach;

static partial class Checks
{
    private static void UnhandWarning()
    {
        var first = Actor(30, (uint)OID.ShadowLimb, new(-14, -58));
        var second = Actor(31, (uint)OID.ShadowLimb, new(-4, -61));
        using var module = Module(first, second);
        var warning = Component<BossComponent>(module, "Unhand");
        string Text()
        {
            var global = new BossComponent.GlobalHints();
            warning.AddGlobalHints(module.PrimaryActor, global);
            return string.Join(" ", global);
        }
        module.WorldState.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(7461, first.Position, 4.7f)));
        Check(Text().Contains("Unhand") && Text().Contains("5.0s") && Text().Contains("eligibility unverified"),
            "actual Unhand cast gives its corrected early deadline without claiming a measured recipient boundary");
        var hints = Hints(module, warning, module.PrimaryActor);
        Check(hints.ForbiddenZones.Count == 0 && hints.PredictedDamage.Count == 0,
            "Unhand warning does not invent a60y avoidance or universally affected alliance mask");
        module.WorldState.Frame = new(Now.AddSeconds(0.5), 0, 0, 0, 0, 1);
        warning.OnCastStarted(first, first.CastInfo!);
        Check(Text().Contains("4.5s"), "duplicate Unhand callback preserves the original deadline");
        module.WorldState.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        module.WorldState.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(7461, second.Position, 4.7f)));
        module.WorldState.Frame = new(Now.AddSeconds(4.98), 0, 0, 0, 0, 1);
        module.WorldState.Execute(new ActorState.OpDead(first.InstanceID, true));
        Check(Text().Contains("0.0s"), "native limb death immediately before damage retains its already committed warning");
        module.WorldState.Frame = new(Now.AddSeconds(5), 0, 0, 0, 0, 1);
        module.WorldState.Execute(new ActorState.OpCastEvent(first.InstanceID, Hit(7461)));
        module.WorldState.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(Text().Contains("1.0s"), "first Unhand action and cast-end retire only that source, retaining the second deadline");
        module.WorldState.Execute(new ActorState.OpCastInfo(second.InstanceID, null));
        Check(Text().Length == 0, "native cancellation of the other source clears its own warning");
        foreach (var cleanup in new[] { "despawn", "phase", "stale" })
        {
            using var fresh = Module();
            var component = Component<BossComponent>(fresh, "Unhand");
            var limb = Actor(50, (uint)OID.ShadowLimb, fresh.Center);
            component.OnCastStarted(limb, Cast(7461, limb.Position, 4.7f));
            switch (cleanup)
            {
                case "despawn": component.OnActorDestroyed(limb); break;
                case "phase": component.OnActorUntargetable(fresh.PrimaryActor); break;
                case "stale": fresh.WorldState.Frame = new(Now.AddSeconds(8), 0, 0, 0, 0, 1); break;
            }
            var global = new BossComponent.GlobalHints();
            component.AddGlobalHints(fresh.PrimaryActor, global);
            Check(global.Count == 0, cleanup + " removes obsolete source-bound Unhand warning");
        }
        using var reset = Module();
        var resetHints = new BossComponent.GlobalHints();
        Component<BossComponent>(reset, "Unhand").AddGlobalHints(reset.PrimaryActor, resetHints);
        Check(resetHints.Count == 0, "fresh pull contains no old limb deadline");
    }
}
