namespace BossMod.Endwalker.VariantCriterion.V3Aloalo.V32Ketuduke;

// Variant OIDs/actions are different from Another Aloalo Island (Criterion).
// Reference: awgil/ffxiv_bossmod 40b0abdd35e81517409bdc81446910f943a75a49,
// V03Aloalo/V032Ketuduke.cs (BSD-3-Clause). Evidence/limits: docs/ketuduke-variant.md.
public enum OID : uint
{
    Boss = 0x4091,
    Helper = 0x233C,
    SpringCrystalSphere = 0x4092,
    SpringCrystalRect = 0x4093,
    AiryBubble = 0x4095,
    Zozone = 0x4096,
    SummonedApa = 0x4113,
    SummonedAnila = 0x4097,
    AloaloZaratan = 0x409A,
    AloaloOgrebon = 0x4098,
    BubbleStrewer = 0x1EB936
}

public enum AID : uint
{
    TidalRoar = 35493,
    TidalRoarAOE = 35494,
    SpringCrystals = 35449,
    SphereAppear = 35450,
    RectAppear = 35451,
    SaturateSphere = 35452,
    SaturateSphereFast = 35453,
    SaturateRect = 35454,
    SaturateRectFast = 35455,
    BubbleNet = 35456,
    BubbleNetAOE = 35457,
    FlukeTyphoon = 35460,
    FlukeTyphoonAOE = 35461,
    StrewnBubbles = 35462,
    SphereShatter = 35463,
    RecedingTwintides = 35485,
    FarTide = 35488,
    EncroachingTwintides = 35487,
    NearTide = 35486,
    HydrobombAOE = 35490,
    BlowingBubbles = 35464,
    Hydroblast = 35491,
    HydroblastAOE = 35492,
    WaterIII = 36116,
    WaterIIIApply = 36125,
    WaterIIIApplyUnstretched = 36126
}

public enum SID : uint
{
    Bubble = 3745
}

public enum TetherID : uint
{
    WaterIIIGood = 1,
    WaterIIIStretch = 57
}

sealed class TidalRoar(BossModule module) : Components.RaidwideCastDelay(module, (uint)AID.TidalRoar, (uint)AID.TidalRoarAOE, 1d)
{
    private Actor? source;
    private ActorCastInfo? visual;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.TidalRoar || visual == spell)
            return;
        source = caster;
        visual = spell;
        base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == source && spell == visual && !spell.EventHappened)
            Activation = default;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == source)
            Activation = default;
    }

    // One forecast per impact, including when the party contains a companion.
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Activation == default)
            return;
        BitMask affected = default;
        foreach (var (s, _) in Raid.WithSlot())
            affected.Set(s);
        if (affected != default)
            hints.AddPredictedDamage(affected, Activation);
    }
}

sealed class BubbleNet(BossModule module) : Components.RaidwideCast(module, (uint)AID.BubbleNetAOE);
sealed class Hydrobomb(BossModule module) : Components.SimpleAOEs(module, (uint)AID.HydrobombAOE, 5f);
sealed class Hydroblast(BossModule module) : Components.BaitAwayCast(module, (uint)AID.Hydroblast, 5f, tankbuster: true);

sealed class RouteCoverage(BossModule module) : BossComponent(module)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
        => hints.Add("Partial support: routes 2–4 mechanics are not implemented");
}

sealed class V32KetudukeStates : StateMachineBuilder
{
    public V32KetudukeStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RouteCoverage>()
            .ActivateOnEnter<TidalRoar>()
            .ActivateOnEnter<BubbleNet>()
            .ActivateOnEnter<SpringCrystals>()
            .ActivateOnEnter<StrewnBubbles>()
            .ActivateOnEnter<Twintides>()
            .ActivateOnEnter<Hydrobomb>()
            .ActivateOnEnter<Hydroblast>()
            .ActivateOnEnter<BlowingBubbles>()
            .ActivateOnEnter<WaterIII>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, Contributors = "Xeltor; reference: croizat", GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 961u, NameID = 12605u, SortOrder = 2, Category = BossModuleInfo.Category.VariantCriterion, Expansion = BossModuleInfo.Expansion.Endwalker)]
public sealed class V32Ketuduke(WorldState ws, Actor primary) : BossModule(ws, primary, ArenaCenter, new ArenaBoundsSquare(20f))
{
    // The perimeter is a DoT zone. These reference bounds also become native AI
    // pathfinding bounds, from the first active frame, on every route.
    // Physical boundary calibration and routes 2–4 remain pending.
    public static readonly WPos ArenaCenter = new(-790f, -395f);
}
