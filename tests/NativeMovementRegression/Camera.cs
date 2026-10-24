using BossMod.CombatAssistance.Camera;
using System.Numerics;

public static partial class Checks
{
    private static void CameraPolicies()
    {
        CombatCameraRejectsGroundClippingBeforeFraming();
        CombatCameraPrioritizesFightThenPartyThenRaid();
        CombatCameraFramesBossAndPartyWithinTheLens();
        CombatCameraLowersPitchForDistantAction();
        CombatCameraUsesBoundedShortestArcTurns();
        CombatCameraWidensForLargeFights();
        CombatCameraRejectsObstructedTransitions();
        CombatCameraZoomRespectsLimits();
    }
    private static void AssertTrue(bool value, string message) => Check(value, message);
    private static void AssertEqual<T>(T expected, T actual, string message) => Check(EqualityComparer<T>.Default.Equals(expected, actual), message);
    private static void CombatCameraRejectsGroundClippingBeforeFraming()
    {
        AssertEqual(0f, CombatCameraPolicy.GroundClearance(-0.2f, 0f), "camera below floor is clipped");
        AssertEqual(0f, CombatCameraPolicy.GroundClearance(0f, 0f), "camera on floor has no margin");
        AssertEqual(0.5f, CombatCameraPolicy.GroundClearance(0.3f, 0f), "low camera has partial margin");
        AssertEqual(1f, CombatCameraPolicy.GroundClearance(2f, 0f), "clear camera has full margin");
        var clipped = new CameraViewScore(1000f, 1000f, 1000f) { GroundClearance = 0f };
        var clear = new CameraViewScore(-1000f, -1000f, -1000f);
        AssertTrue(clear.Improves(clipped) && clear.CompareTo(clipped) > 0,
            "ground clearance must win over framing every subject");
        AssertTrue(!clipped.Improves(clear), "framing must not pull the camera into the floor");
        var recovering = clipped with { GroundClearance = 0.5f };
        AssertTrue(recovering.Improves(clipped), "allow gradual recovery from existing clipping");
        var hiddenCharacter = new CameraViewScore(1000f, 1000f, 1000f) { CharacterVisibility = 0.8f };
        var visibleCharacter = new CameraViewScore(-1000f, -1000f, -1000f);
        AssertTrue(visibleCharacter.Improves(hiddenCharacter),
            "an unobstructed character view must win over framing every subject");
        AssertTrue(!hiddenCharacter.Improves(visibleCharacter),
            "framing must not pull an object or ground between the camera and character");
    }
    private static void CombatCameraPrioritizesFightThenPartyThenRaid()
    {
        var clearFight = new CameraViewScore(10f, -1000f, -10000f);
        var allAlliesVisible = new CameraViewScore(9f, 0f, 0f);
        AssertTrue(clearFight.CompareTo(allAlliesVisible) > 0 && clearFight.Improves(allAlliesVisible),
            "offscreen allies must never outweigh a clearer fight");
        AssertTrue(!allAlliesVisible.Improves(clearFight), "allies cannot degrade fight visibility");
        var partyVisible = new CameraViewScore(10f, 0f, -10000f);
        var raidVisible = new CameraViewScore(10f, -1f, 0f);
        AssertTrue(partyVisible.CompareTo(raidVisible) > 0 && partyVisible.Improves(raidVisible),
            "party must win over raid at equal fight quality");
        AssertTrue(new CameraViewScore(10f, 0f, 0f).Improves(partyVisible),
            "raid visibility can break an equal fight and party tie");
        AssertTrue(!new CameraViewScore(10.01f, 0f, 0f).Improves(clearFight),
            "tiny fight score changes must retain the stable view");
    }
    private static void CombatCameraFramesBossAndPartyWithinTheLens()
    {
        var pivot = new Vector3(0f, 1.5f, 0f);
        var lens = new Vector2(1.36f, 2.42f);
        CameraSubject[] boss = [new(new Vector3(0f, 0f, -18f), 5f, 5f, 2f)];
        var centered = CombatCameraPolicy.FramingPenalty(boss, pivot, 0f, -0.33f, 16f, lens);
        var corner = CombatCameraPolicy.FramingPenalty(boss, pivot, 1f, -0.33f, 16f, lens);
        AssertTrue(centered < corner, "a clear sightline must not excuse leaving the boss at the edge");
        CameraSubject[] party = [new(new Vector3(0f, 0f, -10f), 1f, 2f, 2f),
            new(new Vector3(-20f, 0f, 0f), 0.5f, 2f, 1f)];
        AssertTrue(CombatCameraPolicy.FramingPenalty(party, pivot, 0.6f, -0.33f, 16f, lens) <
            CombatCameraPolicy.FramingPenalty(party, pivot, -0.6f, -0.33f, 16f, lens),
            "equivalent boss views should turn toward the party member otherwise left off screen");
        var behind = CombatCameraPolicy.Project(new Vector3(0f, 0f, 40f), pivot, 0f, -0.33f, 16f, lens);
        AssertTrue(MathF.Abs(behind.X) > 1f, "subjects behind the camera cannot count as framed");
        AssertTrue(CombatCameraPolicy.FramingPenalty(boss, pivot, 1f, -0.33f, 16f, lens * 1.5f) > corner,
            "a narrower native field of view must increase clipping pressure");
    }
    private static void CombatCameraLowersPitchForDistantAction()
    {
        var pivot = new Vector3(0f, 1.5f, 0f);
        var lens = new Vector2(1.36f, 2.42f);
        CameraSubject[] subjects = [new(Vector3.Zero, 0.5f, 2f, 1f),
            new(new Vector3(0f, 0f, -18f), 5.6f, 5.6f, 2f),
            new(new Vector3(0f, 0f, -27f), 0.5f, 2f, 1f)];
        AssertTrue(CombatCameraPolicy.FramingPenalty(subjects, pivot, 0f, -0.33f, 16.5f, lens) <
            CombatCameraPolicy.FramingPenalty(subjects, pivot, 0f, -0.61f, 16.5f, lens),
            "distant boss and tank need a shallower view instead of the fixed elevated pitch");
    }
    private static void CombatCameraUsesBoundedShortestArcTurns()
    {
        AssertEqual(0f, CombatCameraPolicy.TargetYaw(Vector3.Zero, new Vector3(0, 0, -10)), "north-facing camera bearing");
        AssertEqual(-MathF.PI / 2f, CombatCameraPolicy.TargetYaw(Vector3.Zero, new Vector3(10, 0, 0)),
            "an eastern fight needs the camera on the western side of the player");
        AssertTrue(CombatCameraPolicy.CameraOffset(-MathF.PI / 2f, 0f, 10f).X < -9.9f,
            "negative native yaw places the camera west as observed in the client");
        var offset = CombatCameraPolicy.CameraOffset(0f, -0.3f, 10f);
        AssertTrue(offset.Z > 0f && offset.Y > 0f, "north-facing view places camera south and above the character");
        AssertTrue(CombatCameraPolicy.ViewScore(1f, true, 0.5f, 0f, 0f) >
            CombatCameraPolicy.ViewScore(0.3f, true, 0f, 0f, 0f), "room takes priority over exact fight alignment");
        AssertTrue(CombatCameraPolicy.ViewScore(1f, true, 0f, 0f, 0.2f) >
            CombatCameraPolicy.ViewScore(1f, true, 0.2f, 0f, 0.2f), "keep an already useful view instead of chasing the center");
        AssertTrue(CombatCameraPolicy.ViewScore(1f, true, 1.5f, 0f, 3f) >
            CombatCameraPolicy.ViewScore(1f, true, 0f, 0f, 3f), "turn when the action is behind the camera");
        var heldAngle = 1f;
        for (var i = 0; i < 10000; i++)
        {
            var input = CombatCameraPolicy.ActiveInputDelta(heldAngle, 1f);
            AssertTrue(input != 0f, "holding the view must not relinquish input ownership to walking pivot");
            heldAngle += input;
            AssertTrue(MathF.Abs(heldAngle - 1f) <= 0.000011f, "active holding must not accumulate camera drift");
        }
        var from = 179f * MathF.PI / 180f;
        var target = -170f * MathF.PI / 180f;
        var next = CombatCameraPolicy.Step(from, target, 0.05f);
        var delta = MathF.Atan2(MathF.Sin(next - from), MathF.Cos(next - from));
        AssertTrue(delta > 0f && delta <= 2.01f * MathF.PI / 180f,
            "wraparound uses the short arc and respects the turn-rate cap");
        AssertEqual(0f, CombatCameraPolicy.Step(0f, 0.02f, 0.1f), "tiny target changes must not twitch the camera");
        AssertEqual(0f, CombatCameraPolicy.Step(0f, 1f, 0f), "initialization must not snap");
        var diagonalYaw = MathF.PI / 4f;
        AssertTrue(MathF.Abs(Vector3.Dot(CombatCameraPolicy.CameraRight(diagonalYaw),
            CombatCameraPolicy.CameraOffset(diagonalYaw, 0f, 1f))) < 0.00001f,
            "camera clearance samples must remain perpendicular to the viewing direction");
        var defaultPan = CombatCameraPolicy.Step(0f, 1f, 0.1f);
        AssertTrue(CombatCameraPolicy.Step(0f, 1f, 0.1f, 0.25f) < defaultPan, "slow panning must turn less per frame");
        AssertTrue(CombatCameraPolicy.Step(0f, 1f, 0.1f, 3f) > defaultPan, "fast panning must turn more per frame");
        AssertEqual(defaultPan, CombatCameraPolicy.Step(0f, 1f, 0.1f, float.NaN), "invalid speed must retain default behavior");
    
        var justOutsideTolerance = CombatCameraPolicy.Step(0f, 3.001f * MathF.PI / 180f, 0.1f);
        AssertTrue(justOutsideTolerance > 0f && justOutsideTolerance < 0.001f * MathF.PI / 180f,
            "crossing the framing tolerance must start smoothly instead of stepping camera speed");
        AssertTrue(CombatCameraPolicy.Step(0f, MathF.PI, 30f) <= 4.01f * MathF.PI / 180f,
            "a stalled frame cannot cause a large camera jump");
        var at30 = 0f;
        var at120 = 0f;
        for (var i = 0; i < 30; i++) at30 = CombatCameraPolicy.Step(at30, 1f, 1f / 30f);
        for (var i = 0; i < 120; i++) at120 = CombatCameraPolicy.Step(at120, 1f, 1f / 120f);
        AssertTrue(MathF.Abs(at30 - at120) < 0.02f, "framing must remain comparable across frame rates");
    }
    private static void CombatCameraWidensForLargeFights()
    {
        var pivot = new Vector3(0f, 1.5f, 0f);
        var lens = new Vector2(1.36f, 2.42f);
        CameraSubject[] massiveBoss = [new(Vector3.Zero, 0.5f, 2f, 1f),
            new(new Vector3(0f, 0f, -10f), 18f, 20f, 2f)];
        var close = CombatCameraPolicy.FramingPenalty(massiveBoss, pivot, 0f, -0.33f, 10f, lens);
        var wide = CombatCameraPolicy.FramingPenalty(massiveBoss, pivot, 0f, -0.33f, 20f, lens);
        AssertTrue(CombatCameraPolicy.ZoomImproves(close, wide, 10f), "a huge clipped boss needs a wider view");
        CameraSubject[] smallFight = [new(Vector3.Zero, 0.5f, 2f, 1f),
            new(new Vector3(0f, 0f, -5f), 1f, 2f, 2f)];
        close = CombatCameraPolicy.FramingPenalty(smallFight, pivot, 0f, -0.33f, 16f, lens);
        wide = CombatCameraPolicy.FramingPenalty(smallFight, pivot, 0f, -0.33f, 20f, lens);
        AssertTrue(!CombatCameraPolicy.ZoomImproves(close, wide, 4f), "an already readable small fight keeps its zoom");
        AssertTrue(!CombatCameraPolicy.ZoomImproves(1f, 0.9f, 2f), "tiny framing changes must not repeatedly move zoom");
        AssertTrue(!CombatCameraPolicy.ZoomImproves(float.NaN, 0f, 2f), "unknown coverage cannot authorize zoom");
        CameraSubject[] arena = [new(Vector3.Zero, 25f, 0f, 1f)];
        close = CombatCameraPolicy.ZoomCoveragePenalty(0f,
            CombatCameraPolicy.FramingPenalty(arena, pivot, 0f, -0.33f, 15f, lens));
        wide = CombatCameraPolicy.ZoomCoveragePenalty(0f,
            CombatCameraPolicy.FramingPenalty(arena, pivot, 0f, -0.33f, 20f, lens));
        AssertTrue(CombatCameraPolicy.ZoomImproves(close, wide, 5f),
            "a large authored arena can justify wider surroundings even when a small boss already fits");
    }
    private static void CombatCameraRejectsObstructedTransitions()
    {
        var clear = new CameraViewClearance(1f, 1f, 1f, 1f);
        AssertTrue(CombatCameraPolicy.AllowsTransition(clear, clear, true), "open space permits widening");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(clear, clear with { Room = 0.7f }, true), "wall-shortened boom rejects zoom");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(clear, clear with { Ground = 0.5f }, false), "slope rejects a low cinematic angle");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(clear, clear with { Character = 0.8f }, false), "foreground prop cannot hide the player");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(clear, clear with { Fight = 0.66f }, false), "a visible player cannot excuse an obscured fight");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(clear, clear with { Room = float.NaN }, false), "unknown camera clearance fails closed");
        var blocked = new CameraViewClearance(0.6f, 0.5f, 0.4f, 0.3f);
        AssertTrue(CombatCameraPolicy.AllowsTransition(blocked, clear, false), "allow recovery out of an obstructed view");
        AssertTrue(!CombatCameraPolicy.AllowsTransition(blocked, blocked, true), "a blocked view cannot authorize more zoom");
        var prettyButBlocked = new CameraViewScore(1000f, 1000f, 1000f) { FightVisibility = 0.66f };
        var readable = new CameraViewScore(-1000f, -1000f, -1000f);
        AssertTrue(readable.Improves(prettyButBlocked) && readable.CompareTo(prettyButBlocked) > 0,
            "unobstructed action must beat attractive framing and all allies");
        AssertTrue(!prettyButBlocked.Improves(readable), "framing never authorizes a wall-obscured fight");
    }
    private static void CombatCameraZoomRespectsLimits()
    {
        AssertEqual(10f, CombatCameraPolicy.ZoomStep(10f, 20f, 20f, 0f), "first frame cannot snap zoom");
        AssertTrue(CombatCameraPolicy.ZoomStep(10f, 20f, 20f, 30f) <= 10.401f, "stalled frame retains speed cap");
        AssertEqual(20f, CombatCameraPolicy.ZoomStep(20f, 50f, 20f, 0.1f), "native maximum cannot be expanded");
        AssertEqual(25f, CombatCameraPolicy.ZoomStep(25f, 20f, 20f, 0.1f), "never pull a user's wider view inward");
        AssertEqual(16f, CombatCameraPolicy.ZoomStep(16f, 10f, 20f, 0.1f), "smaller fights cannot cause zoom breathing");
        AssertEqual(10f, CombatCameraPolicy.ZoomStep(10f, float.NaN, 20f, 0.1f), "invalid zoom target retains the current view");
        var at30 = 10f;
        var at120 = 10f;
        for (var i = 0; i < 60; i++) at30 = CombatCameraPolicy.ZoomStep(at30, 20f, 20f, 1f / 30f);
        for (var i = 0; i < 240; i++) at120 = CombatCameraPolicy.ZoomStep(at120, 20f, 20f, 1f / 120f);
        AssertTrue(at30 > 10f && at30 < 20f && MathF.Abs(at30 - at120) < 0.03f, "smooth zoom must be comparable across frame rates");
    }
}
