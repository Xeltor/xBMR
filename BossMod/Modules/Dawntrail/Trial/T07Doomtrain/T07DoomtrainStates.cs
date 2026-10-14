namespace BossMod.Dawntrail.Trial.T07Doomtrain;

sealed class T07DoomtrainStates : StateMachineBuilder
{
    public T07DoomtrainStates(BossModule module) : base(module)
    {
        // Components follow their own observed cues; arena changes must not depend
        // on cast-order timeouts or the death of a cached intermission actor.
        TrivialPhase()
            .ActivateOnEnter<CarGeometry>()
            .ActivateOnEnter<LightningBurstTankBuster>()
            .ActivateOnEnter<LevinSignal>()
            .ActivateOnEnter<LightningExpress>()
            .ActivateOnEnter<WindpipeDrawIn>()
            .ActivateOnEnter<Blastpipe>()
            .ActivateOnEnter<UnlimitedExpress>()
            .ActivateOnEnter<ElectrayShort>()
            .ActivateOnEnter<ElectrayMedium>()
            .ActivateOnEnter<ElectrayLong>()
            .ActivateOnEnter<Electray3>()
            .ActivateOnEnter<ElectrayUpper>()
            .ActivateOnEnter<HeadOnEmission>()
            .ActivateOnEnter<AetherSurge>()
            .ActivateOnEnter<AetherialRay>()
            .ActivateOnEnter<RunawayTrain>()
            .ActivateOnEnter<RunawayTrainRaidwide>()
            .ActivateOnEnter<Shockwave>()
            .ActivateOnEnter<ArcaneRevelation>()
            .ActivateOnEnter<HailOfThunder>()
            .ActivateOnEnter<DerailmentSiegeCircle>()
            .ActivateOnEnter<Derail>();
    }
}
