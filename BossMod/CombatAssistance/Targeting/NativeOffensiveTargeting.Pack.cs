using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections;
using System.Reflection;

namespace BossMod.CombatAssistance.Targeting;

internal sealed partial class NativeOffensiveTargeting
{
    internal unsafe PlanningRange? CaptureSagePackRange(float readyIn, out string reason)
    {
        reason = "Sage pack approach unavailable";
        var world = _manager.WorldState;
        var hints = _manager.Hints;
        var player = world.Party.Player();
        if (player is not { Class: Class.SGE, InCombat: true } || !_preferences().ManageAoePackPositioning
            || !_allowed() || !Prepare() || !SelectionMode() || Manual()
            || hints.ForcedMovement != null || hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal
            || player.PendingKnockbacks.Count != 0) return null;
        try
        {
            var config = _basic!.GetType("RotationSolver.Basic.Service", true)!.GetProperty("Config", Static)?.GetValue(null);
            if (config == null || Get(config, "AoEType")?.ToString() != "Full")
            { reason = "RSR full AoE is disabled"; return null; }
            var native = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
            if (native == null) return null;
            var id = native->GetAdjustedActionId((uint)SGE.AID.Dyskrasia);
            if (id != (uint)SGE.AID.Dyskrasia && id != (uint)SGE.AID.DyskrasiaII
                || Get(_rotation!, "AllBaseActions") is not IEnumerable actions) return null;
            var action = actions.Cast<object>().FirstOrDefault(a => Convert.ToUInt32(Get(a, "ID") ?? 0) == id);
            reason = "Dyskrasia saved settings or action geometry unavailable";
            if (action == null || Get(action, "Config") is not { } actionConfig
                || actionConfig.GetType().GetField("_isEnable", Instance)?.GetValue(actionConfig) is not true
                || ReadMetadata(action) is not { Geometry: { CastType: 2, Range: 0, Friendly: false, EffectRange: > 0 } } metadata
                || Get(action, "Info") is not { } info) return null;
            var minimum = Convert.ToInt32(Get(actionConfig, "AoeCount") ?? 0);
            var check = info.GetType().GetMethod("BasicCheck", Instance);
            reason = "Dyskrasia basic/lifetime eligibility not ready";
            if (minimum <= 0 || check?.GetParameters() is not { Length: 6 } parameters
                || check.Invoke(info, [false, false, false, true, false, Enum.ToObject(parameters[5].ParameterType, 0)]) is not true
                || action.GetType().GetMethod("IsTimeToKillValid", Instance)?.Invoke(action, null) is not true
                || CaptureFrame() is not { } frame) return null;
            var target = hints.ForcedTarget?.InstanceID ?? player.TargetID;
            if (!frame.Eligible(target) || Service.ObjectTable.SearchById(target) is not IBattleChara) return null;
            // Match the range-independent predicates of RSR's GetCanAffects.
            // CanUse/FindTarget would filter out the very pack we need to reach.
            var targetInfo = Get(action, "TargetInfo")!;
            Func<IBattleChara, bool> Bind(string name) => targetInfo.GetType().GetMethod(name, Instance, [typeof(IBattleChara)])!
                .CreateDelegate<Func<IBattleChara, bool>>(targetInfo);
            var resistance = Bind("CheckResistance");
            var lifetime = Bind("CheckTimeToKill");
            var statuses = targetInfo.GetType().GetMethod("CheckStatus", Instance, [typeof(IBattleChara), typeof(bool), typeof(bool)])!
                .CreateDelegate<Func<IBattleChara, bool, bool, bool>>(targetInfo);
            var hostiles = (Center("AllHostileTargets") as IEnumerable)?.OfType<IBattleChara>()
                .Where(a => a.IsValid() && a.Address != 0).Select(a => a.GameObjectId).ToHashSet();
            if (hostiles == null) return null;
            var eligible = new List<ExecutionActor>();
            var excluded = new List<ExecutionActor>();
            foreach (var actor in frame.Actors)
            {
                if (actor.Actor.Dead) continue;
                if (frame.Eligible(actor.Actor.Id) && !actor.ForbiddenCollateral && hostiles.Contains(actor.Actor.Id)
                    && Service.ObjectTable.SearchById(actor.Actor.Id) is IBattleChara wrapper && wrapper.IsValid() && wrapper.Address != 0
                    && resistance(wrapper) && lifetime(wrapper) && statuses(wrapper, false, false)) eligible.Add(actor.Actor);
                else excluded.Add(actor.Actor);
            }
            var range = NativeRadialCoverageRegion.Capture(frame.Player, target, metadata.Geometry.EffectRange, minimum, eligible, excluded, readyIn);
            reason = range == null ? "No eligible compact Dyskrasia pack" : $"Dyskrasia pack approach: RSR minimum {minimum}, radius {metadata.Geometry.EffectRange}";
            return range;
        }
        catch (Exception error)
        {
            reason = "Sage pack-range evidence unavailable: " + error.GetBaseException().Message;
            return null;
        }
    }
}
