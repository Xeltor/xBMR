namespace BossMod.Stormblood.Alliance.A12Hashmal;

class A12HashmalStates : StateMachineBuilder
{
    public A12HashmalStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ExtremeEdge>()
            .ActivateOnEnter<TowerAppear>()
            .ActivateOnEnter<CommandTowerAppear>()
            .ActivateOnEnter<Towerfall>()
            .ActivateOnEnter<EarthHammer>();
    }
}
