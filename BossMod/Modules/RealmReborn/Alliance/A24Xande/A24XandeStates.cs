namespace BossMod.RealmReborn.Alliance.A24Xande;

class A24XandeStates : StateMachineBuilder
{
    public A24XandeStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<KnucklePress>()
            .ActivateOnEnter<BurningRave1>()
            .ActivateOnEnter<BurningRave2>()
            .ActivateOnEnter<AncientQuake>()
            .ActivateOnEnter<AetherialMines>()
            .ActivateOnEnter<ImperiumAndQuaga>()
            //.ActivateOnEnter<Stackmarker>()
            .ActivateOnEnter<AuraCannon>();
    }
}
