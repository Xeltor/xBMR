namespace BossMod;

// A cast or a momentarily stationary pose does not prove the target will keep
// its facing while we move. Require a fixed target or a resolved other victim.
internal static class PositionalMovement
{
    internal static Positional ForTarget(Positional desired, Actor? target, Actor player, ActorState actors)
    {
        if (desired is not (Positional.Rear or Positional.Flank))
            return desired;
        if (target == null || target.IsDeadOrDestroyed || target.Omnidirectional)
            return Positional.Any;
        if (target.IsStrikingDummy)
            return desired;
        var victim = actors.Find(target.TargetID);
        return victim != null && victim.InstanceID != player.InstanceID && victim.InstanceID != target.InstanceID
            && !victim.IsDeadOrDestroyed && (victim.Type == ActorType.Player
                || victim.IsAlly && victim.Type is ActorType.Enemy or ActorType.Pet or ActorType.Chocobo or ActorType.Buddy)
            ? desired : Positional.Any;
    }
}
