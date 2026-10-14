namespace BossMod.Heavensward.Alliance.A32FerdiadHollow;

class A32FerdiadHollowStates : StateMachineBuilder
{
    public A32FerdiadHollowStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Blackbolt>()
            .ActivateOnEnter<Blackfire2>()
            .ActivateOnEnter<FerdiadRaidwides>()
            .ActivateOnEnter<BlackWind>()
            .ActivateOnEnter<DeathScythe>()
            .ActivateOnEnter<Debilitator>()
            .ActivateOnEnter<Icefall>()
            .ActivateOnEnter<LuckyPierrot2>()
            .ActivateOnEnter<FerdiadsFools>()
            .ActivateOnEnter<JestersReap>()
            .ActivateOnEnter<JestersReward>()
            .ActivateOnEnter<JongleursX>()
            .ActivateOnEnter<JugglingSphere>()
            .ActivateOnEnter<JugglingSphere2>()
            .ActivateOnEnter<Wormhole>()
            .ActivateOnEnter<PetrifyingEye>()
            .ActivateOnEnter<TransportedLuckyPierrot>();
    }
}
