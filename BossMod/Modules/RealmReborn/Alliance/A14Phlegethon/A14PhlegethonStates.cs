namespace BossMod.RealmReborn.Alliance.A14Phlegethon;

class A14PhlegethonStates : StateMachineBuilder
{
    public A14PhlegethonStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<MegiddoFlame2>()
            .ActivateOnEnter<MegiddoFlame3>()
            .ActivateOnEnter<MegiddoFlame4>()
            .ActivateOnEnter<MegiddoFlame5>()
            .ActivateOnEnter<MoonfallSlash>()
            .ActivateOnEnter<AncientFlare>()
            .ActivateOnEnter<AbyssalSlash>()
            .ActivateOnEnter<QuakeIII>()
            .ActivateOnEnter<VacuumSlash2>();

    }
}
