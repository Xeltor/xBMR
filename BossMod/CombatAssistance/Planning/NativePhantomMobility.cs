namespace BossMod.CombatAssistance.Planning;

// Phantom identity is independent of the player's regular combat job. Native
// status/charge/target checks still establish whether the selected action is legal.
internal static class NativePhantomMobility
{
    internal static readonly ImmutableArray<uint> ActionIds = [41595, 41600, 49078, 46604, 41646];
    internal static bool Contains(uint id) => ActionIds.Contains(id);

    internal static uint RequiredStatus(uint id) => id switch
    {
        41595 => 4360, // Monk: Phantom Kick
        41600 => 4361, // Ranger: Occult Featherfoot
        49078 => 5331, // Dragoon: Step Forth
        46604 => 4805, // Dancer: Steadfast Stance
        41646 => 4369, // Thief: Occult Sprint
        _ => 0
    };

    internal static float Remaining(uint id, Actor player, DateTime now)
        => RequiredStatus(id) is not 0 and var status && player.FindStatus(status) is { } active
            ? Math.Max(0, (float)(active.ExpireAt - now).TotalSeconds) : 0;

    internal static bool Available(uint id, ushort zone, Actor player, DateTime now)
        => zone is 1252 or 1346 && (id != 49078 || zone == 1346) && Remaining(id, player, now) > 0;
}
