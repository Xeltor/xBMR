namespace BossMod.CombatAssistance.Integrations;

// ActionUpdater can briefly retain the previous rotation's selected action.
// Wait for a new selection before deriving movement from the new rotation.
internal sealed class RsrRotationSelectionTracker
{
    private object? rotation;
    private object? lastAction;
    private object? staleAction;

    internal bool Observe(object? currentRotation, object? action)
    {
        if (!ReferenceEquals(this.rotation, currentRotation))
        {
            this.staleAction = this.lastAction;
            this.rotation = currentRotation;
        }
        if (currentRotation == null || action == null || ReferenceEquals(action, this.staleAction)) return false;
        this.lastAction = action;
        this.staleAction = null;
        return true;
    }
}
