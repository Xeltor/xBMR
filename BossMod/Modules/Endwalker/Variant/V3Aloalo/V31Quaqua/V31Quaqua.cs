namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

// Quaqua, Variant Aloalo Island. Reference IDs/shapes: awgil/ffxiv_bossmod
// fff7f6e98541fcd6b23d7c2ccbac88e1fa4c9c75, V031Quaqua.cs (BSD-3-Clause).
// Route 1 thunder recording and remaining uncertainty: docs/quaqua-variant.md.
public enum OID : uint
{
    Boss = 0x40BA,
    Helper = 0x233C,
    AethericCharge = 0x40BB,
    DrakeFamiliar = 0x4135
}

public enum AID : uint
{
    ArcaneArmaments = 35720,
    RavagingAxe = 35722,
    RingingQuoits = 35723,
    Rout = 35729,
    RoutRepeat = 35730,
    RoutPreview = 35731, // unnamed helper preview; not the damaging Rout action
    MadeMagic = 35732,
    VioletStorm = 35733,
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
        => hints.Add("Partial support: thunder route 1; hammers and other path mechanics unsupported");
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
            .ActivateOnEnter<CloudToGround>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "Xeltor; reference: croizat", GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 961u, NameID = 12527u, SortOrder = 1, Category = BossModuleInfo.Category.VariantCriterion, Expansion = BossModuleInfo.Expansion.Endwalker)]
public sealed class V31Quaqua(WorldState ws, Actor primary) : BossModule(ws, primary, primary.Position, new ArenaBoundsCircle(20f))
{
    // Capture the arena center at spawn, never follow the boss's dashes. R20 is
    // the upstream reference bound, not a physically calibrated perimeter.
    // Native arena bounds constrain pathfinding from the first active frame.
}
