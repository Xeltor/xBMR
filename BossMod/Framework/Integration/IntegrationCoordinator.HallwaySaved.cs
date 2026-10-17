namespace BossMod.Integration;

internal sealed partial class IntegrationCoordinator
{
    private static readonly HashSet<string> HallwaySavedIdentities = new(StringComparer.Ordinal)
    {
        "41DA:BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardAerostat1.D040VanguardAerostat1",
        "43D4:BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardAerostat2.D040VanguardAerostat2",
        "447A:BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardLeptocyon.D040VanguardLeptocyon",
        "4479:BossMod.Dawntrail.Dungeon.D04Vanguard.D040VanguardSentryR7.D040VanguardSentryR7",
        "41E2:BossMod.Dawntrail.Dungeon.D05Origenics.D050OrigenicsAerostat.D050OrigenicsAerostat",
        "4612:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90ForestBat.D90ForestBat",
        "46A4:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90ForestWoolback.D90ForestWoolback",
        "4652:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90RottenResearcher.D90RottenResearcher",
        "461B:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90SprightlyClayGolem.D90SprightlyClayGolem",
        "4619:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90SprightlyPhoebad.D90SprightlyPhoebad",
        "4616:BossMod.Dawntrail.Dungeon.D09YuweyawataFieldStation.D90StationSpecter.D90StationSpecter",
    };
    internal sealed record HallwaySavedQuery(int SchemaVersion, string InstanceGeneration, string OwnerToken, string Identity);
    internal sealed record HallwaySavedWrite(int SchemaVersion, string InstanceGeneration, string OwnerToken, string Identity,
        long ExpectedRevision, bool ExpectedSavedEnabled, bool Enabled);
    private bool HallwaySavedSource(int schema, string generation, string owner, string identity)
        => schema == 1 && generation == Session.InstanceGeneration && Session.IsFrameworkThread
            && Session.IsOwnerActive(owner) && identity != null && HallwaySavedIdentities.Contains(identity);

    internal string GetHallwaySavedEnabled(string json)
    {
        var request = IntegrationSession.Parse<HallwaySavedQuery>(json);
        if (request == null || !HallwaySavedSource(request.SchemaVersion, request.InstanceGeneration, request.OwnerToken, request.Identity))
            return Unavailable("invalid-hallway-saved-read");
        var info = ResolveModuleIdentity(request.Identity);
        if (info == null) return Unavailable("hallway-identity-unavailable");
        var state = BossModuleManager.Config.ObserveModuleEnablement(info.PrimaryActorOID);
        return state == null ? Unavailable("module-watch-limit") : IntegrationSession.Serialize(new
        {
            available = true, schemaVersion = 1, Session.InstanceGeneration, request.Identity, state.SavedEnabled,
            state.Revision, state.UserRevision, canDisable = CanDisableModule(info), leased = state.LeaseId != null
        });
    }

    internal string SetHallwaySavedEnabled(string json)
    {
        var request = IntegrationSession.Parse<HallwaySavedWrite>(json);
        if (request == null || !HallwaySavedSource(request.SchemaVersion, request.InstanceGeneration, request.OwnerToken, request.Identity))
            return Unavailable("invalid-hallway-saved-write");
        var info = ResolveModuleIdentity(request.Identity);
        if (info == null || !request.Enabled && (!ControlsAllowed() || !CanDisableModule(info)))
            return Unavailable("hallway-native-disable-gate");
        var config = BossModuleManager.Config; var state = config.ObserveModuleEnablement(info.PrimaryActorOID);
        if (state == null || state.Revision != request.ExpectedRevision || state.SavedEnabled != request.ExpectedSavedEnabled || state.LeaseId != null)
            return Unavailable("hallway-saved-revision-or-lease-conflict");
        config.SetModuleEnabled(info.PrimaryActorOID, request.Enabled);
        var after = config.ObserveModuleEnablement(info.PrimaryActorOID)!;
        return IntegrationSession.Serialize(new
        {
            accepted = true, schemaVersion = 1, Session.InstanceGeneration, request.Identity,
            previousSavedEnabled = state.SavedEnabled, after.SavedEnabled, after.Revision, after.UserRevision
        });
    }
}
