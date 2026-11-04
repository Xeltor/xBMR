namespace BossMod.Stormblood.Alliance.A12Hashmal;

class PennantstoneGolems(BossModule module) : Components.RaidwideCast(module, (uint)AID.Demolish, "Demolish incoming. Kill the golems and prepare for raid damage.")
{
    private bool _summoning;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.Summon)
            _summoning = true;
        else if (caster.OID == (uint)OID.PennantstoneGolem)
            base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.Summon)
            _summoning = false;
        base.OnCastFinished(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == (uint)OID.PennantstoneGolem && spell.IsSpell(AID.Demolish))
        {
            base.OnEventCast(caster, spell);
            Casters.Remove(caster);
        }
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
        {
            _summoning = false;
            Casters.Clear();
        }
        else
        {
            Casters.Remove(actor);
        }
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        base.AddGlobalHints(actor, hints);
        if (!Active && (_summoning || Module.Enemies((uint)OID.PennantstoneGolem).Any(golem => !golem.IsDeadOrDestroyed)))
            hints.Add("Tanks pick up the Pennantstone Golems. Defeat them before Demolish grows stronger.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        // A targetable summon can be passive until the first tank engages it.
        // Preserve hard exclusions, pending deaths, and targets outside this arena.
        var golems = hints.PotentialTargets.Where(enemy => (enemy.Priority >= 0 || enemy.Priority == AIHints.Enemy.PriorityUndesirable)
            && enemy.Actor.OID == (uint)OID.PennantstoneGolem && enemy.Actor.IsTargetable && !enemy.Actor.IsDeadOrDestroyed
            && Arena.InBounds(enemy.Actor.Position)).OrderBy(enemy => enemy.Actor.InstanceID).ToArray();
        if (golems.Length == 0)
            return;

        foreach (var golem in golems)
            golem.Priority = 2;
        if (actor.Role == Role.Tank)
            AssignPickups(actor, golems);

        var priority = golems.Max(golem => golem.Priority);
        var attackRange = actor.Role is Role.Tank or Role.Melee ? 3f : 24.5f;
        var approaches = golems.Where(golem => golem.Priority == priority)
            .Select(golem => (golem.Actor.Position, Range: golem.Actor.HitboxRadius + attackRange)).ToArray();
        hints.GoalZones.Add(position => approaches.Any(approach => position.InCircle(approach.Position, approach.Range)) ? 5f : 0f);
    }

    private void AssignPickups(Actor actor, AIHints.Enemy[] golems)
    {
        var bossTank = Module.PrimaryActor.TargetID;
        var tanks = Raid.WithoutSlot(excludeNPCs: true).Where(tank => tank.Role == Role.Tank && !tank.IsDeadOrDestroyed && Arena.InBounds(tank.Position))
            .OrderBy(tank => golems.Count(golem => golem.Actor.TargetID == tank.InstanceID))
            .ThenBy(tank => tank.InstanceID).ToArray();
        var offTanks = tanks.Where(tank => tank.InstanceID != bossTank).ToArray();
        var pickupTanks = offTanks.Length != 0 ? offTanks : tanks;
        if (pickupTanks.Length == 0)
            return;

        var nextPickup = 0;
        foreach (var golem in golems)
        {
            var holder = tanks.FirstOrDefault(tank => tank.InstanceID == golem.Actor.TargetID);
            var owner = holder ?? pickupTanks[nextPickup++ % pickupTanks.Length];
            golem.ShouldBeTanked = owner == actor;
            golem.PreferProvoking = owner == actor && holder == null;
            if (golem.PreferProvoking)
                golem.Priority = 3;
        }
    }
}
