namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

// Creation is the earliest recorded portal cue,18.745s before Deep Flow starts.
// Entry eligibility, entrant ownership and the separate arena remain manual.
class GateGuidance(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, Actor> _gates = [];

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID is (uint)OID.DiabolicGate or (uint)OID.Deathgate)
            _gates[actor.InstanceID] = actor;
        else
            _gates.Remove(actor.InstanceID); // A replacement can change object type.
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_gates.GetValueOrDefault(actor.InstanceID) == actor)
            _gates.Remove(actor.InstanceID);
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (PortalSafety.InPortal(actor))
            return;
        foreach (var gate in _gates.Values)
        {
            if (gate.IsDeadOrDestroyed || WorldState.Actors.Find(gate.InstanceID) != gate)
                continue;
            if (gate.OID == (uint)OID.DiabolicGate)
            {
                // Targetability changes the preparation wording, not a claim that
                // entering is now safe or that any particular actor must enter.
                hints.Add(gate.IsTargetable
                    ? "Diabolic Gate: a group must enter manually and defeat Shadow Dahak!"
                    : "Diabolic Gate forming: prepare a group to enter manually!");
                if (gate.CastInfo is { Action.ID: (uint)AID.DeepFlow, RemainingTime: > 0 } flow)
                    hints.Add($"Deep Flow: defeat the portal add before the cast finishes ({flow.NPCRemainingTime:f1}s)!");
            }
            else if (gate.CastInfo is { Action.ID: (uint)AID.VoidCall, RemainingTime: > 0 } call)
                hints.Add($"Void Call: destroy the Deathgate before adds spawn ({call.NPCRemainingTime:f1}s)!");
        }
    }

    public override void Update()
    {
        foreach (var (id, gate) in _gates.ToArray())
            if (gate.IsDeadOrDestroyed || WorldState.Actors.Find(id) != gate)
                _gates.Remove(id);
    }
}
