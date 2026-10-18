namespace BossMod.RealmReborn.Alliance.A33Cerberus;

class BellyAndChains(BossModule module) : BossComponent(module)
{
    private readonly HashSet<ulong> _belly = [];
    public static bool Inside(Actor actor) => actor.PosRot.Y < -400;
    private static bool Miniature(Actor actor) => actor.FindStatus(SID.Minimum) != null;
    private IEnumerable<Actor> Puddles => WorldState.Actors.Where(a => a.OID == 0x1E968C && !a.IsDeadOrDestroyed && a.EventState != 7);
    public override void Update()
    {
        // Freeze assignments through each ingestion cycle. Re-evaluate only when the next juice appears.
        var juice = Module.Enemies((uint)OID.GastricJuice).Any(a => !a.IsDeadOrDestroyed);
        if (!juice && !Puddles.Any() && !Raid.WithoutSlot().Any(Inside))
            _belly.Clear();
        if (_belly.Count != 0 || !juice)
            return;
        var policy = Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly;
        var living = Raid.WithSlot().Where(p => p.Item2.Type == ActorType.Player && !p.Item2.IsDeadOrDestroyed).ToArray();
        // An incomplete roster must not send the whole visible party inside and leave nobody on adds.
        if (policy == WorldOfDarknessConfig.BellyStrategy.Manual || living.Length < 18)
            return;
        var healer = living.Where(p => p.Item2.Role == Role.Healer).OrderBy(p => p.Item2.InstanceID).Select(p => p.Item2).FirstOrDefault();
        var selected = living.Where(p => policy == WorldOfDarknessConfig.BellyStrategy.AllianceB
            ? WorldOfDarknessAlliance.Group(Raid, p.Item1) == 1
            : p.Item2.Role is Role.Melee or Role.Ranged || p.Item2 == healer)
            .Where(p => p.Item2.InstanceID != Module.PrimaryActor.TargetID).OrderBy(p => p.Item2.InstanceID).Take(living.Length - 5);
        foreach (var (_, actor) in selected)
            _belly.Add(actor.InstanceID);
    }
    public bool Assigned(Actor actor) => _belly.Contains(actor.InstanceID);
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Inside(actor))
            hints.Add("Stomach: destroy the four walls!", false);
        else if (actor.FindStatus(SID.Transporting) != null)
            hints.Add("Carrying a chain: attach it to an open fetter on Cerberus!");
        else if (Assigned(actor) && (Puddles.Any() || Module.Enemies((uint)OID.GastricJuice).Any(a => !a.IsDeadOrDestroyed)))
            hints.Add(Miniature(actor) ? "Mini: enter the purple puddle after Slabber resolves!" : "Stomach team: get Mini from Gastric Juice first!");
        else if (Puddles.Any())
            hints.Add("Outside team: stay out of the purple puddle; kill Wolfsbanes", false);
        if (actor.FindStatus(SID.Abandonment) != null)
            hints.Add("Abandonment: stay close to your party!");
    }
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Reawakening)
            hints.Add("Rechain Cerberus before Reawakening finishes! Two players take the chains, then attach both fetters.");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var inside = Inside(actor);
        foreach (var enemy in hints.PotentialTargets)
        {
            if (Inside(enemy.Actor) != inside)
            {
                enemy.Priority = AIHints.Enemy.PriorityForbidden;
                continue;
            }
            if (!enemy.Actor.InCombat || enemy.Priority < 0)
                continue;
            enemy.Priority = enemy.Actor.OID switch
            {
                (uint)OID.StomachWall => 5,
                (uint)OID.Wolfsbane or (uint)OID.Unknown => 3,
                // Leave the juice alive to shrink the stomach team.
                (uint)OID.GastricJuice => AIHints.Enemy.PriorityUndesirable,
                (uint)OID.Boss => 1,
                _ => enemy.Priority
            };
        }
        if (inside)
            return;
        var entering = Assigned(actor) && Miniature(actor) && actor.FindStatus(SID.Seized) == null;
        foreach (var puddle in Puddles)
        {
            if (!entering)
                hints.AddForbiddenZone(new SDCircle(puddle.Position, 8), arenaProjectionLayer: 0);
        }
        if (entering && Puddles.OrderBy(p => (p.Position - actor.Position).LengthSq()).FirstOrDefault() is { } entrance)
            // The puddle is created after the damaging Slabber cast. Never exempt that cast AOE.
            hints.AddForbiddenZone(new SDInvertedCircle(entrance.Position, 1), arenaProjectionLayer: 0);
        else if (Assigned(actor) && !Miniature(actor))
        {
            var juice = Module.Enemies((uint)OID.GastricJuice).Where(a => !a.IsDeadOrDestroyed).OrderBy(a => a.InstanceID).FirstOrDefault();
            if (juice != null)
                hints.AddForbiddenZone(new SDInvertedCircle(juice.Position, 7.5f), juice.CastInfo?.Action.ID == (uint)AID.Mini ? Module.CastFinishAt(juice.CastInfo, -0.5) : WorldState.CurrentTime, arenaProjectionLayer: 0);
        }
        if (actor.FindStatus(SID.Abandonment) != null && slot >= 0)
        {
            var members = Raid.WithSlot().Where(p => p.Item1 / PartyState.MaxPartySize == slot / PartyState.MaxPartySize && p.Item2 != actor && !p.Item2.IsDeadOrDestroyed && !Inside(p.Item2))
                .OrderBy(p => p.Item2.InstanceID).ToArray();
            if (members.Length != 0)
                // One yard is a conservative arrival tolerance; the fear threshold was not observed.
                hints.AddForbiddenZone(new SDInvertedCircle(members[0].Item2.Position, 1), arenaProjectionLayer: 0);
        }
    }
}

class Mini(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Mini, 9, arenaProjectionLayer: 0)
{
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => Module.FindComponent<BellyAndChains>()?.Assigned(actor) == true && actor.FindStatus(SID.Minimum) == null ? [] : base.ActiveAOEs(slot, actor);
}
