namespace BossMod.Heavensward.Alliance.A31DeathgazeHollow;

class A31DeathgazeHollowStates : StateMachineBuilder
{
    public A31DeathgazeHollowStates(A31DeathgazeHollow module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<DarkII>()
            .ActivateOnEnter<BoltOfDarkness3>()
            .ActivateOnEnter<VoidDeath>()
            .ActivateOnEnter<VoidDeathPull>()
            .ActivateOnEnter<VoidAeroII>()
            .ActivateOnEnter<BitingWind>()
            .ActivateOnEnter<VoidAeroIIIAOE>()
            .ActivateOnEnter<VoidBlizzardIIIAOE>()
            .ActivateOnEnter<ShipWalls>()
            .ActivateOnEnter<VoidAeroIIIKnockback>()
            .ActivateOnEnter<IceBlocks>()
            .ActivateOnEnter<VoidAeroIV>()
            .ActivateOnEnter<VoidBlizzardIV>()
            .ActivateOnEnter<VoidDeathIV>()
            .ActivateOnEnter<FloorStrikes>()
            .ActivateOnEnter<Doom>();
    }
}
