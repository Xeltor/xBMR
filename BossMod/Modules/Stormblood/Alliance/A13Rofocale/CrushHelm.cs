namespace BossMod.Stormblood.Alliance.A13Rofocale;

class CrushHelm(BossModule module) : BossComponent(module)
{
    // Conservative offsets from the visual effect, based on the three recorded sequences.
    private static readonly double[] HitOffsets = [0.8d, 1.45d, 2.02d, 2.62d, 3.22d];
    private readonly HashSet<ulong> _helpers = [];
    private DateTime _visualAt;
    private ulong _target;
    private bool _visualResolved;
    private (ulong Target, DateTime Expires) _pendingMark;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != (uint)IconID.CrushHelm || _visualResolved || Module.PrimaryActor.IsDeadOrDestroyed)
            return;

        var target = WorldState.Actors.Find(targetID) ?? actor;
        if (target.IsDeadOrDestroyed || Raid.FindSlot(target.InstanceID) < 0)
            return;

        // The mark and the self-targeted visual can arrive in either callback order.
        if (_visualAt != default)
            _target = target.InstanceID;
        else
            _pendingMark = (target.InstanceID, WorldState.FutureTime(0.25d));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.CrushHelm1))
        {
            var mark = _pendingMark.Expires >= WorldState.CurrentTime ? _pendingMark.Target : 0;
            Reset();
            _visualAt = Module.CastFinishAt(spell);
            var explicitTarget = spell.TargetID != caster.InstanceID ? spell.TargetID : 0;
            _target = mark != 0 ? mark : explicitTarget != 0 ? explicitTarget : caster.TargetID;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.IsSpell(AID.CrushHelm1) && !_visualResolved && !spell.EventHappened)
            Reset();
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor)
        {
            if (spell.IsSpell(AID.CrushHelm1))
            {
                _visualAt = WorldState.CurrentTime;
                _visualResolved = true;
                if (_target == 0)
                    _target = caster.TargetID;
            }
            else if (spell.IsSpell(AID.CrushHelm3))
            {
                Reset();
            }
        }
        else if (_visualAt != default && caster.OID == (uint)OID.Rofocale1 && spell.IsSpell(AID.CrushHelm2)
            && _helpers.Count < HitOffsets.Length - 1 && _helpers.Add(caster.InstanceID))
        {
            _visualResolved = true;
            // The visual targets the boss. The helper supplies the actual recipient.
            if (spell.Targets.Count == 1)
                _target = spell.Targets[0].ID;
            else if (spell.MainTargetID != 0)
                _target = spell.MainTargetID;
        }
    }

    public override void Update()
    {
        if (_visualAt != default && WorldState.CurrentTime > _visualAt.AddSeconds(4d))
            Reset();
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Reset();
        else if (actor.InstanceID == _pendingMark.Target)
            _pendingMark = default;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_visualAt != default)
            hints.Add("Crush Helm applies four stacks of vulnerability before the heavy hit. Mitigate the finisher.");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var targetSlot = Raid.FindSlot(_target);
        if (_visualAt != default && targetSlot >= 0 && Raid[targetSlot] is { IsDeadOrDestroyed: false })
        {
            var recipients = new BitMask().WithBit(targetSlot);
            for (var hit = _helpers.Count; hit < HitOffsets.Length; ++hit)
                hints.AddPredictedDamage(recipients, _visualAt.AddSeconds(HitOffsets[hit]),
                    hit == HitOffsets.Length - 1 ? AIHints.PredictedDamageType.Tankbuster : AIHints.PredictedDamageType.None);
        }

        foreach (var (partySlot, member) in Raid.WithSlot(excludeAlliance: true, excludeNPCs: true))
            if (member.PendingDispels.Count == 0 && member.FindStatus((uint)SID.PhysicalVulnerabilityUp) is { Extra: >= 4 } status
                && status.ExpireAt > WorldState.CurrentTime)
                hints.ShouldCleanse.Set(partySlot);
    }

    private void Reset()
    {
        _helpers.Clear();
        _visualAt = default;
        _target = 0;
        _visualResolved = false;
        _pendingMark = default;
    }
}
