using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A34Scathach;

static partial class Checks
{
    private static void TowerLifecycle()
    {
        using var module = Module();
        var world = module.WorldState;
        var towers = Component<GenericTowers>(module, "ParticleBeam");
        Actor Create(ulong id, uint oid, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.EventObj, Class.None, 0,
                new(position.X, 0, position.Z, 0), 0, default, false, true, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        var first = Create(50, (uint)OID.MeteorTower, module.Center + new WDir(20, 0));
        var originalDeadline = towers.Towers.Single().Activation;
        world.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        towers.OnActorCreated(first);
        Check(towers.Towers.Single().Activation == originalDeadline, "native tower generation duplicate preserves its impact deadline");
        var replacement = Create(50, (uint)OID.MeteorTower, module.Center + new WDir(-20, 0));
        Check(towers.Towers.Count == 1 && towers.Towers[0].Position == replacement.Position
            && towers.Towers[0].Activation == Now.AddSeconds(10.8), "replacement tower gets its own location and future deadline");
        towers.OnActorDestroyed(first);
        Check(towers.Towers.Count == 1, "late destruction of the old generation cannot erase the replacement tower");
        Create(50, 0x19A, module.Center);
        Check(towers.Towers.Count == 0, "replacement by a non-tower immediately removes the obsolete tower contract");
        var missingHit = Create(51, (uint)OID.MeteorTower, module.Center);
        world.Frame = new(Now.AddSeconds(13), 0, 0, 0, 0, 1);
        towers.Update();
        Check(towers.Towers.Count == 0, "missing tower hit expires bounded guidance while the event object remains present");
        towers.OnActorCreated(missingHit);
        Check(towers.Towers.Count == 0, "duplicate source after expiry cannot restart an obsolete tower deadline");
        var current = Create(52, (uint)OID.MeteorTower, module.Center);
        world.Execute(new ActorState.OpDestroy(current.InstanceID));
        Check(towers.Towers.Count == 0, "native destruction clears the current generation immediately");

        var existing = Actor(60, (uint)OID.MeteorTower, new(20, -50));
        using var bootstrap = Module(existing);
        var recovered = Component<GenericTowers>(bootstrap, "ParticleBeam");
        Check(recovered.Towers.Single().Activation == Now, "existing tower bootstrap does not invent another9.8seconds before impact");
        var global = new BossComponent.GlobalHints();
        recovered.AddGlobalHints(bootstrap.PrimaryActor, global);
        Check(global.Single().Contains("soak now") && global.Single().Contains("timing unavailable"), "bootstrap tower warning exposes the unavailable original timing");
        var ai = Hints(bootstrap, recovered, bootstrap.PrimaryActor);
        Check(ai.ForbiddenZones.Single().activation == Now, "AI consumes bootstrap tower guidance immediately instead of arriving after the unknown impact");
        bootstrap.WorldState.Frame = new(Now.AddSeconds(3), 0, 0, 0, 0, 1);
        recovered.Update();
        Check(recovered.Towers.Count == 1, "unknown-age bootstrap guidance is retained across the possible remaining original tower lifetime");
        recovered.OnEventCast(bootstrap.PrimaryActor, Hit(7464, existing.Position));
        Check(recovered.Towers.Count == 0, "actual location-matched impact retires the unknown-age bootstrap tower");
        using var reset = Module();
        Check(Component<GenericTowers>(reset, "ParticleBeam").Towers.Count == 0, "fresh module reset has no inherited tower source or timing");
    }
}
