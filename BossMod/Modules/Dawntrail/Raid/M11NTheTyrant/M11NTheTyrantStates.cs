namespace BossMod.Dawntrail.Raid.M11NTheTyrant;

sealed class M11NTheTyrantStates : StateMachineBuilder
{
    public M11NTheTyrantStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ArenaChanges>()
            .ActivateOnEnter<CrownOfArcadia>()
            .ActivateOnEnter<WeaponAOEs>()
            .ActivateOnEnter<VoidStardust>()
            .ActivateOnEnter<Cometite>()
            .ActivateOnEnter<DanceOfDomination>()
            .ActivateOnEnter<Explosion1>()
            .ActivateOnEnter<WallOfFireExplosion>()
            .ActivateOnEnter<RawSteelTankBuster>()
            .ActivateOnEnter<RawSteelSpreads>()
            .ActivateOnEnter<Charybdistopia>()
            .ActivateOnEnter<Maelstrom>()
            .ActivateOnEnter<PowerfulGust>()
            .ActivateOnEnter<OneAndOnly>()
            .ActivateOnEnter<CosmicKiss>()
            .ActivateOnEnter<MassiveMeteor>()
            .ActivateOnEnter<ForegoneFatality>()
            .ActivateOnEnter<Tyrannhilation>()
            .ActivateOnEnter<FlatlinerKnockUp>()
            .ActivateOnEnter<FlatlinerRaidwide>()
            .ActivateOnEnter<MajesticMeteor>()
            .ActivateOnEnter<MajesticMeteorain>()
            .ActivateOnEnter<FireAndFury>()
            .ActivateOnEnter<MammothMeteor>()
            .ActivateOnEnter<ExplosionTower>()
            .ActivateOnEnter<ExplosionKnockUp>()
            .ActivateOnEnter<LaunchPositionHints>()
            .ActivateOnEnter<ArcadionAvalancheToss>()
            .ActivateOnEnter<HeartbreakKick>()
            .ActivateOnEnter<GreatWallOfFire>();
    }
}
