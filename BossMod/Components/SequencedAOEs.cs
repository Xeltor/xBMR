namespace BossMod.Components;

// Presentation only: future waves remain timed hazards for AI and retain their Risky flags.
public class SequencedAOEs(BossModule module, uint[] aids, AOEShape shape) : SimpleAOEGroups(module, aids, shape)
{
    private AOEInstance[] _output = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = base.ActiveAOEs(slot, actor);
        if (_output.Length != aoes.Length)
            _output = new AOEInstance[aoes.Length];
        aoes.CopyTo(_output);
        AOESequence.HighlightNext(_output);
        return _output;
    }
    public override void DrawArenaBackground(int pcSlot, Actor pc) => AOESequence.Draw(Arena, ActiveAOEs(pcSlot, pc));
}

public static class AOESequence
{
    // Keep the radar to the current wave and one preview, without trimming ActiveAOEs or AI hints.
    public static DateTime PreviewDeadline(ReadOnlySpan<GenericAOEs.AOEInstance> aoes, double waveWindow = 0.2d)
    {
        var first = DateTime.MaxValue;
        foreach (ref readonly var aoe in aoes)
            if (aoe.Activation < first)
                first = aoe.Activation;
        if (first == DateTime.MaxValue)
            return first;
        var end = first.AddSeconds(waveWindow);
        var next = DateTime.MaxValue;
        foreach (ref readonly var aoe in aoes)
            if (aoe.Activation > end && aoe.Activation < next)
                next = aoe.Activation;
        return next == DateTime.MaxValue ? next : next.AddSeconds(waveWindow);
    }

    public static void Draw(MiniArena arena, ReadOnlySpan<GenericAOEs.AOEInstance> aoes)
    {
        var deadline = PreviewDeadline(aoes);
        foreach (ref readonly var aoe in aoes)
            if (aoe.Activation <= deadline)
                using (arena.WorldProjectionLayer(aoe.ArenaProjectionLayer, aoe.RestrictToArenaProjectionLayer))
                    aoe.Shape.Draw(arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    // Client callbacks for one wave can arrive on adjacent frames. This is a display grouping
    // tolerance, not a snapshot margin, altered activation time or AI suppression window.
    public static void HighlightNext(Span<GenericAOEs.AOEInstance> aoes, double waveWindow = 0.2d)
    {
        if (aoes.Length == 0)
            return;
        var first = DateTime.MaxValue;
        foreach (ref readonly var aoe in aoes)
            if (aoe.Activation < first)
                first = aoe.Activation;
        var end = first.AddSeconds(waveWindow);
        foreach (ref var aoe in aoes)
            aoe.Color = aoe.Activation <= end ? Colors.Danger : Colors.AOE;
        // Draw previews first so their translucent fills cannot obscure the imminent wave.
        aoes.Sort((a, b) => b.Activation.CompareTo(a.Activation));
    }
}
