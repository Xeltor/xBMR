namespace BossMod.Stormblood.Alliance.A14Argath;

class A14ArgathStates : StateMachineBuilder
{
    public A14ArgathStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Heartless>()
            .ActivateOnEnter<Soulfix>()
            .ActivateOnEnter<ColdbloodProximity>()
            .ActivateOnEnter<ColdbloodDonut>()
            .ActivateOnEnter<GnawingDread>();
    }
}
