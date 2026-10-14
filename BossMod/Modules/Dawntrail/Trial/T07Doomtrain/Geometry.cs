namespace BossMod.Dawntrail.Trial.T07Doomtrain;

// Pad placement is verified against the normal-duty layout and recorded launches.
// Terrain clearance is separate from damage geometry and never enlarges the arena.
sealed class CarGeometry : BossComponent
{
    public int Car { get; private set; } = 1;
    public bool Intermission { get; private set; }
    public bool EscapeOpen { get; private set; }
    public const float CarHeight = 15f;
    public List<Shape> Platforms = [];
    public AOEShape GroundShape = new AOEShapeRect(CarHeight, 10f, CarHeight);
    public AOEShape AirShape = new AOEShapeRect(CarHeight, 10f, CarHeight);

    public CarGeometry(BossModule module) : base(module)
    {
        // Recover the car on a fresh module instance (e.g. enabling mid-fight).
        SetCar(Math.Clamp((int)MathF.Round((module.PrimaryActor.Position.Z - 75f) / 50f) + 1, 1, 5));
        if (module.Enemies((uint)OID.AetherIntermission).Any(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.Position.InCircle(new(-400f, -400f), 1f)))
            EnterIntermission();
    }

    private void EnterIntermission()
    {
        Intermission = true;
        EscapeOpen = false;
        Arena.Center = new(-400f, -400f);
        // Tracks are scenery, not a disconnected walkable escape destination.
        Arena.Bounds = new ArenaBoundsCircle(14.5f);
    }

    public void SetCar(int car)
    {
        if (car == Car && !Intermission && Arena.Center == new WPos(100f, 50f * (car + 1)))
            return;
        Car = car;
        Intermission = false;
        EscapeOpen = false;
        Platforms.Clear();
        switch (car)
        {
            case 2: Car2(); break;
            case 3: Car3(); break;
            case 4: Car4(); break;
            case 5: Car5(); break;
            default:
                Arena.Center = new(100f, 100f);
                Arena.Bounds = new ArenaBoundsRect(10f, 15f);
                break;
        }
    }

    public override void OnMapEffect(byte index, uint state)
    {
        // New car becomes relevant at the transfer, before the boss reappears.
        if (index == 5 && state == 0x00020001) SetCar(2);
        else if (index == 6 && state == 0x00200001) SetCar(3);
        else if (index == 7 && state == 0x00020001) SetCar(4);
        else if (index == 24)
        {
            var car = state switch { 0x00010001u => 2, 0x00100010u => 3, 0x00200020u => 4, 0x00400040u => 5, _ => 0 };
            if (car != 0) SetCar(car);
        }
        else if (index == 0 && state == 0x00020001)
            EnterIntermission();
        else if (index == 0 && state == 0x00080004 && Intermission)
            SetCar(4);
        else if (index == 10 && Car == 4 && !Intermission)
        {
            if (state == 0x00020001)
            {
                EscapeOpen = true;
                // Both cars coexist during the jump. The lethal gap remains a hole.
                ArenaBoundsCustom transfer = new([new Rectangle(new(100f, 250f), 10f, 14.6f), new Rectangle(new(100f, 300f), 10f, 15f)]);
                Arena.Center = transfer.Center;
                Arena.Bounds = transfer;
            }
            else if (state == 0x00080004)
                EscapeOpen = false;
        }
    }

