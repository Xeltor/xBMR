namespace BossMod.RealmReborn.Alliance.A33Cerberus;

class Chains(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<Actor, ulong> _pickups = [];
    private static bool Anchor(Actor a) => a.OID is (uint)OID.MagickedFetter1 or (uint)OID.MagickedFetter2;
    private static bool Open(Actor a) => a.OID is (uint)OID.OpenFetter1 or (uint)OID.OpenFetter2;
    private bool Rechaining => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.Reawakening;
    public override void Update()
    {
        if (!Rechaining)
        {
            _pickups.Clear();
            return;
        }
        var previous = new Dictionary<Actor, ulong>(_pickups);
        _pickups.Clear();
        var players = Raid.WithSlot().Where(p => p.Item2.Type == ActorType.Player && !p.Item2.IsDeadOrDestroyed).ToArray();
        if (players.Length < 18 || Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly == WorldOfDarknessConfig.BellyStrategy.Manual)
            return;
        var used = new HashSet<ulong>();
        foreach (var anchor in WorldState.Actors.Where(a => Anchor(a) && a.IsTargetable && !a.IsDeadOrDestroyed).OrderBy(a => a.InstanceID))
        {
            var eligible = players.Where(p => !used.Contains(p.Item2.InstanceID) && !BellyAndChains.Inside(p.Item2) && p.Item2.Role != Role.Tank
                && p.Item2.FindStatus(SID.Transporting) == null && p.Item2.FindStatus(SID.Seized) == null);
            if (Service.Config.Get<WorldOfDarknessConfig>().CerberusBelly == WorldOfDarknessConfig.BellyStrategy.AllianceB)
                eligible = eligible.Where(p => WorldOfDarknessAlliance.Group(Raid, p.Item1) == 2);
            var actor = previous.TryGetValue(anchor, out var id) ? eligible.Select(p => p.Item2).FirstOrDefault(p => p.InstanceID == id) : null;
            actor ??= eligible.OrderBy(p => (p.Item2.Position - anchor.Position).LengthSq()).ThenBy(p => p.Item2.InstanceID).Select(p => p.Item2).FirstOrDefault();
            if (actor != null)
            {
                _pickups[anchor] = actor.InstanceID;
                used.Add(actor.InstanceID);
            }
        }
    }
    private Actor? Destination(Actor actor)
    {
        if (!Rechaining || BellyAndChains.Inside(actor))
            return null;
        if (actor.FindStatus(SID.Transporting) != null)
        {
            var chain = Module.Enemies((uint)OID.CerberusHelper3).FirstOrDefault(a => a.Tether.ID == (uint)TetherID.Tether34 && a.Tether.Target == actor.InstanceID);
            // The recorded left chain changes to tether32 when attached at OpenFetter1;
            // the right chain changes to33 at OpenFetter2. Do not infer from carrier position.
            if (chain == null)
                return null;
            var oid = chain.Position.X < Arena.Center.X ? (uint)OID.OpenFetter1 : (uint)OID.OpenFetter2;
            return WorldState.Actors.FirstOrDefault(a => Open(a) && a.OID == oid && a.IsTargetable && !a.IsDeadOrDestroyed);
        }
        return _pickups.FirstOrDefault(p => p.Value == actor.InstanceID && p.Key.IsTargetable && !p.Key.IsDeadOrDestroyed).Key;
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Destination(actor) is { } target)
            hints.Add(Anchor(target) ? "Take your assigned chain!" : "Attach your chain to the open fetter!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Destination(actor) is { } target)
        {
            var radius = Anchor(target) ? 6.5f : 4;
            hints.AddForbiddenZone(new SDInvertedCircle(target.Position, radius), arenaProjectionLayer: 0);
            hints.InteractWithTarget = target;
        }
    }
}
