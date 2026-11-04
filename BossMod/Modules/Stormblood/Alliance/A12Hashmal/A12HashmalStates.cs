namespace BossMod.Stormblood.Alliance.A12Hashmal;

class A12HashmalStates : StateMachineBuilder
{
    public A12HashmalStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RabanastreGauge>()
            .ActivateOnEnter<ExtremeEdge>()
            .ActivateOnEnter<TowerAppear>()
            .ActivateOnEnter<CommandTowerAppear>()
            .ActivateOnEnter<Towerfall>()
            .ActivateOnEnter<EarthHammer>()
            .ActivateOnEnter<JaggedEdge>()
            .ActivateOnEnter<EarthShaker>()
            .ActivateOnEnter<FallingRock>()
            .ActivateOnEnter<HashmalRaidwides>()
            .ActivateOnEnter<Sanction>()
            .ActivateOnEnter<WideTowerfall>()
            .ActivateOnEnter<RockCutter>()
            .ActivateOnEnter<FallingBoulder>()
            .ActivateOnEnter<ToDust>()
            .ActivateOnEnter<Landwaster>()
            .ActivateOnEnter<PennantstoneGolems>()
            .ActivateOnEnter<CommandTowerRaidwide>()
            .ActivateOnEnter<CommandTowerImpacts>()
            .ActivateOnEnter<CommandTowerKnockback>();
    }
}
