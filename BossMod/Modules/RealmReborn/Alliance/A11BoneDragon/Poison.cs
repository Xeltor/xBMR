namespace BossMod.RealmReborn.Alliance.A11BoneDragon;

class Poison(BossModule module) : BossComponent(module)
{
    private int _stage;
    private ShapeDistance? _danger;
    public override void OnLegacyMapEffect(byte seq, byte param, byte[] data)
    {
        if (data.Length < 3)
            return;
        if (data[0] == 7)
            SetStage(0);
        else if (data[0] == 3)
            switch (data[2])
            {
                case 0x10: SetStage(1); break;
                case 0x20: SetStage(2); break;
                case 0: case 0x40: case 0x80: SetStage(0); break;
            }
    }
    public override void OnActorDestroyed(Actor actor)
    {
        if (actor == Module.PrimaryActor)
            SetStage(0);
    }
    private void SetStage(int stage)
    {
        _stage = stage;
        if (stage == 0)
        {
            _danger = null;
            return;
        }
        List<ShapeDistance> safe = [new SDInvertedCircle(Arena.Center, 8)];
        for (var i = 0; i < 8; ++i)
        {
            var rotation = (45 * i).Degrees();
            if (stage == 1)
                safe.Add(new SDInvertedRect(Arena.Center + new WDir(0, 31.87f).Rotate(rotation), rotation, 17.675f, 17.675f, 3.9f));
            else
                foreach (var radius in new[] { 18.02f, 32.13f, 45.745f })
                    safe.Add(new SDInvertedRect(Arena.Center + new WDir(0, radius).Rotate(rotation), rotation, 2.95f, 2.95f, 2.95f));
        }
        _danger = new SDIntersection([.. safe]);
    }
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_danger?.Distance(actor.Position) <= 0)
            hints.Add("Poison floor: move onto a raised platform!");
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_danger != null)
            hints.AddForbiddenZone(_danger);
    }
    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        if (_stage == 0)
            return;
        Arena.ZoneCircle(Arena.Center, 49.8f, Colors.AOE);
        Arena.ZoneCircle(Arena.Center, 8, Colors.SafeFromAOE);
        for (var i = 0; i < 8; ++i)
        {
            var rotation = (45 * i).Degrees();
            if (_stage == 1)
                Arena.ZoneRect(Arena.Center + new WDir(0, 31.87f).Rotate(rotation), rotation, 17.675f, 17.675f, 3.9f, Colors.SafeFromAOE);
            else
                foreach (var radius in new[] { 18.02f, 32.13f, 45.745f })
                    Arena.ZoneRect(Arena.Center + new WDir(0, radius).Rotate(rotation), rotation, 2.95f, 2.95f, 2.95f, Colors.SafeFromAOE);
        }
    }
}
