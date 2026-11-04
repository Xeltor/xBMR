namespace BossMod.Stormblood.Alliance.A13Rofocale;

class A13RofocaleStates : StateMachineBuilder
{
    public A13RofocaleStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RabanastreGauge>()
            .ActivateOnEnter<Maverick>()
            .ActivateOnEnter<CryOfVictory>()
            .ActivateOnEnter<Trample>()
            .ActivateOnEnter<Chariot>()
            .ActivateOnEnter<CrushWeapon>()
            .ActivateOnEnter<Karma>()
            .ActivateOnEnter<UnholyDarkness>()
            .ActivateOnEnter<Archaeodemons>()
            .ActivateOnEnter<AetherialAcceleration>()
            .ActivateOnEnter<CrushHelm>()
            .ActivateOnEnter<HeavenlySubjugation>()
            .ActivateOnEnter<PompAndCircumstance>()
            .ActivateOnEnter<Embrace>()
            // Select an interaction only after the other components publish their hazards.
            .ActivateOnEnter<DarkGeas>();
    }
}
