namespace BossMod.RealmReborn.Alliance;

[ConfigDisplay(Name = "World of Darkness", Parent = typeof(RealmRebornConfig))]
public sealed class WorldOfDarknessConfig() : ConfigNode()
{
    public enum AllianceGroup { Automatic, A, B, C }
    public enum BellyStrategy { AllDPSAndOneHealer, AllianceB, Manual }
    [PropertyDisplay("Your alliance", tooltip: "Automatic uses the live alliance labels. Override only for a custom arrangement or a replay without labels.")]
    public AllianceGroup Alliance = AllianceGroup.Automatic;
    [PropertyDisplay("Cerberus stomach assignment", tooltip: "All DPS and one healer is the common shared strategy. Alliance B supports the A adds / B belly / C chains arrangement. Manual gives warnings without directing players into the stomach.")]
    public BellyStrategy CerberusBelly = BellyStrategy.AllDPSAndOneHealer;
}

static class WorldOfDarknessAlliance
{
    public static int Group(PartyState raid, int slot)
    {
        var configured = Service.Config.Get<WorldOfDarknessConfig>().Alliance;
        return slot is >= 0 and < PartyState.MaxPartySize && configured != WorldOfDarknessConfig.AllianceGroup.Automatic
            ? (int)configured - 1 : raid.AllianceGroupForSlot(slot);
    }
}
