namespace BossMod.CombatAssistance.Support;

internal static class TankArmsLengthPolicy
{
    // This is the action's real 120-second cooldown, not a forecast/search cap.
    internal const float CooldownSeconds = 120;
    internal static bool ShouldUse(bool tank, bool dungeonTrash, int attackers, bool existingImmunity,
        float nextKnockbackIn, bool manualControl)
        => tank && dungeonTrash && attackers >= 2 && !existingImmunity && !manualControl
            && float.IsFinite(nextKnockbackIn) && nextKnockbackIn > CooldownSeconds;

    internal static bool IsDungeonTrash(string? moduleName, bool dungeon)
    {
        if (!dungeon || string.IsNullOrWhiteSpace(moduleName)) return false;
        var name = moduleName.AsSpan(moduleName.LastIndexOf('.') + 1);
        if (name.Length < 3 || name[0] != 'D') return false;
        var index = 1;
        while (index < name.Length && char.IsDigit(name[index])) ++index;
        return index > 1 && index < name.Length && name[index - 1] == '0';
    }
}
