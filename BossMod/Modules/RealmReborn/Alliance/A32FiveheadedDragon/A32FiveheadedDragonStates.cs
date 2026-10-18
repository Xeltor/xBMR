namespace BossMod.RealmReborn.Alliance.A32FiveheadedDragon;

class A32FiveheadedDragonStates : StateMachineBuilder
{
    public A32FiveheadedDragonStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<WhiteBreath>()
            .ActivateOnEnter<BreathOfFire>()
            .ActivateOnEnter<BreathOfLight>()
            .ActivateOnEnter<BreathOfPoison>()
            .ActivateOnEnter<BreathOfIce>()
            .ActivateOnEnter<Radiance>()
            .ActivateOnEnter<Discordance>()
            .ActivateOnEnter<HeatWave>()
            .ActivateOnEnter<Markers>()
            .ActivateOnEnter<AddsAndPrey>();
    }
}
