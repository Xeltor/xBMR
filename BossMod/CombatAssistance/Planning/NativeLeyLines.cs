namespace BossMod.CombatAssistance.Planning;

internal sealed record ObservedLeyLines(ulong Owner, ulong Object, WPos Center, float Height, DateTime ExpiresAt, ulong Effect = 0)
{
    internal ulong EffectIdentity => Effect == 0 ? Object : Effect;
}

// Shared current native ownership for timing, zone recovery and the exact return
// anchor. An absent/ambiguous object never becomes a guessed spatial footprint.
internal static class NativeLeyLines
{
    internal static ObservedLeyLines? Capture(WorldState world, Actor player)
    {
        if (player.Class != Class.BLM || player.InstanceID == 0 || player.IsDead || player.IsDestroyed
            || player.FindStatus((uint)BLM.SID.LeyLines) is not { } status || status.ExpireAt <= world.CurrentTime) return null;
        var objects = world.Actors.Where(a => a.OID == 0x179 && a.OwnerID == player.InstanceID && !a.IsDestroyed).ToArray();
        return objects.Length == 1 && objects[0].InstanceID != 0 && float.IsFinite(objects[0].PosRot.X) && float.IsFinite(objects[0].PosRot.Y) && float.IsFinite(objects[0].PosRot.Z)
            ? new(player.InstanceID, objects[0].InstanceID, objects[0].Position, objects[0].PosRot.Y, status.ExpireAt) : null;
    }
}
