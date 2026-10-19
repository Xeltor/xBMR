namespace BossMod.RealmReborn.Alliance.A22Glasya;

class A22GlasyaStates : StateMachineBuilder
{
    public A22GlasyaStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ClockworkWrights>()
            .ActivateOnEnter<AstralPunch>()
            .ActivateOnEnter<Aura>()
            .ActivateOnEnter<VileUtterance>();
    }
}
