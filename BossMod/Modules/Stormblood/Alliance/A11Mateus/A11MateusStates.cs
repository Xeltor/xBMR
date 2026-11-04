namespace BossMod.Stormblood.Alliance.A11Mateus;

class A11MateusStates : StateMachineBuilder
{
    public A11MateusStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RabanastreGauge>()
            .ActivateOnEnter<IceAzerBlizzard>()
            .ActivateOnEnter<HypothermalCombustion>()
            .ActivateOnEnter<IceSpiral>()
            .ActivateOnEnter<AquaSpheres>()
            .ActivateOnEnter<DarkBlizzardIII>()
            .ActivateOnEnter<WhiteWhisper>()
            .ActivateOnEnter<Chill>()
            .ActivateOnEnter<BlizzardIV>()
            .ActivateOnEnter<IceBubbleBlizzardIIITowers>()
            .ActivateOnEnter<FinRays>()
            .ActivateOnEnter<AzureGuards>()
            .ActivateOnEnter<FlashFreeze>()
            .ActivateOnEnter<Froth>()
            .ActivateOnEnter<Snowpierce>()
            .ActivateOnEnter<BlizzardSphere>()
            .ActivateOnEnter<Frosting>()
            .ActivateOnEnter<Hydrowave>();
    }
}