    // Draw the platform outlines here for the cars that have platforms
    // AirShape is defined in the respective car method.
    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        switch (Car)
        {
            case 3:
                AirShape.Outline(Arena, new WPos(100f, 200f), default, Colors.Border);
                break;
            case 5:
                AirShape.Outline(Arena, new WPos(100f, 300f), default, Colors.Border);
                break;
        }
    }

    private static readonly WPos EscapePad = new(100f, 262.5f);
    private bool EscapeWarning => !Intermission && Car == 4 && (EscapeOpen || Module.FindComponent<Derail>()?.Casters.Count > 0);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (EscapeWarning && actor.Position.Z < 275f)
            hints.Add(EscapeOpen ? "Use the rear jump pad!" : "Prepare to use the rear jump pad!", EscapeOpen);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Intermission)
            return;
        if (EscapeOpen)
            hints.Teleporters.Add(new(EscapePad, new(100f, 291.5f), 0.75f, false, 0.8f));
        else if (Car != 4 || Arena.Bounds is not ArenaBoundsCustom)
            // Transfer bounds contain both cars until the car-five event; never centre
            // a single-car clearance rectangle on the intervening lethal gap.
            // Keep the actor's centre clear of car walls/edges; the lethal bounds remain authoritative.
            hints.TemporaryObstacles.Add(new SDInvertedRect(Arena.Center, new WDir(0, 1), Car == 4 ? 14.1f : 14.5f, Car == 4 ? 14.1f : 14.5f, 9.5f));

        switch (Car)
        {
            case 2:
                // Recorded corner snag at (94.79, 160.53): bounds alone leave only a grid-cell margin.
                hints.TemporaryObstacles.Add(new SDRect(new(102.5f, 147.5f), new WDir(0, 1), 3f, 3f, 3f));
                hints.TemporaryObstacles.Add(new SDRect(new(97.5f, 157.5f), new WDir(0, 1), 3f, 3f, 3f));
                break;
            case 3:
                Platform(hints, true, 205f, 10f);
                Platform(hints, false, 205f, 10f);
                break;
            case 5:
                Platform(hints, true, 310f, 5f);
                Platform(hints, false, 300f, 5f);
                break;
        }
    }

    private static void Platform(AIHints hints, bool left, float z, float halfLength)
    {
        var edge = left ? 95f : 105f;
        var sign = left ? -1f : 1f;
        var wall = edge + sign * 0.25f;
        var pad = new WPos(left ? 96.5f : 103.5f, z);
        // The placed trigger has radius 0.75. Do not declare a transfer from outside it:
        // the old radius/offset could return an upper-deck waypoint while still against the pillar.
        hints.Teleporters.Add(new(pad, new(left ? 92.5f : 107.5f, z), 0.75f, false, 0.6f));

        // Separate upper floor from ground on the shared 2D map. Entry is through the pad,
        // descent through directed inward drops. End caps prevent walking up a vertical wall.
        hints.TemporaryObstacles.Add(new SDRect(new(wall, z - halfLength), new(wall, z + halfLength), 0.1f));
        hints.TemporaryObstacles.Add(new SDRect(new(left ? 90f : 105f, z - halfLength), new(left ? 95f : 110f, z - halfLength), 0.1f));
        hints.TemporaryObstacles.Add(new SDRect(new(left ? 90f : 105f, z + halfLength), new(left ? 95f : 110f, z + halfLength), 0.1f));
        // Drop away from the launch pad, so landing cannot immediately send us back up.
        // 0.8 seconds includes the observed fall plus a conservative movement allowance.
        for (var offset = -halfLength + 2f; offset <= halfLength - 2f; offset += 2f)
            if (MathF.Abs(offset) >= 2f)
                hints.Teleporters.Add(new(new(wall + sign * 0.75f, z + offset), new(edge - sign * 1.5f, z + offset), 0.75f, false, 0.8f));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (EscapeWarning && pc.Position.Z < 275f)
        {
            // Foreground outline stays visible over Derail's full-car danger fill.
            Arena.ZoneCircle(EscapePad, 0.75f, Colors.SafeFromAOE);
            Arena.AddCircleUnfilled(EscapePad, 1.5f, Colors.SafeFromAOE, 3f);
            Arena.AddLine(pc.Position, EscapePad, Colors.SafeFromAOE, 2f);
        }
        if (Intermission)
            return;
        if (Car is 3 or 5)
        {
            Arena.ZoneCircle(new(96.5f, Car == 3 ? 205f : 310f), 0.75f, Colors.SafeFromAOE);
            Arena.ZoneCircle(new(103.5f, Car == 3 ? 205f : 300f), 0.75f, Colors.SafeFromAOE);
        }
    }

    void Car2()
    {
        var carTwoCenter = new WPos(100f, 150f);
        Shape[] carOutline = [new Rectangle(carTwoCenter, 10f, 15f)];
        Shape[] carTwoCrates = [new Square(new(102.5f, 147.5f), 2.5f), new Square(new(97.5f, 157.5f), 2.5f)];

        ArenaBoundsCustom carTwo = new(carOutline, carTwoCrates);

        Arena.Center = new(100f, 150f);
        Arena.Bounds = carTwo;
    }

    void Car3()
    {
        // Car3 has a platform on the right and left side.  Each with a portal
        // to jump up
        var carThreeCenter = new WPos(100f, 200f);
        Shape[] carOutline = [new Rectangle(carThreeCenter, 10f, 15f)];
        //blocking shapes on ground level for platforms
        Platforms = [new Rectangle(new(92.5f, 205f), 2.5f, 10f), new Rectangle(new(107.5f, 205f), 2.5f, 10f)];

        // AOE shape for an AOE that would cover the car three lower deck.
        // used for head on emission (lower)
        GroundShape = new AOEShapeCustom(carThreeCenter, carOutline, Platforms);

        // AOE shape that covers both right and left platforms
        // Used for head on emission (upper)
        AirShape = new AOEShapeCustom(carThreeCenter, Platforms);

        // Upper decks stay navigable through the shared map and authored pads.
        ArenaBoundsCustom carThreeOutline = new(carOutline, WorldProjectionLayers:
            [new(PolygonClipper.GetCombinedPolygon(carThreeCenter, carOutline, Platforms), 0f, shared2DGroup: 0),
             new(PolygonClipper.GetCombinedPolygon(carThreeCenter, Platforms), 5f, shared2DGroup: 0)]);

        Arena.Center = carThreeCenter;
        Arena.Bounds = carThreeOutline;
    }

    void Car4()
    {
        Arena.Bounds = new ArenaBoundsRect(10f, 14.6f);
        Arena.Center = new(100f, 250f);
    }

    void Car5()
    {
        //car 5 has platforms on right and left placed slightly off from each other
        var carFiveCenter = new WPos(100f, 300f);
        Shape[] carOutline = [new Rectangle(carFiveCenter, 10f, 15f)];

        // upper deck should have rectangles in the same size as platforms.
        // with wall boundary on outer rectangle, no boundary inside outer rectangle bounds.
        // ai hints are used to set pathfinding above.
        Platforms = [new Rectangle(new(92.5f, 310f), 2.5f, 5f), new Rectangle(new(107.5f, 300f), 2.5f, 5f)];

        // AOE shape for an AOE that would cover car five lower deck.
        // used for head on emission (lower)
        GroundShape = new AOEShapeCustom(carFiveCenter, carOutline, Platforms);
        // AOE shape that covers both right and left platforms
        // Used for head on emission (upper)
        AirShape = new AOEShapeCustom(carFiveCenter, Platforms);

        ArenaBoundsCustom carFiveOutline = new(carOutline, WorldProjectionLayers:
            [new(PolygonClipper.GetCombinedPolygon(carFiveCenter, carOutline, Platforms), 0f, shared2DGroup: 0),
             new(PolygonClipper.GetCombinedPolygon(carFiveCenter, Platforms), 5f, shared2DGroup: 0)]);
        Arena.Center = carFiveCenter;
        Arena.Bounds = carFiveOutline;
    }
}
