namespace BossMod.Stormblood.Alliance.A11Mateus;

class BlizzardSphere(BossModule module) : BossComponent(module)
{
    private const float KiteDistance = 10f;

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Chasing(actor).Any())
            hints.Add("Keep away from your sphere and aim Chill away from the raid!");
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var sphere in Chasing(pc))
        {
            Arena.Actor(sphere, Colors.Object, true);
            Arena.AddLine(sphere.Position, pc.Position, Colors.Danger);
            if (sphere.CastInfo?.IsSpell(AID.Chill) != true)
                Arena.ZoneConeOutline(sphere.Position, 0f, Chill.Cone.Radius, sphere.AngleTo(pc), Chill.Cone.HalfAngle, Colors.Object);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var sphere in Chasing(actor))
        {
            var origin = sphere.Position;
            hints.AddForbiddenZone(new SDCircle(origin, KiteDistance), WorldState.CurrentTime);
            if (sphere.CastInfo?.IsSpell(AID.Chill) == true)
                continue;

            // Before the cast identifies the telegraph, favor an aim that clears the
            // current raid positions. The preference never overrides an active hazard.
            var aim = new ArcList(origin, Chill.Cone.Radius);
            BlockAimAt(Module.PrimaryActor.Position, Module.PrimaryActor.HitboxRadius);
            foreach (var ally in Raid.WithoutSlot(excludeNPCs: true))
                if (ally != actor)
                    BlockAimAt(ally.Position, ally.HitboxRadius + 0.25f);

            var blocked = aim.Forbidden.Segments.ToArray();
            hints.GoalZones.Add(position =>
            {
                var angle = Angle.FromDirection(position - origin).Rad;
                foreach (var (min, max) in blocked)
                    if (angle >= min && angle <= max)
                        return 0f;
                return 12f;
            });

            void BlockAimAt(WPos position, float radius)
            {
                var offset = position - origin;
                var distance = offset.Length();
                if (distance > Chill.Cone.Radius + radius)
                    return;
                var padding = Angle.Asin(Math.Min(1f, radius / Math.Max(distance, 0.01f)));
                aim.ForbidArcByLength(Angle.FromDirection(offset), Chill.Cone.HalfAngle + padding);
            }
        }
    }

    private IEnumerable<Actor> Chasing(Actor target) => Module.Enemies((uint)OID.BlizzardSphere)
        .Where(sphere => !target.IsDeadOrDestroyed && !sphere.IsDeadOrDestroyed
            && sphere.Tether.ID == (uint)TetherID.KiteTether && sphere.Tether.Target == target.InstanceID);
}

class FinRays(BossModule module) : Components.Cleave(module, (uint)AID.FinRays, new AOEShapeCone(11.8f, 60f.Degrees()), [(uint)OID.AzureGuard]);
