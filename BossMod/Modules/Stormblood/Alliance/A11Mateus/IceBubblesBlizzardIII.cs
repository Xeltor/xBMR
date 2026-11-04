namespace BossMod.Stormblood.Alliance.A11Mateus;

class IceBubbleBlizzardIIITowers(BossModule module) : Components.GenericTowers(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (SelectTower(actor) is not { } tower)
            return;

        if (tower.MinSoakers == 1)
            hints.Add("Step into a water orb.", !tower.IsInside(actor));
        else if (tower.IsInside(actor))
            hints.Add("Stay in the water spout until it clears.", false);
        else
            hints.Add("Fill the water spout with three players.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (SelectTower(actor) is { } tower)
            hints.GoalZones.Add(AIHints.GoalProximity(tower.Position, Math.Max(8f, actor.DistanceToPoint(tower.Position) + 2f), 12f));
    }

    private Tower? SelectTower(Actor actor)
    {
        if (Towers.Count == 0 || actor.IsDeadOrDestroyed)
            return null;

        var spiral = Module.FindComponent<IceSpiral>();
        var objectives = Towers.Select(tower => (Tower: tower, Passage: spiral?.EstimatedPassage(tower.Position, ((AOEShapeCircle)tower.Shape).Radius))).ToArray();
        foreach (var (tower, passage) in objectives)
            if (tower.IsInside(actor) && CanReach(actor, tower, passage))
                return tower;

        var players = Raid.WithoutSlot(excludeNPCs: true).Where(player => Arena.InBounds(player.Position)).ToArray();
        var assignSpouts = players.Contains(actor);
        if (assignSpouts && objectives.Any(objective => objective.Tower.MinSoakers > 1))
        {
            var free = players.Where(player => !objectives.Any(objective => objective.Tower.IsInside(player)
                && CanReach(player, objective.Tower, objective.Passage))).ToList();
            var spouts = objectives.Where(objective => objective.Tower.MinSoakers > 1)
                .Select(objective => (objective.Tower, objective.Passage, Inside: players.Count(player => objective.Tower.IsInside(player))))
                .Where(spout => spout.Inside < spout.Tower.MinSoakers)
                .OrderByDescending(spout => spout.Inside)
                .ThenBy(spout => spout.Passage ?? DateTime.MaxValue)
                .ThenBy(spout => spout.Tower.ActorID);
            foreach (var (tower, passage, inside) in spouts)
            {
                var needed = tower.MinSoakers - inside;
                var soakers = free.Where(player => CanReach(player, tower, passage))
                    .OrderBy(player => player.DistanceToPoint(tower.Position))
                    .ThenBy(player => player.InstanceID)
                    .Take(needed).ToArray();
                if (soakers.Length < needed)
                    continue;
                if (soakers.Contains(actor))
                    return tower;
                foreach (var soaker in soakers)
                    free.Remove(soaker);
            }
        }

        // Orbs are personal objectives. An incomplete party snapshot still
        // permits the old nearest-objective behavior for unlisted actors.
        return objectives.Where(objective => (!assignSpouts || objective.Tower.MinSoakers == 1)
                && objective.Tower.NumInside(Module) < objective.Tower.MinSoakers
                && CanReach(actor, objective.Tower, objective.Passage))
            .OrderBy(objective => actor.DistanceToPoint(objective.Tower.Position))
            .Select(objective => (Tower?)objective.Tower)
            .FirstOrDefault();
    }

    private bool CanReach(Actor actor, in Tower tower, DateTime? passage)
    {
        var travel = tower.IsInside(actor) ? 0d : actor.DistanceToPoint(tower.Position) / 6d;
        return passage == null || passage > WorldState.FutureTime(travel + 2d);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Rebind)
            Towers.Clear();
    }

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == (uint)OID.AquaBubble && !actor.IsDeadOrDestroyed && !Towers.Any(t => t.ActorID == actor.InstanceID))
            Towers.Add(new(actor.Position, 1f, 1, int.MaxValue, actorID: actor.InstanceID));
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID == (uint)OID.BlizzardIIITowers)
        {
            if (state == 0x00040008 && !Towers.Any(t => t.ActorID == actor.InstanceID))
                Towers.Add(new(actor.Position, 2f, 3, int.MaxValue, actorID: actor.InstanceID));
            else if (state is 0x00010002 or 0x00400080 or 0x04000800)
                Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.DreadTide)
            Towers.RemoveAll(t => t.ActorID == caster.InstanceID);
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Boss)
            Towers.Clear();
        else
            Towers.RemoveAll(t => t.ActorID == actor.InstanceID);
    }
}
