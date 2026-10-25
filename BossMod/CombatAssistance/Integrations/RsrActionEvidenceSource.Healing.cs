using BossMod.CombatAssistance.Planning;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections;
using System.Reflection;
using CSActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using CSActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace BossMod.CombatAssistance.Integrations;

internal sealed partial class RsrActionEvidenceSource
{
    internal string HealingStatus { get; private set; } = "No configured healing demand";
    internal void ClearHealingDemand() => HealingStatus = "No current configured healing capture";

    // Only consumed by a required escape's arrival preference. This does not
    // publish party contact or request movement from a safe current pose.
    internal PlanningRange? CaptureHealingEscapePosture(WorldState world, Actor player)
    {
        if (player.Role != Role.Healer || !player.InCombat || _rotation?.GetValue(null) is not { } rotation
            || Convert.ToUInt32(Read(rotation, "Job") ?? 0) != (uint)player.Class) return null;
        try
        {
            var allies = world.Party.WithoutSlot(excludeAlliance: true)
                .Where(a => a.InstanceID != player.InstanceID && !a.IsDead && !a.IsDestroyed).ToArray();
            if (allies.Length < 2) return null;
            var radii = new List<float>();
            foreach (var id in EnabledActions(player.Class))
            {
                if (!NativeHealingActions.Supported(id)
                    || ActionDefinitions.Instance[new(ActionType.Spell, id)] is not { } definition || !definition.IsUnlocked(world, player)
                    || definition.IsGCD && Read(rotation, "GCDHeal") is false
                    || !data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)
                    || row.CastType != 2 || row.Range != 0 || row.TargetArea || row.EffectRange == 0) continue;
                radii.Add(row.EffectRange);
            }
            if (radii.Count == 0) return null;
            var anchor = allies.OrderByDescending(a => a.Role == Role.Tank).ThenBy(a => a.InstanceID).First();
            var actors = allies.Select(a => new ExecutionActor(a.InstanceID, a.Position, a.PosRot.Y, a.HitboxRadius, a.Rotation)).ToArray();
            return NativeRadialCoverageRegion.Capture(new(player.InstanceID, player.Position, player.PosRot.Y, player.HitboxRadius, player.Rotation),
                anchor.InstanceID, radii.Min(), actors.Length, actors, [], 0);
        }
        catch (Exception error)
        {
            log.Verbose($"Healing escape posture unavailable: {error.GetBaseException().Message}");
            return null;
        }
    }

    // Sage explicitly retains the chosen follow-up while Eukrasia itself is the
    // selected self action. Its setup does not mean the party shield landed.
    internal RsrActionEvidence? CaptureSageShield(ActionManagerEx manager, RsrActionEvidence? selected)
    {
        if (selected?.ActionId != (uint)SGE.AID.Eukrasia || _rotation?.GetValue(null) is not { } rotation)
            return null;
        try
        {
            object? aim = null;
            for (var type = rotation.GetType(); type != null && aim == null; type = type.BaseType)
                aim = type.GetField("_EukrasiaActionAim", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.GetValue(rotation);
            if (aim == null || !IsSageShield(Convert.ToUInt32(Read(aim, "AdjustedID") ?? 0u))) return null;
            var result = CaptureSelectedAction(manager, aim, false);
            return result is { Friendly: true } && EnabledActions(Class.SGE).Contains(result.ActionId)
                ? result with { ReadyIn = Math.Max(result.ReadyIn, selected.ReadyIn + selected.Recast) } : null;
        }
        catch (Exception error)
        {
            log.Verbose("RSR Sage shield delivery evidence unavailable: " + error.GetBaseException().Message);
            return null;
        }
    }

    internal static bool IsSageShield(uint action)
        => (SGE.AID)action is SGE.AID.EukrasianDiagnosis or SGE.AID.EukrasianPrognosis or SGE.AID.EukrasianPrognosisII;

    // Ask RSR's original range-independent need predicates using its live saved
    // thresholds and HoT rules. Do not call CanUse/target selection: those filter
    // by current range and would recreate the dependency this capture removes.
    internal unsafe PlanningHealingCoverage? CaptureHealingCoverage(WorldState world, Actor player)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread)
            throw new InvalidOperationException("Healing need capture requires the framework thread.");
        HealingStatus = "No configured healing demand";
        if (player.Role != Role.Healer || _assembly == null || _rotation?.GetValue(null) is not { } rotation
            || Convert.ToUInt32(Read(rotation, "Job") ?? 0) != (uint)player.Class) return null;
        try
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var basic = RsrRuntimeAccess.ResolveReferencedAssemblyInLoadContext(_assembly, "RotationSolver.Basic");
            var updater = _assembly.GetType("RotationSolver.Updaters.StateUpdater", true)!;
            var originalNeed = updater.GetMethod("StatusFromHealing", flags) ?? throw new MissingMethodException("RSR healing need");
            var need = originalNeed.Invoke(null, null) as Enum ?? throw new MissingMemberException("RSR healing flags");
            var active = need.ToString().Split(',', StringSplitOptions.TrimEntries).ToHashSet();
            if (!active.Any(s => s.StartsWith("Heal", StringComparison.Ordinal))) return null;
            var config = basic?.GetType("RotationSolver.Basic.Service", true)!.GetProperty("Config", flags)?.GetValue(null)
                ?? throw new MissingMemberException("RSR healing configuration");
            var center = basic!.GetType("RotationSolver.Basic.DataCenter", true)!;
            var party = center.GetProperty("PartyMembers", flags)?.GetValue(null) as IEnumerable
                ?? throw new MissingMemberException("RSR healing recipients");
            var helpers = basic.GetType("RotationSolver.Basic.Helpers.ObjectHelper", true)!;
            var health = helpers.GetMethods(flags).Single(m => m.Name == "GetHealthRatio" && m.GetParameters().Length == 1);
            var single = updater.GetMethods(flags).Single(m => m.Name == "ShouldHealSingle" && m.GetParameters().Length == 4);
            var hots = basic.GetType("RotationSolver.Basic.Helpers.StatusHelper", true)!.GetProperty("SingleHots", flags)?.GetValue(null)
                ?? throw new MissingMemberException("RSR healing-over-time evidence");
            var canHeal = basic.GetType("RotationSolver.Basic.Helpers.StatusHelper", true)!.GetMethod("CanBeHealed", flags)
                ?? throw new MissingMethodException("RSR healable recipient");
            var enabled = EnabledActions(player.Class);
            var native = CSActionManager.Instance();
            if (native == null || Read(rotation, "AllBaseActions") is not IEnumerable actions) return null;
            var alternatives = new Dictionary<HealingDemandKind, List<(uint Id, float Radius, float Limit)>>();
            foreach (var action in actions)
            {
                if (action == null || Read(action, "Setting") is not { } setting || Read(setting, "IsFriendly") is not true
                    || Read(action, "Config") is not { } actionConfig) continue;
                var id = Convert.ToUInt32(Read(action, "AdjustedID") ?? 0u);
                if (!NativeHealingActions.Supported(id)) continue;
                var definition = ActionDefinitions.Instance[new(ActionType.Spell, id)];
                if (!enabled.Contains(id) || definition == null || !definition.IsUnlocked(world, player)
                    || native->GetAdjustedActionId(id) != id || !definition.IsGCD && native->GetCurrentCharges(id) == 0
                    || native->GetActionStatus(CSActionType.Action, id, checkRecastActive: false, checkCastingActive: false) != 0
                    || Read(action, "Info") is not { } info || Read(action, "TargetInfo") is not { } targetInfo) continue;
                var basicCheck = info.GetType().GetMethod("BasicCheck", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (basicCheck?.GetParameters() is not { Length: 6 } parameters
                    || basicCheck.Invoke(info, [false, false, false, true, false, Enum.ToObject(parameters[5].ParameterType, 0)]) is not true) continue;
                var range = Number(Read(targetInfo, "Range"));
                var effect = Number(Read(targetInfo, "EffectRange"));
                if (Read(targetInfo, "IsTargetArea") is true
                    || !data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(id, out var row)) continue;
                var area = range == 0 && row.CastType == 2 && effect > 0;
                // Targeted area/ground/cone healing needs its own placement
                // evidence; never pretend it is a radial player-centered heal.
                if (!area && (row.CastType != 1 || effect != 0 || range <= 0)) continue;
                var kind = (area, definition.IsGCD) switch
                {
                    (false, false) => HealingDemandKind.SingleAbility, (false, true) => HealingDemandKind.SingleSpell,
                    (true, false) => HealingDemandKind.AreaAbility, _ => HealingDemandKind.AreaSpell
                };
                if (!active.Contains("Heal" + kind)) continue;
                var limit = Number(Read(actionConfig, "AutoHealRatio"));
                if (!float.IsFinite(limit) || limit is <= 0 or > 1) continue;
                if (!alternatives.TryGetValue(kind, out var list)) alternatives.Add(kind, list = []);
                list.Add((id, area ? effect : range + player.HitboxRadius, limit));
            }
            var requirements = new List<HealingRequirement>();
            foreach (var wrapper in party.OfType<IBattleChara>())
            {
                if (!wrapper.IsValid() || wrapper.Address == 0 || world.Party.FindSlot(wrapper.GameObjectId) < 0
                    || world.Actors.Find(wrapper.GameObjectId) is not { IsDead: false, IsDestroyed: false } actor) continue;
                var ratio = Number(health.Invoke(null, [wrapper]));
                if (!float.IsFinite(ratio) || ratio is <= 0 or >= 1 || canHeal.Invoke(null, [wrapper]) is not true) continue;
                foreach (var (kind, options) in alternatives)
                {
                    if (kind is HealingDemandKind.SingleAbility or HealingDemandKind.SingleSpell)
                    {
                        var name = kind == HealingDemandKind.SingleAbility ? "HealthSingleAbility" : "HealthSingleSpell";
                        if (single.Invoke(null, [wrapper, hots, Number(Read(config, name)), Number(Read(config, name + "Hot"))]) is not true) continue;
                    }
                    var reaches = options.Where(a => ratio < a.Limit).DistinctBy(a => a.Id)
                        .Select(a => new HealingReach(a.Id, a.Radius + (kind is HealingDemandKind.SingleAbility or HealingDemandKind.SingleSpell ? actor.HitboxRadius : 0), kind))
                        .ToImmutableArray();
                    if (!reaches.IsEmpty)
                        requirements.Add(new(actor.InstanceID, actor.Position, actor.PosRot.Y - player.PosRot.Y, ratio, reaches));
                }
            }
            HealingStatus = $"Configured RSR healing need: {requirements.Count} recipient/category requirements; {alternatives.Values.Sum(a => a.Count)} usable heals";
            return requirements.Count == 0 ? null : new(requirements.GroupBy(r => r.RecipientId)
                .Select(group => group.First() with { Alternatives = group.SelectMany(r => r.Alternatives).DistinctBy(a => a.ActionId).ToImmutableArray() }));
        }
        catch (Exception error)
        {
            HealingStatus = "Configured healing-range evidence unavailable: " + error.GetBaseException().Message;
            log.Verbose(HealingStatus);
            return null;
        }
    }
}
