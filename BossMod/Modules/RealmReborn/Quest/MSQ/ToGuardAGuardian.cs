namespace BossMod.RealmReborn.Quest.MSQ.ToGuardAGuardian;

public enum OID : uint
{
    Galfrid = 0x39A, // first battle ally; removed during the cutscene
    QuzalHuatotl = 0x39C,
    IxaliLostwing = 0x39D,
    IxaliSlowbeak = 0x39E,
    IxaliSincaller = 0x39F,
    IxaliLostwingOuter = 0x22A,
    IxaliSlowbeakOuter = 0x22B,
    IxaliDulltalon = 0x22C,
    IxaliSincallerOuter = 0x22D,
    Blanga = 0x3A0,
    LesserGargoyle = 0x3A1,
    MaskedMage = 0x3A2,
}

public enum AID : uint
{
    TerrorEye = 740, // Blanga->location, range 6 circle
    FireII = 147, // Masked Mage->target, range 5 splash (target follows cast)
    BlizzardII = 146, // Masked Mage->self, range 5 circle
}

sealed class TerrorEye(BossModule module) : Components.SimpleAOEs(module, (uint)AID.TerrorEye, 6f);
sealed class BlizzardII(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BlizzardII, 5f)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == (uint)OID.MaskedMage && !caster.IsAlly)
            base.OnCastStarted(caster, spell);
    }
}

sealed class FireII(BossModule module) : Components.BaitAwayCast(module, (uint)AID.FireII, 5f)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        // Papalymo uses the same action IDs: never treat friendly magic as danger.
        if (caster.OID == (uint)OID.MaskedMage && !caster.IsAlly)
            base.OnCastStarted(caster, spell);
    }
}

sealed class TargetPriority(BossModule module) : BossComponent(module)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var enemy in hints.PotentialTargets)
        {
            var target = enemy.Actor;
            if (enemy.Priority < 0 || target.IsAlly || !target.IsTargetable || target.IsDeadOrDestroyed)
                continue;
            enemy.Priority = (OID)target.OID switch
            {
                OID.Blanga or OID.LesserGargoyle => 5,
                OID.IxaliSincaller or OID.IxaliSincallerOuter => 4,
                OID.IxaliLostwing or OID.IxaliSlowbeak or OID.IxaliLostwingOuter or OID.IxaliSlowbeakOuter or OID.IxaliDulltalon => 3,
                OID.QuzalHuatotl or OID.MaskedMage => 1,
                _ => enemy.Priority
            };
        }
    }
}

sealed class ToGuardAGuardianStates : StateMachineBuilder
{
    public ToGuardAGuardianStates(BossModule module) : base(module)
    {
        // The cutscene removes Galfrid. A separate mage module covers the second battle.
        SimplePhase(default, id => SimpleState(id, 10000f, "Protect the Guardian Tree"), "Guardian Tree")
            .ActivateOnEnter<TerrorEye>().ActivateOnEnter<TargetPriority>()
            .Raw.Update = () => Module.PrimaryActor.IsDeadOrDestroyed;
    }
}

sealed class GuardianMageStates : StateMachineBuilder
{
    public GuardianMageStates(BossModule module) : base(module)
    {
        SimplePhase(default, id => SimpleState(id, 10000f, "Defeat the masked mage"), "Masked mage")
            .ActivateOnEnter<FireII>().ActivateOnEnter<BlizzardII>().ActivateOnEnter<TargetPriority>()
            .Raw.Update = () => AllDeadOrDestroyed([(uint)OID.MaskedMage, (uint)OID.LesserGargoyle]);
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65983, NameID = 617, PrimaryActorOID = (uint)OID.MaskedMage)]
public sealed class GuardianMage(WorldState ws, Actor primary) : BossModule(ws, primary, new(-310f, 172f), new ArenaBoundsCircle(35f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    protected override bool CheckPull() => WorldState.CurrentZone == 226 && WorldState.CurrentCFCID == 298
        && (IsAnyActorInCombat((uint)OID.MaskedMage) || IsAnyActorInCombat((uint)OID.LesserGargoyle));

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.MaskedMage));
        Arena.Actors(Enemies((uint)OID.LesserGargoyle));
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, GroupType = BossModuleInfo.GroupType.Quest, GroupID = 65983, NameID = 946, PrimaryActorOID = (uint)OID.Galfrid)]
public sealed class ToGuardAGuardian(WorldState ws, Actor primary) : BossModule(ws, primary, new(-310f, 172f), new ArenaBoundsCircle(35f, AllowObstacleMap: true, AllowDrawing3DArenaBounds: false))
{
    protected override bool CheckPull() => WorldState.CurrentZone == 226 && WorldState.CurrentCFCID == 298
        && WorldState.Actors.Any(a => !a.IsAlly && !a.IsDeadOrDestroyed && a.InCombat && a.OID is (uint)OID.QuzalHuatotl or (uint)OID.IxaliLostwing or (uint)OID.IxaliSlowbeak
            or (uint)OID.IxaliSincaller or (uint)OID.IxaliLostwingOuter or (uint)OID.IxaliSlowbeakOuter or (uint)OID.IxaliDulltalon or (uint)OID.IxaliSincallerOuter
            or (uint)OID.Blanga or (uint)OID.LesserGargoyle or (uint)OID.MaskedMage);

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var enemy in WorldState.Actors)
            if (enemy.Type == ActorType.Enemy && !enemy.IsAlly && enemy.IsTargetable && !enemy.IsDeadOrDestroyed && enemy.Position.InCircle(Center, Bounds.Radius))
                Arena.Actor(enemy);
    }
}
