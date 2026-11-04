namespace BossMod.Stormblood.Alliance.A14Argath;

class A14ArgathStates : StateMachineBuilder
{
    public A14ArgathStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<RabanastreGauge>()
            .ActivateOnEnter<Heartless>()
            .ActivateOnEnter<JudgmentBlade>()
            .ActivateOnEnter<BleedingTiles>()
            .ActivateOnEnter<Soulfix>()
            .ActivateOnEnter<ColdbloodDeparture>()
            .ActivateOnEnter<ColdbloodProximity>()
            .ActivateOnEnter<ColdbloodDonut>()
            .ActivateOnEnter<GnawingDread>()
            .ActivateOnEnter<CrushWeapon>()
            .ActivateOnEnter<CripplingBlow>()
            .ActivateOnEnter<Raidwides>()
            .ActivateOnEnter<DivineCommandment>()
            .ActivateOnEnter<Unrelenting>()
            .ActivateOnEnter<Trepidation>()
            .ActivateOnEnter<RoyalBlood>()
            .ActivateOnEnter<EmptySoul>()
            .ActivateOnEnter<LethalRim>()
            .OnExit(() => module.Arena.Bounds = A14Argath.InnerPlatform);
    }
}
