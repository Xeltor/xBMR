namespace BossMod;

public sealed partial class ActionManagerEx
{
    private Func<ActionID, bool>? _automaticDeliveryGate;

    // Installed only around the original synchronous RSR callback. Reject before
    // the manual queue can retain an automatic request after its safety snapshot.
    // The native executor and real manual input run outside this scope.
    internal IDisposable EnterAutomaticDeliveryGate(Func<ActionID, bool> permission)
    {
        var previous = _automaticDeliveryGate;
        _automaticDeliveryGate = permission;
        return new AutomaticDeliveryScope(this, previous, permission);
    }
    private sealed class AutomaticDeliveryScope(ActionManagerEx owner, Func<ActionID, bool>? previous, Func<ActionID, bool> installed) : IDisposable
    {
        public void Dispose()
        {
            if (ReferenceEquals(owner._automaticDeliveryGate, installed)) owner._automaticDeliveryGate = previous;
        }
    }
}
