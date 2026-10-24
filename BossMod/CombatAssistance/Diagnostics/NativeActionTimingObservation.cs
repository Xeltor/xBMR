using BossMod.Autorotation;
using BossMod.CombatAssistance.Planning;
using CSActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace BossMod.CombatAssistance.Diagnostics;

internal sealed record NativeActionTimingSample(uint ActionId, bool Gcd, int WithProcsMs, int WithoutProcsMs,
    int AdjustedRecastMs, int BaseCastMs, int BaseRecastMs, byte CastTimeProc);
internal sealed record NativeFieldTimingObservation(ulong ObjectId, float X, float Y, float Z, float RemainingSeconds);
internal sealed record NativeActionEffectTiming(uint ActionId, float AnimationLockSeconds, DateTime ObservedAt);
internal sealed record NativeActionTimingObservation(DateTime ObservedAt, DateTime WorldTime, uint Job, int Level,
    float X, float Y, float Z, bool CircleOfPower, NativeFieldTimingObservation? Field,
    ImmutableArray<NativeActionTimingSample> Actions, NativeActionEffectTiming? LastRelevantEffect, bool Inspiration = false)
{
    // A small mechanics-specific set, not an arbitrary action inspection API.
    // This runs only for a valid current diagnostic request on the framework
    // thread. It neither forecasts nor submits actions nor retains history.
    internal static ImmutableArray<uint> ActionsFor(Class job) => job switch
    {
        Class.BLM => [(uint)BLM.AID.Fire4, (uint)BLM.AID.Blizzard4, (uint)BLM.AID.Xenoglossy,
            (uint)BLM.AID.LeyLines, (uint)BLM.AID.BetweenTheLines, (uint)BLM.AID.Retrace],
        Class.PCT => [(uint)PCT.AID.FireInRed, (uint)PCT.AID.StarryMuse, (uint)PCT.AID.LandscapeMotif,
            (uint)PCT.AID.StarrySkyMotif, (uint)PCT.AID.Smudge],
        Class.DRG => [(uint)DRG.AID.TrueThrust, (uint)DRG.AID.ChaoticSpring, (uint)DRG.AID.Geirskogul,
            (uint)DRG.AID.Stardiver, (uint)DRG.AID.DragonfireDive],
        Class.NIN => [(uint)NIN.AID.Ten1, (uint)NIN.AID.Ten2, (uint)NIN.AID.Chi2,
            (uint)NIN.AID.Raiton, (uint)NIN.AID.ForkedRaiju, (uint)NIN.AID.FleetingRaiju],
        Class.WAR => [(uint)WAR.AID.InnerRelease, (uint)WAR.AID.PrimalRend],
        Class.SMN => [(uint)SMN.AID.SummonIfrit1, (uint)SMN.AID.SummonIfrit2, (uint)SMN.AID.CrimsonCyclone],
        _ => []
    };

    internal static unsafe NativeActionTimingObservation? Capture(RotationModuleManager rotation)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread || !Service.ClientState.IsLoggedIn
            || rotation.WorldState.Party.Player() is not { IsDead: false, IsDestroyed: false } actor
            || Service.ObjectTable.LocalPlayer is not { } local || !local.IsValid() || local.Address == 0
            || local.GameObjectId != actor.InstanceID || local.ClassJob.RowId != (uint)actor.Class || local.Level != actor.Level
            || CSActionManager.Instance() == null) return null;
        var pose = local.Position;
        if (!float.IsFinite(pose.X) || !float.IsFinite(pose.Y) || !float.IsFinite(pose.Z)) return null;
        var ids = ActionsFor(actor.Class);
        if (ids.IsEmpty) return null;
        var samples = ImmutableArray.CreateBuilder<NativeActionTimingSample>();
        foreach (var id in ids)
        {
            var action = new ActionID(ActionType.Spell, id);
            var definition = ActionDefinitions.Instance[action];
            if (definition == null || !definition.IsUnlocked(rotation.WorldState, actor)
                || !Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)) continue;
            var proc = CSActionManager.CastTimeProc.None;
            var withProcs = CSActionManager.GetAdjustedCastTime(CSActionType.Action, id, true, &proc);
            var withoutProcs = CSActionManager.GetAdjustedCastTime(CSActionType.Action, id, false);
            var recast = CSActionManager.GetAdjustedRecastTime(CSActionType.Action, id, true);
            if (withProcs < 0 || withoutProcs < 0 || recast < 0) continue;
            samples.Add(new(id, definition.IsGCD, withProcs, withoutProcs, recast, row.Cast100ms * 100, row.Recast100ms * 100, (byte)proc));
        }
        var field = NativeLeyLines.Capture(rotation.WorldState, actor);
        var last = rotation.LastCast;
        var effect = last.Data is { Action.Type: ActionType.Spell } data && ids.Contains(data.Action.ID)
            && last.Time <= rotation.WorldState.CurrentTime && float.IsFinite(data.AnimationLockTime) && data.AnimationLockTime >= 0
                ? new NativeActionEffectTiming(data.Action.ID, data.AnimationLockTime, last.Time) : null;
        return new(DateTime.UtcNow, rotation.WorldState.CurrentTime, (uint)actor.Class, actor.Level,
            pose.X, pose.Y, pose.Z, actor.FindStatus((uint)BLM.SID.CircleOfPower) != null,
            field == null ? null : new(field.Object, field.Center.X, field.Height, field.Center.Z,
                Math.Max(0, (float)(field.ExpiresAt - rotation.WorldState.CurrentTime).TotalSeconds)), samples.ToImmutable(), effect, actor.Class == Class.PCT && actor.FindStatus(3689) != null);
    }
}
