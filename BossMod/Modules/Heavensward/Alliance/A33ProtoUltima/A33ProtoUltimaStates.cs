namespace BossMod.Heavensward.Alliance.A33ProtoUltima;

class A33ProtoUltimaStates : StateMachineBuilder
{
    public A33ProtoUltimaStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Eikonizer>()
            .ActivateOnEnter<ProtoUltimaAdds>()
            .ActivateOnEnter<LightPillar>()
            .ActivateOnEnter<IntegratedAetheromodulator>()
            .ActivateOnEnter<DiffractiveLaser>()
            .ActivateOnEnter<Supernova>()
            .ActivateOnEnter<SupernovaEnrage>()
            .ActivateOnEnter<WreckingBall>()
            .ActivateOnEnter<AetherochemicalFlare>()
            .ActivateOnEnter<AetherochemicalLasers>()
            .ActivateOnEnter<CitadelBuster2>()
            .ActivateOnEnter<FlareStar>()
            .ActivateOnEnter<AetherialPool>()
            .ActivateOnEnter<Rotoswipe>();
    }
}
