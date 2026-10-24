using BossMod.Autorotation;
using BossMod.Autorotation.MiscAI;

namespace BossMod.CombatAssistance.Runtime;

// Supply the missing native module for this known preset without rewriting the
// user's preset database. A present selector, including Never/Passive, always wins.
internal sealed class QuestTargetingCorrection(RotationModuleManager manager) : IDisposable
{
    internal const string PresetName = "Questionable - Quest Battles";
    private AutoTarget? _targeting;
    private Actor? _player;
    private readonly StrategyValues _strategy = Strategy();

    internal static bool MissingSelector(Preset? preset)
        => preset?.Name == PresetName && preset.Modules.Any(m => m.Type == typeof(NormalMovement))
            && !preset.Modules.Any(m => m.Type == typeof(AutoTarget));

    internal static StrategyValues Strategy()
    {
        var strategy = new StrategyValues(AutoTarget.Definition().Configs);
        strategy.Values[(int)AutoTarget.Track.QuestBattle] = new StrategyValueTrack { Option = (int)AutoTarget.Flag.Enabled };
        strategy.Values[(int)AutoTarget.Track.Retarget] = new StrategyValueTrack { Option = (int)AutoTarget.RetargetStrategy.Always };
        return strategy;
    }

    internal void Update(bool enabled, Actor? player, float lockDelay, bool moving)
    {
        if (!enabled || player == null || !MissingSelector(manager.Preset))
        {
            Dispose();
            return;
        }
        if (_player != player)
        {
            Dispose();
            _targeting = new(manager, player);
            _player = player;
        }
        _targeting!.Execute(_strategy, manager.Hints.ForcedTarget ?? manager.WorldState.Actors.Find(player.TargetID), lockDelay, moving);
    }

    public void Dispose()
    {
        _targeting?.Dispose();
        _targeting = null;
        _player = null;
    }
}
