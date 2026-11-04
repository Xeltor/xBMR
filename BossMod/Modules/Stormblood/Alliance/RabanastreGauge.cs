namespace BossMod.Stormblood.Alliance;

class RabanastreGauge(BossModule module) : BossComponent(module)
{
    private const uint GaugeUpdate = 0x8000000C;
    private const int GaugeMaximum = 10000;
    private readonly (uint Row, string Name, string Instruction) _gauge = module.PrimaryActor.OID switch
    {
        (uint)A11Mateus.OID.Boss => (12u, "Mateus's Aether", "Defeat the Azure Guards."),
        (uint)A12Hashmal.OID.Boss => (8u, "Hashmal's Mana", "Destroy the Command Tower."),
        (uint)A13Rofocale.OID.Boss => (13u, "Aetherial Acceleration", "Defeat the Archaeodemons."),
        (uint)A14Argath.OID.Boss => (10u, "Emptiness", "Destroy the Shards of Emptiness."),
        _ => (0u, "", ""),
    };
    private int? _value;

    public override void OnEventDirectorUpdate(uint updateID, uint param1, uint param2, uint param3, uint param4)
    {
        if (updateID == GaugeUpdate && _gauge.Row != 0 && param1 == _gauge.Row)
        {
            // The client reads the low word as signed and hides negative values.
            int value = unchecked((short)param2);
            _value = value < 0 ? null : Math.Min(value, GaugeMaximum);
        }
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_value is { } value && !Module.PrimaryActor.IsDeadOrDestroyed)
            hints.Add(value == GaugeMaximum ? $"{_gauge.Name} is full." : $"{_gauge.Name} at {value / 100}%. {_gauge.Instruction}");
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            _value = null;
    }

    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}
