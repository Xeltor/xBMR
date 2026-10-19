namespace BossMod.RealmReborn.Alliance.A11BoneDragon;

class A11BoneDragonStates : StateMachineBuilder
{
    public A11BoneDragonStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Platinals>()
            .ActivateOnEnter<Apocalypse>()
            .ActivateOnEnter<EvilEye>()
            .ActivateOnEnter<Stone>()
            .ActivateOnEnter<Level5Petrify>()
            .ActivateOnEnter<MiasmaBreath>()
            .ActivateOnEnter<Poison>()
            .Raw.Update = () => Module.PrimaryActor.IsDestroyed;
    }
}
