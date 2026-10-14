using BossMod;
using BossMod.Components;

static partial class Checks
{
    private static void HollowTowerBootstrap()
    {
        var world = World();
        var center = new WPos(-350, -445);
        var existing = Actor(60, 0x1EA24F, center);
        world.Actors.Actors[existing.InstanceID] = existing;
        var tank = Actor(61, 0, center + new WDir(10, 0), Class.PLD);
        Member(world, tank, 0);
        using var module = Hollow(world);
        var towers = Component<GenericTowers>(module, "ParticleBeam");
        Check(towers.Towers.Single().Activation == Now, "Hollow tower bootstrap cannot restart the original9.7second countdown");
        var global = new BossComponent.GlobalHints();
        towers.AddGlobalHints(tank, global);
        Check(global.Single().Contains("soak now") && global.Single().Contains("timing unavailable"), "unknown-age Hollow tower gives immediate explicit guidance");
        var ai = Hints(module, towers, 0, tank);
        Check(ai.ForbiddenZones.Single().activation == Now, "tank's bootstrap arrival constraint is immediate");
        var healer = Actor(62, 0, center, Class.WHM);
        Member(world, healer, 8);
        towers.Update();
        Check(towers.Towers.Single().ForbiddenSoakers[8], "bootstrap preserves tank-only eligibility across alliances");
        world.Frame = new(Now.AddSeconds(3), 0, 0, 0, 0, 1);
        towers.Update();
        Check(towers.Towers.Count == 1, "bootstrap tower remains active through its possible remaining original lifetime");
        towers.OnEventCast(module.PrimaryActor, Hit(7207, center));
        Check(towers.Towers.Count == 0, "actual Hollow impact clears the unknown-age bootstrap contract");
        towers.OnActorCreated(existing);
        Check(towers.Towers.Count == 0, "duplicate source after actual impact cannot restart the old tower");

        world.Execute(new ActorState.OpCreate(63, 0x1EA24E, 0, 0, "", 0, ActorType.EventObj, Class.None, 0,
            new(-330, 1, -445, 0), 0, default, false, true, 0, 0, 0));
        Check(towers.Towers.Single().Activation == world.FutureTime(9.7), "new native source after bootstrap retains the ordinary future activation");
        world.Execute(new ActorState.OpCreate(63, 0x19A, 0, 0, "", 0, ActorType.Enemy, Class.None, 60,
            new(-330, 1, -445, 0), 0, default, true, false, 0, 0, 0));
        Check(towers.Towers.Count == 0, "non-tower replacement removes old Hollow tower semantics immediately");
    }
}
