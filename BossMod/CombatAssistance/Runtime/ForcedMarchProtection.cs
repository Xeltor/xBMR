using BossMod.CombatAssistance.Integrations;
using BossMod.Components;

namespace BossMod.CombatAssistance.Runtime;

// Native component state is observed on the framework thread. The brief transition
// bridge expires unless fresh player-specific evidence renews it; no IPC leases,
// reflected component fields or arbitrary maximum march duration are involved.
internal sealed class ForcedMarchProtection(Func<(RsrMode? Mode, object? Provider)> read, Func<RsrMode, bool> write)
{
    private readonly ForcedMarchRotationOwner _rotation = new(read, write);
    private BossModule? _module;
    private ulong _actor;
    private DateTime _transitionUntil;
    private long _identity;
    internal bool Protecting { get; private set; }
    internal bool OwnsRotation => _rotation.Owned;
    internal bool Paused => _rotation.Paused;
    internal string Status => _rotation.Status;

    internal void Update(bool allowed, WorldState world, BossModule? module, Actor? actor)
    {
        if (_module != module || _actor != actor?.InstanceID || !allowed || module == null || actor == null)
        {
            Release();
            _module = module;
            _actor = actor?.InstanceID ?? 0;
            if (!allowed || module == null || actor == null) return;
        }
        var relevant = false;
        foreach (var march in module.Components.OfType<GenericForcedMarch>())
        {
            if (!Planning.NativeForcedMovementCapture.NeedsExclusivePlanning(world, march, actor)) continue;
            relevant = true;
            break;
        }
        if (relevant)
        {
            if (!Protecting) ++_identity;
            _transitionUntil = world.FutureTime(0.35);
        }
        Protecting = relevant || world.CurrentTime < _transitionUntil;
        _rotation.Tick(Protecting, _identity);
    }

    internal void Release()
    {
        Protecting = false;
        _transitionUntil = default;
        _rotation.Tick(false, 0);
    }
}
