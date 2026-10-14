using System.Numerics;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private const string PromathiaPrefix = "Dawntrail.Alliance.A34Promathia.";
    private static readonly (WPos Center, Angle Rotation)[] PromathiaPlatforms =
    [
        (new(-820, -807), default),
        (new(-808.724f, -826.5f), 120.Degrees()),
        (new(-831.258f, -826.5f), -120.Degrees())
    ];

    private static void PromathiaGenesis()
    {
        var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
        module.Arena.Bounds = new ArenaBoundsCircle(25);
        var genesis = Component<BossComponent>(module, PromathiaPrefix + "FalseGenesis");
        var changes = Component<BossComponent>(module, PromathiaPrefix + "ArenaChanges");
        var boss = Actor(100, 0x4DEE, new(-820, -844.5f));
        var player = Actor(1, 0, new(-820, -820), true);
        var cast = Cast(50343, boss.Position);
        var hints = new AIHints();
        genesis.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0, "idle False Genesis does not restrict movement");
        genesis.OnCastStarted(boss, cast);
        genesis.OnCastStarted(boss, cast);
        genesis.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 1, "False Genesis supplies required arrival from cast start, once");
        var zone = hints.ForbiddenZones[0];
        Check(zone.activation == module.CastFinishAt(cast), "platform arrival uses a fixed cast deadline");
        Check(zone.shapeDistance.Contains(player.Position), "the centre is forbidden at the floor-change deadline");
        Check(module.Arena.InBounds(player.Position), "the connecting floor remains walkable while preparing");
        foreach (var (center, rotation) in PromathiaPlatforms)
        {
            player.PosRot = new Vector4(center.X, -900, center.Z, 0);
            Check(!zone.shapeDistance.Contains(center), "each of the three squares is a legal arrival");
            Check(!zone.shapeDistance.Contains(center + rotation.ToDirection() * 6), "interior edge of each rotated square stays legal");
            Check(zone.shapeDistance.Contains(center + rotation.ToDirection() * 7), "outside each rotated square is forbidden");
            var text = new BossComponent.TextHints();
            genesis.AddHints(0, player, text);
            Check(text.Count == 0, "all three safe squares suppress the move-to-platform warning");
        }
        genesis.OnCastFinished(boss, cast);
        changes.OnCastFinished(boss, cast);
        hints = new();
        genesis.AddAIHints(0, player, default, hints);
        Check(hints.ForbiddenZones.Count == 0 && module.Arena.Bounds is ArenaBoundsCircle,
            "cancelled False Genesis removes preparation without destroying the floor");
        genesis.OnCastStarted(boss, Cast(50343, boss.Position));
        changes.OnEventCast(boss, Effect(50343));
        Check(!module.Arena.InBounds(new(-820, -820)), "successful False Genesis changes to disconnected platforms");
        foreach (var (center, _) in PromathiaPlatforms)
            Check(module.Arena.InBounds(center), "each platform remains inside the transformed arena");
        changes.OnMapEffect(0x0C, 0x00080004);
        genesis.OnMapEffect(0x0C, 0x00080004);
        hints = new();
        genesis.AddAIHints(0, player, default, hints);
        Check(module.Arena.Bounds is ArenaBoundsCircle && module.Arena.InBounds(new(-820, -820))
            && hints.ForbiddenZones.Count == 0, "arena restoration also clears pending preparation");
    }

    private static void PromathiaKnockback()
    {
        var module = Module(PromathiaPrefix + "A34Promathia", new(-820, -820));
        var changes = Component<BossComponent>(module, PromathiaPrefix + "ArenaChanges");
        changes.OnEventCast(Actor(100, 0x4DEE, new(-820, -844.5f)), Effect(50343));
        var seed = Component<SimpleKnockbacks>(module, PromathiaPrefix + "EmptySeed");
        var player = Actor(1, 0, PromathiaPlatforms[0].Center, true);
        module.WorldState.Party.Members[0] = new(0, player.InstanceID, false);
        var actors = new Actor?[PartyState.MaxAllies];
        actors[0] = player;
        Set(module.WorldState.Party, "_actors", actors);
        foreach (var (center, _) in PromathiaPlatforms)
        {
            var caster = Actor((ulong)(101 + seed.Casters.Count), 0x4DF0, center);
            var cast = Cast(50349, center);
            seed.OnCastStarted(caster, cast);
            seed.OnCastStarted(caster, cast);
        }
        Check(seed.Casters.Count == 3, "simultaneous sources remain distinct and duplicate starts are idempotent");
        for (var platform = 0; platform < 3; ++platform)
        {
            var (center, rotation) = PromathiaPlatforms[platform];
            player.PosRot = new Vector4(center.X, -900, center.Z, 0);
            var hints = new AIHints();
            seed.AddAIHints(0, player, default, hints);
            Check(hints.ForbiddenZones.Count == 1, "only the current platform restricts its player");
            var zone = hints.ForbiddenZones[0];
            Check(zone.activation == seed.Casters[platform].Activation, "knockback preparation has the source deadline");
            Check(zone.shapeDistance.Contains(center), "undefined radial direction at exact centre is unsafe");
            for (var corner = 0; corner < 4; ++corner)
            {
                var point = center + (rotation + (45 + corner * 90).Degrees()).ToDirection() * 4;
                Check(!zone.shapeDistance.Contains(point), "every corner barrier is a valid knockback direction");
                player.PosRot = new Vector4(point.X, -900, point.Z, 0);
                var movements = seed.CalculateMovements(0, player);
                Check(movements.Count == 1 && module.Arena.InBounds(movements[0].to), "corner knockback drawing stops on the platform");
                var edge = center + (rotation + (corner * 90).Degrees()).ToDirection() * 3;
                Check(zone.shapeDistance.Contains(edge), "an open edge is not a knockback barrier");
            }
        }
        var status = new ActorStatus((uint)BossMod.WHM.SID.Surecast, 0, Now.AddSeconds(20), 0);
        seed.OnStatusGain(player, ref status);
        var immuneHints = new AIHints();
        seed.AddAIHints(0, player, default, immuneHints);
        Check(immuneHints.ForbiddenZones.Count == 0 && seed.CalculateMovements(0, player).Count == 0,
            "immunity covering the impact disables both movement restriction and displacement");
        status.ExpireAt = Now.AddSeconds(1);
        seed.OnStatusGain(player, ref status);
        immuneHints = new();
        seed.AddAIHints(0, player, default, immuneHints);
        Check(immuneHints.ForbiddenZones.Count == 1, "immunity expiring before impact does not disable guidance");
        seed.OnMapEffect(0x0C, 0x00080004);
        Check(seed.Casters.Count == 0, "arena restoration clears interrupted knockbacks");
    }
}
