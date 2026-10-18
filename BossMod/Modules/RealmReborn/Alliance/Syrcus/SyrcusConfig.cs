namespace BossMod.RealmReborn.Alliance;

[ConfigDisplay(Name = "Syrcus Tower", Parent = typeof(RealmRebornConfig))]
public sealed class SyrcusConfig() : ConfigNode()
{
    public enum AllianceGroup { Automatic, A, B, C }
    [PropertyDisplay("Your alliance", tooltip: "Automatic uses the live alliance labels. Select A, B or C for a replay without alliance labels or a custom alliance arrangement.")]
    public AllianceGroup Alliance = AllianceGroup.Automatic;
}

static class SyrcusAlliance
{
    public static int Group(PartyState raid, int slot)
    {
        var configured = Service.Config.Get<SyrcusConfig>().Alliance;
        return slot is >= 0 and < PartyState.MaxPartySize && configured != SyrcusConfig.AllianceGroup.Automatic
            ? (int)configured - 1 : raid.AllianceGroupForSlot(slot);
    }
}
