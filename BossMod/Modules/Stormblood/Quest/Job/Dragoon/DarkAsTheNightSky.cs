namespace BossMod.Stormblood.Quest.Job.Dragoon.DarkAsTheNightSky;

public enum OID : uint
{
    Boss = 0x1CDC, // Faunehm, R6.84; distinct from Dragon Sound's 0x1CDD
    Helper = 0x18D6,
    OrnKhai = 0x1CDF,
    LeftWingOfTragedy = 0x1D05,
    RightWingOfInjury = 0x1D06
}

public enum AID : uint
{
    AutoAttack = 870,
    AbyssicBuster = 8929, // 2s cast, range 25 + hitbox, 90-degree cone
    ChaoticVerse = 8930, // 2s cast, single-target damage and Vulnerability Up
    DarkStar = 8931, // 2s cast, raidwide
    Comet = 8932, // 5s visual; helper impact has its own later deadline
    CometImpact = 8933 // 6s location cast, distance-based damage; safe falloff unverified
}

sealed class AbyssicBuster(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AbyssicBuster, new AOEShapeCone(31.84f, 45f.Degrees()));
sealed class DarkStar(BossModule module) : Components.RaidwideCast(module, (uint)AID.DarkStar);
sealed class Comet(BossModule module) : Components.CastHint(module, (uint)AID.Comet, "Comet: move away from the impact marker!")
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Module.FindComponent<CometImpact>()?.Active != true)
            base.AddGlobalHints(actor, hints);
    }
}

// The 50y sheet range covers damage falloff, not a measured lethal exclusion.
// Keep the helper's deadline after the visual finishes, without inventing a safe radius.
sealed class CometImpact(BossModule module) : Components.RaidwideCast(module, (uint)AID.CometImpact, "Comet: move away from the impact marker!")
{
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var caster in Casters)
            if (caster.CastInfo is { } cast)
                Arena.ZoneCircleOutline(cast.LocXZ, 1f, Colors.Danger);
    }
}

sealed class ChaoticVerse(BossModule module) : Components.CastHint(module, (uint)AID.ChaoticVerse, "Chaotic Verse: targeted damage and vulnerability!")
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var caster in Casters)
        {
            // The opening cast hits the scripted NPC, who has no party slot.
            if (caster.CastInfo is { } cast && Raid.FindSlot(cast.TargetID) is var targetSlot && targetSlot >= 0)
                hints.AddPredictedDamage(new BitMask().WithBit(targetSlot), Module.CastFinishAt(cast));
        }
    }
}

sealed class Wings(BossModule module) : BossComponent(module)
{
    private bool _leftSeen;
    private bool _rightSeen;
    public bool Finished => _leftSeen && _rightSeen
        && Module.Enemies((uint)OID.LeftWingOfTragedy).All(a => a.IsDeadOrDestroyed)
        && Module.Enemies((uint)OID.RightWingOfInjury).All(a => a.IsDeadOrDestroyed);

    public override void OnActorCreated(Actor actor)
    {
        _leftSeen |= actor.OID == (uint)OID.LeftWingOfTragedy;
        _rightSeen |= actor.OID == (uint)OID.RightWingOfInjury;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_leftSeen && _rightSeen && !Finished)
            hints.Add("Defeat both of Faunehm's wings!");
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Priority >= 0 && !enemy.Actor.IsDeadOrDestroyed && enemy.Actor.IsTargetable && !enemy.Actor.IsAlly
                && enemy.Actor.OID is (uint)OID.LeftWingOfTragedy or (uint)OID.RightWingOfInjury)
                enemy.Priority = 1;
    }
}

sealed class FaunehmStates : StateMachineBuilder
{
    public FaunehmStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<AbyssicBuster>()
            .ActivateOnEnter<ChaoticVerse>()
            .ActivateOnEnter<Comet>()
            .ActivateOnEnter<CometImpact>()
            .ActivateOnEnter<DarkStar>()
            .ActivateOnEnter<Wings>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed || Module.FindComponent<Wings>()?.Finished == true;
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 68449, NameID = 6347)]
public sealed class Faunehm(WorldState ws, Actor primary) : BossModule(ws, primary, new(-340f, -393f), new ArenaBoundsCircle(30f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    // Provisional central planning envelope, not a measured physical/lethal edge.
    // See docs/encounters/dark-as-the-night-sky.md before expanding coverage.
    protected override bool CheckPull() => WorldState.CurrentZone == 713 && WorldState.CurrentCFCID == 267 && PrimaryActor.InCombat;

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.LeftWingOfTragedy));
        Arena.Actors(Enemies((uint)OID.RightWingOfInjury));
    }
}
