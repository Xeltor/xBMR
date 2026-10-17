using BossMod.Components;

namespace BossMod.Integration;

internal sealed class IntegrationStackCorrection
{
    private readonly Dictionary<string, Func<bool>> _owners = [];
    internal bool Set(string owner, bool enabled, Func<bool>? live)
    {
        foreach (var expired in _owners.Where(e => !e.Value()).Select(e => e.Key).ToArray()) _owners.Remove(expired);
        if (!enabled) return _owners.Remove(owner);
        if (live == null || !live() || (!_owners.ContainsKey(owner) && _owners.Count >= IntegrationSession.MaxOwners)) return false;
        _owners[owner] = live; return true;
    }
    internal bool Enabled => _owners.Values.Any(live => live());

    internal static bool Required(GenericStackSpread component, Actor local, GenericStackSpread.Stack other)
    {
        if (other.Target == local || component.GetType().GetMethod(nameof(GenericStackSpread.AddAIHints))?.DeclaringType != typeof(GenericStackSpread)
            || !component.EnableHints || (!component.IncludeDeadTargets && other.Target.IsDead)) return false;
        var module = component.Module;
        bool Participant(GenericStackSpread.Stack stack) => module.ActorMatchesArenaProjectionLayer(local,
            stack.ResolveArenaProjectionLayer(module), stack.RestrictToArenaProjectionLayer);
        if (!Participant(other)) return false;
        foreach (var own in component.Stacks)
            if (own.Target == local && (component.IncludeDeadTargets || !own.Target.IsDead) && Participant(own)
                && Math.Abs((own.Activation - other.Activation).TotalSeconds) <= 1) return true;
        return false;
    }
}
