namespace BossMod.RealmReborn.Alliance.A14Phlegethon;

class AncientFlare(BossModule module) : BossComponent(module)
{
    public static readonly WPos[] Platforms = [new(-148.65f, 191.99f), new(-110, 221.59f), new(-71.36f, 191.98f)];
    public const float PlatformRadius = 5;
    // Aim inside the 5y layout trigger, allowing 1y for edge uncertainty and navigation cells.
    public const float ArrivalRadius = 4;
    public DateTime Activation;
    private bool _visualResolved;
    private bool _preparing;
    public bool Active => Activation != default;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientFlare1)
        {
            Activation = Module.CastFinishAt(spell, -0.5);
            _visualResolved = false;
            _preparing = true;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientFlare1)
            _visualResolved = true;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == (uint)AID.AncientFlare1 && !_visualResolved)
        {
            Activation = default; // cancelled cast; successful casts retain the platform requirement until the reset
            _preparing = false;
        }
    }

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (actor.OID is not (0x1E8F89 or 0x1E8F88 or 0x1E8F87))
            return;
        if (state == 0x00040008)
            _preparing = true;
        else if (state == 0x00010002)
        {
            _preparing = false;
            _visualResolved = false;
            Activation = default;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (!_preparing && !Active)
            return;
        var group = LabyrinthAlliance.Group(Raid, slot);
        if (group < 0)
            hints.Add("Ancient Flare: alliance unknown - use your alliance's platform", Active);
        else
            hints.Add($"Ancient Flare: alliance {(char)('A' + group)} platform", Active && !actor.Position.InCircle(Platforms[group], ArrivalRadius));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var group = LabyrinthAlliance.Group(Raid, slot);
        if (Active && group >= 0)
            hints.AddForbiddenZone(new SDInvertedCircle(Platforms[group], ArrivalRadius), Activation);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var group = LabyrinthAlliance.Group(Raid, pcSlot);
        if ((_preparing || Active) && group >= 0)
            Arena.AddCircleUnfilled(Platforms[group], PlatformRadius, Colors.Safe);
    }
}
