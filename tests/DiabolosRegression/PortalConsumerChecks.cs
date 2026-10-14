using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Checks
{
    private static void PortalConsumerPolicy()
    {
        using var module = Hollow();
        var player = Actor(80, 0, new(350, -445), Class.PLD);
        player.PosRot.Y = -300;
        Member(module.WorldState, player, 0);
        var requested = new Vector3(0.25f, 0, -0.5f);
        var hints = new AIHints { ForcedMovement = requested };
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.AutomaticMovementPaused && hints.EffectiveForcedMovement == null,
            "portal policy withholds a direction supplied before encounter hints");
        Check(hints.ForcedMovement == requested && !hints.SpecialModeBlocksManualMovement,
            "withholding automated direction preserves its request and physical manual input policy");
        hints.ForcedMovement = -requested;
        Check(hints.EffectiveForcedMovement == null,
            "a later movement producer cannot bypass the portal hold at final direction consumption");

        // Direct return and destroy/recreate return are both present in the original.
        player.PosRot = new(module.Center.X, 1, module.Center.Z, 0);
        hints.Clear();
        hints.ForcedMovement = requested;
        module.CalculateAIHints(0, player, default, hints);
        Check(!hints.AutomaticMovementPaused && hints.EffectiveForcedMovement == requested,
            "ordinary return restores automatic movement after fresh-frame hint clearing");
        player.PosRot = new(350, -300, -445, 0);
        hints.Clear();
        module.CalculateAIHints(0, player, default, hints);
        Check(hints.AutomaticMovementPaused, "replacement return fixture begins with an actual portal hold");
        module.WorldState.Execute(new ActorState.OpDestroy(player.InstanceID));
        module.WorldState.Execute(new ActorState.OpCreate(player.InstanceID, 0, 0, 0, "", 0, ActorType.Player, Class.PLD, 60,
            new(module.Center.X, 1, module.Center.Z, 0), 0.5f, new(100, 100, 0, 100, 100), true, false, 0, 0, 0));
        player = module.WorldState.Actors.Find(player.InstanceID)!;
        hints.Clear();
        hints.ForcedMovement = requested;
        module.CalculateAIHints(0, player, default, hints);
        Check(!hints.AutomaticMovementPaused && hints.EffectiveForcedMovement == requested,
            "native destroy/recreate on the main floor cannot inherit the old portal hold");

        hints.ImminentSpecialMode = (AIHints.SpecialMode.NoMovement, Now, default);
        hints.NoMovementAllowsManualInput = false;
        Check(!hints.AutomaticMovementPaused && hints.EffectiveForcedMovement == requested && hints.SpecialModeBlocksManualMovement,
            "ordinary NoMovement mechanics retain their existing consumer and manual-block policy");
        hints.NoMovementAllowsManualInput = true;
        hints.ImminentSpecialMode = (AIHints.SpecialMode.Pyretic, Now, default);
        Check(!hints.AutomaticMovementPaused && hints.SpecialModeBlocksManualMovement,
            "portal automation exemption cannot disable real Pyretic handling");
    }

    private static void TowerGenerationPolicy()
    {
        using var module = Hollow();
        var world = module.WorldState;
        var tankPosition = module.Center + new WDir(20, 0);
        var healer = Actor(80, 0, tankPosition, Class.SGE);
        var tank = Actor(81, 0, tankPosition, Class.PLD);
        var corpse = Actor(82, 0, module.Center, Class.DRG);
        Member(world, healer, 0);
        Member(world, tank, 1);
        Member(world, corpse, 2);
        world.Execute(new ActorState.OpDead(corpse.InstanceID, true));
        var towers = Component<GenericTowers>(module, "ParticleBeam");
        Actor CreateTower(ulong id, uint oid, WPos position)
        {
            world.Execute(new ActorState.OpCreate(id, oid, 0, 0, "", 0, ActorType.EventObj, Class.None, 60,
                new(position.X, 1, position.Z, 0), 2, new(100, 100, 0, 100, 100), true, false, 0, 0, 0));
            return world.Actors.Find(id)!;
        }
        var source = CreateTower(90, 2007631, tankPosition); // exact TankTower OID0x1EA24F
        var outer = CreateTower(91, 2007630, module.Center); // exact OuterTower OID0x1EA24E
        towers.Update();
        var tankTower = towers.Towers.Single(t => t.ActorID == source.InstanceID);
        var outerTower = towers.Towers.Single(t => t.ActorID == outer.InstanceID);
        Check(tankTower.ForbiddenSoakers[0] && !tankTower.ForbiddenSoakers[1],
            "off-centre tank source retains actual OID role assignment after Update");
        Check(!outerTower.ForbiddenSoakers[0], "outer source at centre cannot be reclassified as a tank tower");
        var personal = new BossComponent.TextHints();
        towers.AddHints(0, healer, personal);
        Check(personal.Any(h => h.Item1 == "Leave the tank tower!"),
            "manual instruction follows tank source identity rather than its location");
        healer.PosRot = new(module.Center.X, 1, module.Center.Z, 0);
        personal.Clear();
        towers.AddHints(0, healer, personal);
        Check(personal.Any(h => h.Item1 == "Soak the tower!") && !personal.Any(h => h.Item1 == "Leave the tank tower!"),
            "non-tank can soak the actual outer tower even when it is centred");
        var damage = Hints(module, towers, 0, healer).PredictedDamage.Single();
        Check(damage.Players[0] && damage.Players[1] && !damage.Players[2],
            "predicted soak damage includes live eligible occupants and excludes the corpse");
        Check(towers.Towers.Single(t => t.ActorID == outer.InstanceID).NumInside(module) == 1,
            "predicted recipients agree with tower occupancy's exclusion of dead players");

        world.Frame = new(Now.AddSeconds(1), 0, 0, 0, 0, 1);
        towers.OnActorCreated(source);
        Check(towers.Towers.Single(t => t.ActorID == source.InstanceID).Activation == Now.AddSeconds(9.7),
            "duplicate creation of the same generation preserves its original deadline");
        var replacementPosition = module.Center + new WDir(-20, 0);
        var replacement = CreateTower(source.InstanceID, 2007630, replacementPosition);
        var replaced = towers.Towers.Single(t => t.ActorID == source.InstanceID);
        Check(replaced.Position == replacementPosition && replaced.Activation == Now.AddSeconds(10.7) && !replaced.ForbiddenSoakers[0],
            "native replacement obtains fresh geometry, deadline and actual outer-tower role");
        towers.OnActorDestroyed(source);
        Check(towers.Towers.Any(t => t.ActorID == replacement.InstanceID),
            "stale destruction of the old actor cannot remove the replacement tower");
        world.Execute(new ActorState.OpDestroy(replacement.InstanceID));
        Check(towers.Towers.All(t => t.ActorID != replacement.InstanceID),
            "native destruction of the current actor clears its tower");
    }
}
