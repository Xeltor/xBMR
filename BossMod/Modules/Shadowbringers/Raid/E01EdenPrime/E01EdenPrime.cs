namespace BossMod.Shadowbringers.Raid.E01EdenPrime;

public enum OID : uint
{
    Boss = 0x2499,
    GuardianOfParadise = 0x249A,
    Orb = 0x249B,
    Eden = 0x249D,
    Helper = 0x233C,
    BlackHole = 0x1EAE20
}

public enum AID : uint
{
    EdensGravity = 15764,
    EdensFlare = 15767,
    DimensionalShift = 15772,
    PureBeamVisual = 15773,
    PureBeam = 15774,
    ParadiseLostVisual = 15775,
    ParadiseLost = 15776,
    SpearOfParadise = 15777,
    Heavensunder = 15778,
    PureLight = 15779,
    FragorMaximus = 15780,
    EternalBreath = 15781,
    PrimevalStasis = 15930,
    ManaSlice = 15782,
    ManaBurst = 15783,
    UntoDust = 15784,
    ParadisalDive = 15785,
    ViceAndVirtueVanity = 17636,
    ViceAndVirtueApathy = 17637,
    ViceOfVanity = 17639,
    ViceOfApathy = 17640,
    DeltaAttack = 17642,
    EdensFireIII = 17643,
    EdensBlizzardIII = 17644,
    EdensThunderIII = 17645,
    SunderPressure = 17663
}

public enum IconID : uint
{
    ViceOfApathy = 28,
    EdensFireIII = 96,
    ViceOfVanity = 172
}

sealed class EdensGravity(BossModule module) : Components.RaidwideCast(module, (uint)AID.EdensGravity);
sealed class DimensionalShift(BossModule module) : Components.RaidwideCast(module, (uint)AID.DimensionalShift);
sealed class FragorMaximus(BossModule module) : Components.RaidwideCast(module, (uint)AID.FragorMaximus);
sealed class SpearOfParadise(BossModule module) : Components.SingleTargetCast(module, (uint)AID.SpearOfParadise, "Two-hit tankbuster");
sealed class ManaBurst(BossModule module) : Components.RaidwideCast(module, (uint)AID.ManaBurst);
sealed class UntoDust(BossModule module) : Components.CastHint(module, (uint)AID.UntoDust, "Kill the adds before enrage!");
sealed class EdensFlare(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EdensFlare, new AOEShapeDonut(5f, 60f));
sealed class PureLight(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PureLight, new AOEShapeRect(60f, 30f));
sealed class PureBeam(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PureBeam, new AOEShapeRect(60f, 2f));
sealed class ParadiseLost(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ParadiseLost, 6f);
sealed class EdensBlizzardIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EdensBlizzardIII, 10f);
sealed class EdensThunderIII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EdensThunderIII, new AOEShapeRect(40f, 5f));
sealed class EdensFireIII(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID.EdensFireIII, 5f);
sealed class BlackHoles(BossModule module) : Components.Voidzone(module, 5f, m => m.Enemies((uint)OID.BlackHole).Where(a => !a.IsDestroyed && a.EventState != 7));

sealed class E01EdenPrimeStates : StateMachineBuilder
{
    public E01EdenPrimeStates(BossModule module) : base(module)
    {
        // Keep the boss instance through untargetability, the adds and Eternal Breath.
        TrivialPhase()
            .ActivateOnEnter<EdensGravity>()
            .ActivateOnEnter<DimensionalShift>()
            .ActivateOnEnter<FragorMaximus>()
            .ActivateOnEnter<SpearOfParadise>()
            .ActivateOnEnter<Heavensunder>()
            .ActivateOnEnter<EdensFlare>()
            .ActivateOnEnter<PureLight>()
            .ActivateOnEnter<PureBeam>()
            .ActivateOnEnter<ParadiseLost>()
            .ActivateOnEnter<EdensBlizzardIII>()
            .ActivateOnEnter<EdensThunderIII>()
            .ActivateOnEnter<EdensFireIII>()
            .ActivateOnEnter<ViceOfApathy>()
            .ActivateOnEnter<ViceOfVanity>()
            .ActivateOnEnter<BlackHoles>()
            .ActivateOnEnter<ManaBurst>()
            .ActivateOnEnter<UntoDust>()
            .ActivateOnEnter<ParadisalDive>()
            .ActivateOnEnter<GuardianRearSafety>()
            .ActivateOnEnter<EternalBreath>()
            .ActivateOnEnter<Guardians>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "Xel", PrimaryActorOID = (uint)OID.Boss,
    Expansion = BossModuleInfo.Expansion.Shadowbringers, Category = BossModuleInfo.Category.Raid,
    GroupType = BossModuleInfo.GroupType.CFC, GroupID = 653, NameID = 8345)]
public sealed class E01EdenPrime(WorldState ws, Actor primary) : BossModule(ws, primary, new(100f, 100f), new ArenaBoundsSquare(19.5f))
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.GuardianOfParadise));
        Arena.Actor(PrimaryActor);
    }
}
