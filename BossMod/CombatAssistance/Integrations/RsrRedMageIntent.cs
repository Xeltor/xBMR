using BossMod.CombatAssistance.Planning;
using System.Reflection;

namespace BossMod.CombatAssistance.Integrations;

internal sealed record RsrRedMageIntent(RedMageIntentChoice Choice, RsrActionEvidence Action, RsrActionEvidence? Fallback,
    uint AfterActionId = 0, float RecastSeconds = 0, bool Swordplay = false, bool Embolden = false);

internal sealed partial class RsrActionEvidenceSource
{
    private readonly Dictionary<(Type Type, string Name), MemberInfo?> _comboMembers = [];
    private string? _comboFailure;

    // Reads only current properties/fields. Do not invoke CanUse, selection or
    // private rotation methods: some of them rewrite RSR's selected targets.
    internal RsrRedMageIntent? CaptureRedMageIntent(RsrActionEvidence? selected, uint lastCombo, float remaining, ushort zone)
    {
        if (!Service.Framework.IsInFrameworkUpdateThread || selected is not { Friendly: false, TargetId: > 0 }
            || !RedMageIntentPolicy.CanPrepareAfter(selected.ActionId)) return null;
        try
        {
            var rotation = _rotation?.GetValue(null);
            if (rotation == null || _redMage?.IsInstanceOfType(rotation) != true
                || _redMageStacks?.GetValue(null) is not byte stacks || _manafication?.GetValue(null) is not bool manafication
                || _redMageCombo?.GetValue(rotation) is not bool inCombo) return null;
            var kind = rotation.GetType().FullName switch
            {
                "RotationSolver.RebornRotations.Magical.RDM_Reborn" => RedMageRotationKind.Reborn,
                "RotationSolver.ExtraRotations.Magical.BeirutaRDM" => RedMageRotationKind.Beiruta,
                _ => RedMageRotationKind.Other
            };
            bool Flag(string name) => ComboMember(null, _redMage, name) is bool flag ? flag : throw new MissingMemberException("RSR Red Mage " + name);
            var pooling = Read(rotation, "Pooling") is true;
            var enough = Read(rotation, pooling ? "EnoughManaComboPooling" : "EnoughManaComboNoPooling") is true;
            var swordplay = Flag("CanMagickedSwordplay");
            var hold = kind == RedMageRotationKind.Beiruta && Read(rotation, "HoldMeleeComboIfOutOfRange") is true
                && ComboMember(rotation, rotation.GetType(), "_meleeHoldUntilMs") is long until
                ? Math.Max(0, (until - Environment.TickCount64) / 1000f) : 0;
            var candidates = new Dictionary<uint, RsrActionEvidence>();
            foreach (var (name, id) in new (string, uint)[]
            {
                ("EnchantedRipostePvE", 7527), ("EnchantedZwerchhauPvE", 7528), ("EnchantedRedoublementPvE", 7529),
                ("EnchantedMoulinetPvE", 7530), ("EnchantedMoulinetDeuxPvE", 37002), ("EnchantedMoulinetTroisPvE", 37003)
            })
                if (ComboCandidate(rotation, selected, name, id) is { } candidate) candidates.Add(id, candidate);
            var fullAoe = FullAoe(zone);
            var moulinet = Read(rotation, "EnchantedMoulinetPvE");
            var configuredMinimum = moulinet != null && Read(moulinet, "Config") is { } config && Read(config, "AoeCount") is byte count ? Math.Max(1, (int)count) : 0;
            var aoeMinimum = kind == RedMageRotationKind.Beiruta
                ? ComboMember(rotation, rotation.GetType(), "DefaultAoeThreshold") is int threshold && configuredMinimum != 0
                    ? Math.Max(threshold, configuredMinimum) : 0
                : configuredMinimum;
            var state = new RedMageIntentState(kind, lastCombo, remaining, stacks, manafication, inCombo,
                Flag("HasEnoughManaFor23Combo") || swordplay,
                Flag("HasEnoughManaFor1Combo") || swordplay,
                Flag("CanInstantCast") || Flag("HasAccelerate") || kind == RedMageRotationKind.Beiruta && Flag("HasDualcast"), enough, fullAoe, hold);
            var choice = RedMageIntentPolicy.Choose(state, candidates.Keys.ToHashSet(), aoeMinimum);
            if (choice is not { } intent) return null;
            _comboFailure = null;
            var action = candidates[intent.Action];
            // Required selected AoE recipients remain individual identities, not
            // a threshold that could hide losing one member of the current pack.
            if (action.CastType == 3 && selected.EffectRange > 0 && selected.AffectedRecipients.Length >= intent.MinimumTargets)
                action = action with { AffectedRecipients = selected.AffectedRecipients };
            return new(intent, action, candidates.GetValueOrDefault(intent.Fallback), selected.ActionId, selected.Recast,
                swordplay, kind == RedMageRotationKind.Reborn && intent.Kind == ComboIntentKind.Starter && Flag("HasEmbolden"));
        }
        catch (Exception error)
        {
            var failure = error.GetBaseException().Message;
            if (_comboFailure != failure) log.Verbose(error, "RSR melee preparation unavailable; selected action planning remains active.");
            _comboFailure = failure;
            return null;
        }
    }

    private RsrActionEvidence? ComboCandidate(object rotation, RsrActionEvidence selected, string name, uint id)
    {
        var action = Read(rotation, name);
        if (action == null || Read(action, "EnoughLevel") is not true || Read(action, "Config") is not { } config
            || ComboMember(config, config.GetType(), "_isEnable") is not true
            || Convert.ToUInt32(Read(action, "AdjustedID") ?? 0u) != id
            || Read(action, "Setting") is not { } setting || Read(setting, "IsFriendly") is not false
            || Read(action, "TargetInfo") is not { } targetInfo) return null;
        var row = data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(id);
        var definition = ActionDefinitions.Instance[new(ActionType.Spell, id)];
        if (row is not { Cast100ms: 0 } value || value.CastType is not (1 or 3)
            || definition == null || !FiniteNonnegative(definition.InstantAnimLock)) return null;
        var range = Number(Read(targetInfo, "Range"));
        var effect = Number(Read(targetInfo, "EffectRange"));
        if (!FiniteNonnegative(range, effect) || range <= 0 || value.CastType == 3 && effect <= 0) return null;
        return new(id, selected.TargetId, selected.TargetPoint, [], false, false, value.CastType, range, effect,
            value.XAxisModifier / 2f, 0, selected.Recast, 0, 0, definition.InstantAnimLock);
    }

    private bool FullAoe(ushort zone)
    {
        var basic = _redMage?.Assembly;
        var service = basic?.GetType("RotationSolver.Basic.Service");
        var config = service == null ? null : ComboMember(null, service, "Config");
        return config != null && Read(config, "AoEType")?.ToString() == "Full"
            && (zone != 1321 || Read(config, "M9SCleaveOnly") is false);
    }

    private object? ComboMember(object? instance, Type type, string name)
    {
        if (!_comboMembers.TryGetValue((type, name), out var member))
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            member = (MemberInfo?)type.GetProperty(name, flags) ?? type.GetField(name, flags);
            _comboMembers.Add((type, name), member);
        }
        return member switch { PropertyInfo property => property.GetValue(instance), FieldInfo field => field.GetValue(instance), _ => null };
    }
}
