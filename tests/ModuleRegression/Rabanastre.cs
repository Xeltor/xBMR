using BossMod;
using BossMod.Components;

static partial class Regression
{
    private static void RabanastreTowers()
    {
        const string prefix = "Stormblood.Alliance.A12Hashmal.";
        var module = Module(prefix + "A12Hashmal", new(-320, -46));
        var component = Component<GenericAOEs>(module, prefix + "Towerfall");
        var first = Actor(1, 0x1EA7DF, new(-320, -57));
        var second = Actor(2, 0x1EA7DF, new(-310.474f, -40.5f));
        component.OnActorCreated(first);
        component.OnActorCreated(first);
        component.OnActorCreated(second);
        var aoes = component.ActiveAOEs(0, first);
        Check(aoes.Length == 2, "tower predictions are distinct and duplicate spawn is idempotent");
        Check(aoes[0].Activation == Now.AddSeconds(9), "fall direction gives nine seconds of advance warning");
        Check(aoes[0].Check(first.Position + new WDir(4.9f, 20)) && !aoes[0].Check(first.Position + new WDir(5.1f, 20)), "tower full width is ten, with safe space on either side");
        var hints = new AIHints();
        component.AddAIHints(0, first, default, hints);
        Check(hints.ForbiddenZones.Count == 2, "both towers constrain AI before their hits");
        component.OnEventCast(Actor(3, 0x18D6, second.Position), Effect(9668));
        component.OnEventCast(Actor(3, 0x18D6, second.Position), Effect(9668));
        aoes = component.ActiveAOEs(0, first);
        Check(aoes.Length == 1 && aoes[0].ActorID == first.InstanceID, "out-of-order and duplicate helper hits cannot clear another tower");
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, first).Length == 0, "tower disappearance clears its own prediction");
    }

    private static void RabanastreCasts()
    {
        foreach (var (moduleName, componentName, action, center, inside, outside) in new (string, string, uint, WPos, WDir, WDir)[]
        {
            ("A12Hashmal", "ExtremeEdge", 9680, new(-320, -46), new(17.9f, 20), new(18.1f, 20)),
            ("A13Rofocale", "Maverick", 9865, new(106, -191), new(7.9f, 20), new(8.1f, 20)),
            ("A14Argath", "Heartless", 9778, new(106, -386), new(5.4f, 20), new(5.6f, 20))
        })
        {
            var prefix = "Stormblood.Alliance." + moduleName + ".";
            var module = Module(prefix + moduleName, center);
            var component = Component<GenericAOEs>(module, prefix + componentName);
            var caster = Actor(1, 0x18D6, center);
            var cast = Cast(action, center);
            component.OnCastStarted(caster, cast);
            var aoes = component.ActiveAOEs(0, caster);
            Check(aoes.Length == 1 && aoes[0].Check(center + inside) && !aoes[0].Check(center + outside), componentName + " preserves both sides of its width boundary");
            var hints = new AIHints();
            component.AddAIHints(0, caster, default, hints);
            Check(hints.ForbiddenZones.Count == 1, componentName + " publishes a timed AI constraint");
            component.OnCastFinished(caster, cast);
            Check(component.ActiveAOEs(0, caster).Length == 0, componentName + " retires on cast finish, including interruption");
            if (componentName is "ExtremeEdge" or "Maverick")
            {
                component.OnCastStarted(caster, Cast(componentName == "ExtremeEdge" ? 9678u : 10022u, center));
                Check(component.ActiveAOEs(0, caster).Length == 0, componentName + " leaves unobserved placeholder variants unimplemented");
            }
        }
    }

    private static void RabanastreArenas()
    {
        foreach (var (name, oid, center, inside, outside, count) in new (string, uint, WPos, WDir, WDir, int)[]
        {
            ("A12Hashmal", 0x1F83, new(-320, -46), new(29, 0), new(30, 0), 5),
            ("A13Rofocale", 0x1FAF, new(106, -191), new(0, 29), new(0, 30), 3),
            ("A14Argath", 0x1F97, new(106, -386), new(23, 23), new(25, 0), 5)
        })
        {
            var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
            var boss = Actor(1, oid, center);
            using var module = (BossModule)Activator.CreateInstance(Bmr.GetType("BossMod.Stormblood.Alliance." + name + "." + name, true)!, world, boss)!;
            Check(module.Info != null, name + " is registered by primary OID");
            Check(module.Arena.Center == center && module.Arena.InBounds(center + inside) && !module.Arena.InBounds(center + outside), name + " has the authored movement bounds, including square corners");
            module.StateMachine.Start(Now);
            Check(module.Components.Count == count, name + " state machine activates its actual component factories");
        }
    }
}
