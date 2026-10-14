namespace BossMod.Heavensward.Alliance.A35Diabolos;

// Gate appearance supplies the preparation cue; summon urgency comes from the
// actual live cast. No fixed intermission schedule or damage geometry is inferred.
class GateIntermission(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        var deathgates = Module.Enemies((uint)OID.Deathgate).Where(Current).ToArray();
        if (deathgates.Length > 0)
        {
            hints.Add(deathgates.Any(g => g.IsTargetable)
                ? "Deathgates: destroy them before they summon adds!"
                : "Deathgates forming: prepare to destroy them!");
            var casts = deathgates.Select(g => g.CastInfo)
                .Where(c => c is { Action.ID: (uint)AID.VoidCall, RemainingTime: > 0 }).ToArray();
            if (casts.Length > 0)
                hints.Add($"Void Call: destroy the Deathgates before the cast finishes ({casts.Min(c => c!.NPCRemainingTime):f1}s)!");
        }
        if (Module.Enemies((uint)OID.Lifegate).Any(g => Current(g) && g.IsTargetable))
            hints.Add("Lifegate: keep attacking through its health refills!");
    }

    private bool Current(Actor gate) => !gate.IsDeadOrDestroyed && WorldState.Actors.Find(gate.InstanceID) == gate;
}
