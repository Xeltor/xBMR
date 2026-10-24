using BossMod;
using BossMod.Autorotation;
using BossMod.Autorotation.MiscAI;
using BossMod.CombatAssistance.Runtime;

public static partial class Checks
{
    private static void NativeCorrections()
    {
        var alignment = BossMod.CombatAssistance.Runtime.NativeCorrections.AlignTerrainFrame(new(10.4f, -3.4f), new(1, 1), 0.5f, 30);
        Check(alignment is { X: 18, Z: -8, Half: 60 } && alignment.Value.Center == new WPos(10, -3),
            "native FATE map frame retains exact terrain-window truncation arithmetic");
        Check(BossMod.CombatAssistance.Runtime.NativeCorrections.AlignTerrainFrame(default, default, 0, 30) == null,
            "invalid terrain resolution cannot mutate map alignment");
        Check(BossMod.CombatAssistance.Runtime.NativeCorrections.AlignTerrainFrame(new(1000010, 0), default, 0.5f, 30) != null,
            "native map frame has no old IPC coordinate cap when indices are representable");
        var preset = new Preset(QuestTargetingCorrection.PresetName);
        preset.AddModule(typeof(NormalMovement), NormalMovement.Definition(), static (_, _) => throw new InvalidOperationException("Not executed in a preset shape test"));
        Check(QuestTargetingCorrection.MissingSelector(preset), "known Questionable movement preset can receive missing targeting behavior");
        var strategy = QuestTargetingCorrection.Strategy();
        Check(strategy.Option(AutoTarget.Track.QuestBattle).As<AutoTarget.Flag>() == AutoTarget.Flag.Enabled
            && strategy.Option(AutoTarget.Track.Retarget).As<AutoTarget.RetargetStrategy>() == AutoTarget.RetargetStrategy.Always,
            "Questionable correction uses native quest and retarget strategy identities");
        preset.AddModule(typeof(AutoTarget), AutoTarget.Definition(), static (_, _) => throw new InvalidOperationException("Not executed in a preset shape test"));
        Check(!QuestTargetingCorrection.MissingSelector(preset), "existing selector and user opt-outs are never replaced");
        Check(!QuestTargetingCorrection.MissingSelector(new Preset("Questionable custom")) && !QuestTargetingCorrection.MissingSelector(null),
            "correction does not activate a preset or expand to arbitrary Questionable presets");
    }
}
