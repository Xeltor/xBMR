namespace BossMod.Heavensward.Alliance.A36DiabolosHollow;

class A36DiabolosHollowStates : StateMachineBuilder
{
    public A36DiabolosHollowStates(BossModule module) : base(module)
    {
        TrivialPhase()
        .ActivateOnEnter<Shadethrust>()
        .ActivateOnEnter<HollowCamisado>()
        .ActivateOnEnter<HollowNightmare>()
        .ActivateOnEnter<HollowOmen1>()
        .ActivateOnEnter<HollowOmen2>()
        .ActivateOnEnter<Blindside>()
        .ActivateOnEnter<EarthShaker2>()
        .ActivateOnEnter<Nox>()
        .ActivateOnEnter<HollowNight>()
        .ActivateOnEnter<HollowNightGaze>()
        .ActivateOnEnter<ParticleBeam>()
        .ActivateOnEnter<PavorInanis>()
        .ActivateOnEnter<HollowTerror>()
        .ActivateOnEnter<CriticalGravity>()
        .ActivateOnEnter<DoubleEdge>()
        .ActivateOnEnter<DamageBuffs>()
        .ActivateOnEnter<GateGuidance>()
        // Last: replace main-floor instructions for the separate portal participants.
        .ActivateOnEnter<PortalSafety>();
    }
}
