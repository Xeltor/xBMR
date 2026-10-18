namespace BossMod.RealmReborn.Alliance.A31AngraMainyu;

class A31AngraMainyuStates : StateMachineBuilder
{
    public A31AngraMainyuStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<DoubleVision>()
            .ActivateOnEnter<MortalGaze1>()
            .ActivateOnEnter<MortalGaze2>()
            .ActivateOnEnter<Stare>()
            .ActivateOnEnter<EyesOnMe>()
            .ActivateOnEnter<LevelMechanics>()
            .ActivateOnEnter<DoomCleanse>()
            .ActivateOnEnter<Adds>();
    }
}
