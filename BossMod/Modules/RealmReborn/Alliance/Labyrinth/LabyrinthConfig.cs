namespace BossMod.RealmReborn.Alliance;

[ConfigDisplay(Name = "Labyrinth of the Ancients", Parent = typeof(RealmRebornConfig))]
public sealed class LabyrinthConfig() : ConfigNode()
{
    public enum AllianceGroup { Automatic, A, B, C }
    [PropertyDisplay("Your alliance", tooltip: "Automatic uses the live alliance labels. Select A, B or C for a replay without alliance labels or a custom alliance arrangement; an unknown assignment never selects another alliance's platform.")]
    public AllianceGroup Alliance = AllianceGroup.Automatic;
}

static class LabyrinthAlliance
{
    public static int Group(PartyState raid, int slot)
    {
        var configured = Service.Config.Get<LabyrinthConfig>().Alliance;
        return slot is >= 0 and < PartyState.MaxPartySize && configured != LabyrinthConfig.AllianceGroup.Automatic
            ? (int)configured - 1 : raid.AllianceGroupForSlot(slot);
    }
}
