namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

// Quaqua, Variant Aloalo Island. Reference IDs/shapes: awgil/ffxiv_bossmod
// fff7f6e98541fcd6b23d7c2ccbac88e1fa4c9c75, V031Quaqua.cs (BSD-3-Clause).
// Route 1 thunder recording and remaining uncertainty: docs/quaqua-variant.md.
public enum OID : uint
{
    Boss = 0x40BA,
    Helper = 0x233C,
    AethericCharge = 0x40BB,
    DrakeFamiliar = 0x4135,
    HammerBeacon = 0x40E0,
    AnalaFamiliar = 0x4134,
    AnalaFamiliarLarge = 0x40BE
}

public enum AID : uint
{
    ArcaneArmaments = 35720,
    RavagingAxe = 35722,
    RingingQuoits = 35723,
    ArcaneArmamentsHammers = 35724,
    HammerLanding = 35725,
    HammerLandingRepeat = 35726,
    Rout = 35729,
    RoutRepeat = 35730,
    RoutPreview = 35731, // unnamed helper preview; not the damaging Rout action
    MadeMagic = 35732,
    VioletStorm = 35733,
    ScaldingWavesFirst = 35735,
    ScaldingWavesRest = 35736,
    CloudToGroundFirst = 35740,
    CloudToGroundRest = 35741
}

public enum TetherID : uint
{
    Axe = 256,
    Quoit = 257
}

sealed class MadeMagic(BossModule module) : Components.RaidwideCast(module, (uint)AID.MadeMagic);
sealed class VioletStorm(BossModule module) : Components.SimpleAOEs(module, (uint)AID.VioletStorm, new AOEShapeCone(32f, 60f.Degrees()));

sealed class RouteCoverage(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
        => hints.Add("Partial support: left path; middle/right path mechanics unsupported");
}

sealed class V31QuaquaStates : StateMachineBuilder
{
    public V31QuaquaStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RouteCoverage>()
            .ActivateOnEnter<MadeMagic>()
            .ActivateOnEnter<ArcaneArmaments>()
            .ActivateOnEnter<Rout>()
            .ActivateOnEnter<VioletStorm>()
            .ActivateOnEnter<CloudToGround>()
            .ActivateOnEnter<HammerLanding>()
            .ActivateOnEnter<ScaldingWaves>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "Xeltor; reference: croizat", GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 961u, NameID = 12527u, SortOrder = 1, Category = BossModuleInfo.Category.VariantCriterion, Expansion = BossModuleInfo.Expansion.Endwalker)]
public sealed class V31Quaqua(WorldState ws, Actor primary) : BossModule(ws, primary, ArenaCenter, new ArenaBoundsCircle(20f))
{
    // Exact o5d1/bg.lgb CollisionBox10146156 places the nominal R20 perimeter mesh here.
    // A module loaded during Rout must not recenter the arena on the dashing boss.
    // See docs/quaqua-variant.md for independent layout/mesh provenance and DoT limits.
    public static readonly WPos ArenaCenter = new(-538f, 94f);
}
