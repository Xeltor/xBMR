namespace BossMod.Heavensward.Alliance;

// Both recorded waves have six hits. Their centers advance ~4.5y, with 0.953-1.034s
// between hits; 0.95s is an early planning deadline, not a measured server snapshot.
public abstract class DunScaithNox(BossModule module, uint firstAction, uint repeatAction) : Components.GenericChasingAOEs(module, 4.5f)
{
    private static readonly AOEShapeCircle Shape = new(10);
    private sealed class Sequence(Chaser chaser)
    {
        public readonly Chaser Chaser = chaser;
        public Actor? FirstCaster;
        public bool FirstResolved;
    }
    private readonly List<Sequence> _sequences = [];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 197u)
            return;
        Update();
        if (actor.IsDead || actor.IsDestroyed || Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed
            || _sequences.Exists(s => s.Chaser.Target == actor))
            return;
        // The icon precedes the first hit by 5.08-5.10s. The helper's location cast
        // arrives ~0.085s after the icon and replaces this initial bait position.
        var chaser = new Chaser(Shape, actor, actor.Position, 0, 6, WorldState.FutureTime(5d), 0.95d);
        Chasers.Add(chaser);
        _sequences.Add(new(chaser));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != firstAction)
            return;
        Update();
        if (_sequences.Exists(s => s.FirstCaster == caster))
            return; // duplicate start must not postpone the established deadline
        Sequence? selected = null;
        var bestDistance = float.MaxValue;
        foreach (var sequence in _sequences)
        {
            if (sequence.FirstCaster != null)
                continue;
            var distance = (sequence.Chaser.Target.Position - spell.LocXZ).LengthSq();
            if (distance < bestDistance)
            {
                selected = sequence;
                bestDistance = distance;
            }
        }
        if (selected != null)
        {
            selected.FirstCaster = caster;
            selected.Chaser.PrevPos = spell.LocXZ;
            selected.Chaser.MoveDist = 0;
            selected.Chaser.NextActivation = Module.CastFinishAt(spell);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        Update();
        if (spell.Action.ID == firstAction)
        {
            var sequence = _sequences.Find(s => s.FirstCaster == caster && !s.FirstResolved);
            if (sequence == null)
                return;
            sequence.FirstResolved = true;
            AdvanceSequence(sequence.Chaser, spell.TargetXZ);
        }
        else if (spell.Action.ID == repeatAction)
        {
            // Subsequent hits use different shared helper actors. Match their actual
            // location to the existing target-following sequence, never by helper ID.
            Sequence? selected = null;
            var bestDistance = float.MaxValue;
            foreach (var sequence in _sequences)
            {
                // A simultaneous pulse can contain overlapping locations. Once a
                // sequence advances, its next deadline is ~1s away; do not consume
                // that same sequence again for the other targets in this pulse.
                if (!sequence.FirstResolved || sequence.Chaser.NextActivation > WorldState.FutureTime(0.2d))
                    continue;
                var distance = (sequence.Chaser.PredictedPosition() - spell.TargetXZ).LengthSq();
                if (distance < bestDistance)
                {
                    selected = sequence;
                    bestDistance = distance;
                }
            }
            if (selected != null)
                AdvanceSequence(selected.Chaser, spell.TargetXZ);
        }
    }

    private void AdvanceSequence(Chaser chaser, WPos position)
    {
        ++NumCasts;
        if (--chaser.NumRemaining == 0)
            Remove(chaser);
        else
        {
            chaser.PrevPos = position;
            chaser.MoveDist = 4.5f;
            chaser.NextActivation = WorldState.FutureTime(0.95d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == firstAction)
        {
            var cancelled = _sequences.Find(s => s.FirstCaster == caster && !s.FirstResolved);
            if (cancelled != null)
                Remove(cancelled.Chaser);
        }
    }

    public override void Update()
    {
        if (Module.PrimaryActor.IsDead || Module.PrimaryActor.IsDestroyed)
            Clear();
        else
            for (var i = _sequences.Count - 1; i >= 0; --i)
            {
                var sequence = _sequences[i];
                if (WorldState.CurrentTime > sequence.Chaser.NextActivation.AddSeconds(2d)
                    || (sequence.Chaser.Target.IsDead || sequence.Chaser.Target.IsDestroyed)
                        && (sequence.FirstCaster == null || sequence.FirstResolved))
                    Remove(sequence.Chaser);
            }
    }

    public override void OnActorUntargetable(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            Clear();
        else
            for (var i = _sequences.Count - 1; i >= 0; --i)
            {
                var sequence = _sequences[i];
                if (sequence.FirstCaster == actor && !sequence.FirstResolved)
                    Remove(sequence.Chaser);
            }
        Update();
    }

    private void Remove(Chaser chaser)
    {
        Chasers.Remove(chaser);
        _sequences.RemoveAll(s => s.Chaser == chaser);
    }

    private void Clear()
    {
        Chasers.Clear();
        _sequences.Clear();
    }
}
