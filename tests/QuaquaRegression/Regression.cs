using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;
using BossMod.Endwalker.VariantCriterion.V3Aloalo.V31Quaqua;

static class Regression
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static Actor Add(BossModule module, ulong id, uint oid, WPos pos)
    {
        var actor = NewActor(id, oid, pos);
        module.WorldState.Actors.Actors.Add(id, actor);
        foreach (var entry in module.RelevantEnemies)
            if (entry.Key == oid) entry.Value.Add(actor);
        return actor;
    }

    private static Actor NewActor(ulong id, uint oid, WPos pos) => new(id, oid, 0, 0, "", 0, ActorType.Enemy, Class.None, 90,
        new Vector4(pos.X, 18, pos.Z, 0), hpmp: new(100, 100, 0, 100, 100));

    private static BossModule Module()
    {
        var world = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1) };
        var boss = NewActor(100, (uint)OID.Boss, new(-538, 94));
        boss.InCombat = boss.IsTargetable = true;
        world.Actors.Actors.Add(boss.InstanceID, boss);
        var module = BossModuleRegistry.CreateModuleForActor(world, boss)!;
        Check(module is V31Quaqua, "generated registry selects Variant Quaqua");
        module.Update();
        Check(module.Components.Count == 8, "generated state/component factories activate all eight components");
        return module;
    }

    private static GenericAOEs AOEs(BossModule module, string name) => module.Components.OfType<GenericAOEs>().Single(c => c.GetType().Name == name);
    private static ActorCastInfo Cast(Actor caster, AID id, WPos? pos = null, Angle rotation = default)
    {
        var p = pos ?? caster.Position;
        return caster.CastInfo = new() { Action = new(ActionType.Spell, (uint)id), Location = new(p.X, 18, p.Z), TotalTime = 4.7f, Rotation = rotation };
    }
    private static ActorCastEvent Effect(AID id, uint sequence) => new(new(ActionType.Spell, (uint)id), 0, 0, 0, default, sequence, 0, default);

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"), new Lumina.LuminaOptions());
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        ActionData(game);
        ArenaAndReset();
        Armaments();
        ArmamentsTiming();
        Dashes();
        Exaflares();
        Hammers();
        FairSkies();
        Console.WriteLine($"{assertions} Quaqua assertions passed (synthetic callbacks; physical/live validation pending).");
    }

    private static void ActionData(Lumina.GameData game)
    {
        // Original sheet fields are reference evidence, not server hitboxes.
        var sheet = game.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Lumina.Data.Language.English)!;
        foreach (var (id, type, range, width) in new (uint, int, int, int)[]
        {
            (35722, 2, 14, 0), (35723, 10, 18, 0), (35729, 12, 45, 16),
            (35730, 12, 45, 16), (35731, 12, 40, 10), (35733, 13, 32, 0),
            (35740, 2, 6, 0), (35741, 2, 6, 0),
            (35735, 12, 50, 8), (35736, 12, 50, 4)
        })
        {
            var row = sheet.GetRow(id);
            Check(row.CastType == type && row.EffectRange == range && row.XAxisModifier == width, "current Action fields match reference " + id);
        }
        var knockback = game.Excel.GetSheet<Lumina.Excel.Sheets.Knockback>()!.GetRow(96);
        Check(knockback.Distance == 20 && knockback.Direction == 6, "current hammer displacement row matches nominal 20-yalm radial push");
    }

    private static void ArenaAndReset()
    {
        using var module = Module();
        var center = module.Center;
        Check(module.Bounds is ArenaBoundsCircle && module.Bounds.Radius == 20, "uses explicit reference circle bounds");
        foreach (var d in new[] { new WDir(1, 0), new WDir(-1, 0), new WDir(0, 1), new WDir(0, -1) })
        {
            Check(module.Bounds.Contains(19.9f * d), "inside reference edge is retained");
            Check(!module.Bounds.Contains(20.1f * d), "outside reference edge is excluded");
        }
        var hints = new AIHints();
        module.CalculateAIHints(0, module.PrimaryActor, default, hints);
        Check(hints.PathfindMapBounds == module.Bounds && hints.PathfindMapCenter == center, "reference arena constrains native AI pathfinding");
        module.PrimaryActor.PosRot.X += 25;
        module.Update();
        Check(module.Center == center, "dash does not move the arena center");
        module.PrimaryActor.IsDead = true;
        module.Update();
        Check(module.Components.Count == 0, "death phase clears mechanic state");
        using var fresh = Module();
        Check(fresh.Components.OfType<GenericAOEs>().All(c => c.ActiveAOEs(0, fresh.PrimaryActor).IsEmpty), "fresh pull has no stale predictions");
    }

    private static void Armaments()
    {
        using var module = Module();
        var component = AOEs(module, "ArcaneArmaments");
        var boss = module.PrimaryActor;
        var first = Add(module, 101, (uint)OID.AethericCharge, new(-552, 94));
        var second = Add(module, 102, (uint)OID.AethericCharge, new(-531, 81.88f));
        foreach (var orb in new[] { first, second })
        {
            orb.Tether = new((uint)TetherID.Axe, boss.InstanceID);
            component.OnTethered(orb, orb.Tether);
        }
        var visual = Cast(boss, AID.ArcaneArmaments);
        component.OnCastStarted(boss, visual);
        Check(component.ActiveAOEs(0, boss).Length == 2, "tethers preceding boss cast are recovered");
        component.OnTethered(first, first.Tether);
        Check(component.ActiveAOEs(0, boss).Length == 2, "duplicate tether is idempotent");
        component.OnUntethered(first, first.Tether);
        visual.EventHappened = true;
        component.OnCastFinished(boss, visual);
        Check(component.ActiveAOEs(0, boss).Length == 2, "normal untether/visual finish retain sphere predictions");
        var cast = Cast(first, AID.RavagingAxe);
        component.OnCastStarted(first, cast);
        var aoe = component.ActiveAOEs(0, boss)[1];
        Check(aoe.Check(first.Position + new WDir(13.9f, 0)) && !aoe.Check(first.Position + new WDir(14.1f, 0)), "axe reference radius is 14");
        component.OnEventCast(first, Effect(AID.RavagingAxe, 1));
        component.OnEventCast(first, Effect(AID.RavagingAxe, 1));
        Check(component.ActiveAOEs(0, boss).Length == 1, "duplicate hit cannot retire another sphere");
        component.OnActorDestroyed(second);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "destruction retires only its sphere");
        component.OnCastStarted(first, Cast(first, AID.RingingQuoits));
        aoe = component.ActiveAOEs(0, boss)[0];
        Check(!aoe.Check(first.Position) && aoe.Check(first.Position + new WDir(5.1f, 0)) && !aoe.Check(first.Position + new WDir(18.1f, 0)), "donut preserves inner and outer safe space");
        component.OnCastFinished(first, first.CastInfo!);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "cancelled sphere cast retires its prediction");
        component.OnCastStarted(boss, Cast(boss, AID.ArcaneArmaments));
        component.OnCastFinished(boss, boss.CastInfo!);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "cancelled visual clears tether forecasts");

        // Recorded mixed-wave arrangement; expected pockets come from the
        // written axe/donut rules and reference dimensions, not AI output.
        var positions = new[] { new WPos(-545, 81.88f), new WPos(-550.12f, 87), new WPos(-524, 94), new WPos(-529, 109.59f), new WPos(-545, 106.12f) };
        for (var i = 0; i < positions.Length; ++i)
        {
            var orb = Add(module, (ulong)(110 + i), (uint)OID.AethericCharge, positions[i]);
            component.OnCastStarted(orb, Cast(orb, i is 1 or 3 ? AID.RavagingAxe : AID.RingingQuoits));
        }
        var shapes = component.ActiveAOEs(0, boss).ToArray();
        Check(shapes.All(a => !a.Check(positions[2])), "mixed wave retains the isolated east donut pocket");
        Check(shapes.All(a => !a.Check(positions[4])), "mixed wave retains the southwest donut pocket");
        Check(shapes.Any(a => a.Check(positions[0])), "donut adjacent to an axe is unsafe");
        var hints = new AIHints();
        component.AddAIHints(0, boss, default, hints);
        Check(hints.ForbiddenZones.Count == 5, "all five imminent sphere hazards reach native AI constraints");
    }

    private static void ArmamentsTiming()
    {
        var worstTravel = 0f;
        foreach (var pattern in new[] { "axes", "donuts", "mixed" })
        {
            using var module = Module();
            var component = AOEs(module, "ArcaneArmaments");
            var boss = module.PrimaryActor;
            var positions = pattern == "mixed"
                ? new[] { new WPos(-545, 81.88f), new WPos(-550.12f, 87), new WPos(-524, 94), new WPos(-529, 109.59f), new WPos(-545, 106.12f) }
                : new[] { new WPos(-552, 94), new WPos(-531, 81.88f), new WPos(-531, 106.12f) };
            for (var i = 0; i < positions.Length; ++i)
            {
                var orb = Add(module, (ulong)(101 + i), (uint)OID.AethericCharge, positions[i]);
                var id = pattern == "axes" || pattern == "mixed" && i is 1 or 3 ? AID.RavagingAxe : AID.RingingQuoits;
                var cast = Cast(orb, id);
                cast.TotalTime = 12.7f;
                component.OnCastStarted(orb, cast);
            }
            var forecast = component.ActiveAOEs(0, boss).ToArray();
            Check(forecast.Length == positions.Length && forecast.All(a => !a.Risky), pattern + " previews remain visible without early danger");
            var deadline = forecast[0].Activation;
            var hints = new AIHints();
            component.AddAIHints(0, boss, default, hints);
            Check(hints.ForbiddenZones.Count == 0, pattern + " early forecast cannot trigger an IPC safety escape");
            module.WorldState.Frame = new(deadline.AddSeconds(-5.01), 0, 0, 0, 0, 1);
            Check(component.ActiveAOEs(0, boss).ToArray().All(a => !a.Risky), pattern + " stays advisory just before the avoidance window");
            module.WorldState.Frame = new(deadline.AddSeconds(-5), 0, 0, 0, 0, 1);
            Check(component.ActiveAOEs(0, boss).ToArray().All(a => a.Risky && a.Activation == deadline), pattern + " avoidance starts five seconds before the unchanged impact");
            hints = new() { PathfindMapBounds = module.Bounds, PathfindMapCenter = module.Center };
            component.AddAIHints(0, boss, default, hints);
            Check(hints.ForbiddenZones.Count == positions.Length && hints.ForbiddenZones.All(z => z.activation == deadline), pattern + " exposes all simultaneous hazards at their true deadline");
            var player = NewActor(200, 0, module.Center);
            // Reference navigation fixtures cover the center, melee radius,
            // and outer arena. This is route-budget evidence, not live safety.
            foreach (var radius in new[] { 0f, 4f, 7.5f, 12f, 16f, 18f })
            {
                for (var i = 0; i < (radius == 0 ? 1 : 16); ++i)
                {
                    var pos = module.Center + radius * (i * 22.5f).Degrees().ToDirection();
                    player.PosRot = new(pos.X, 18, pos.Z, 0);
                    hints.Normalize();
                    var context = new BossMod.Pathfinding.NavigationDecision.Context();
                    var navigation = BossMod.Pathfinding.NavigationDecision.Build(context, module.WorldState.CurrentTime, hints, player);
                    if (navigation.Destination is { } destination)
                    {
                        Check(navigation.LeewaySeconds >= 0, pattern + " reference start retains travel time plus native reserve");
                        var goal = context.ThetaStar.CellCenter(context.ThetaStar.BestIndex());
                        Check(hints.ForbiddenZones.All(z => !z.shapeDistance.Contains(goal)), pattern + " final goal clears the entire simultaneous hazard union");
                        worstTravel = Math.Max(worstTravel, navigation.TimeToGoal);
                    }
                    else
                        Check(hints.ForbiddenZones.All(z => !z.shapeDistance.Contains(pos)), pattern + " an idle reference start is already safe");
                }
            }
        }
        Console.WriteLine($"Sphere reference navigation: worst travel {worstTravel:F3}s with 5s lead and native 1s reserve.");
    }

    private static void Dashes()
    {
        using var module = Module();
        var component = AOEs(module, "Rout");
        var boss = module.PrimaryActor;
        var start = Cast(boss, AID.Rout);
        component.OnCastStarted(boss, start);
        for (var i = 0; i < 4; ++i)
        {
            var helper = Add(module, (ulong)(101 + i), (uint)OID.Helper, new(-558 + 10 * i, 82));
            var cast = Cast(helper, AID.RoutPreview, rotation: (90f * i).Degrees());
            component.OnCastStarted(helper, cast);
            component.OnCastStarted(helper, cast);
            cast.EventHappened = true;
            component.OnCastFinished(helper, cast);
        }
        Check(component.ActiveAOEs(0, boss).Length == 4, "helper previews remain after finishing and duplicate starts do not duplicate charges");
        Check(component.ActiveAOEs(0, boss).ToArray().Count(a => a.Risky) == 1, "future dash previews cannot block every later lane prematurely");
        var shape = component.ActiveAOEs(0, boss)[0].Shape;
        Check(shape is AOEShapeRect rect && rect.LengthFront == 45 && rect.HalfWidth == 8, "uses damaging dash dimensions rather than narrower preview");
        for (uint i = 0; i < 4; ++i)
        {
            var hit = Effect(i == 0 ? AID.Rout : AID.RoutRepeat, i + 1);
            component.OnEventCast(boss, hit);
            component.OnEventCast(boss, hit);
            Check(component.ActiveAOEs(0, boss).Length == 3 - i, "each distinct dash retires exactly one preview");
            if (i < 3)
                Check(component.ActiveAOEs(0, boss)[0].Risky, "the next dash becomes the active avoidance constraint");
        }
        component.OnCastStarted(boss, Cast(boss, AID.Rout));
        component.OnCastFinished(boss, boss.CastInfo!);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "cancelled Rout clears its forecasts");
    }

    private static void Exaflares()
    {
        using var module = Module();
        var component = AOEs(module, "CloudToGround");
        var boss = module.PrimaryActor;
        var first = Add(module, 101, (uint)OID.DrakeFamiliar, new(-523, 80));
        var second = Add(module, 102, (uint)OID.DrakeFamiliar, new(-533, 74));
        foreach (var source in new[] { first, second })
        {
            var cast = Cast(source, AID.CloudToGroundFirst);
            component.OnCastStarted(source, cast);
            component.OnCastStarted(source, cast);
        }
        var aoes = component.ActiveAOEs(0, boss);
        Check(aoes.Length == 6, "two exaflares remain independent and bootstrap is idempotent");
        Check((aoes[1].Activation - aoes[0].Activation).TotalSeconds == 3.1, "first-to-second pulse has its distinct delay");
        Check((aoes[2].Activation - aoes[1].Activation).TotalSeconds == 2.1, "later pulses use 2.1s cadence");
        uint sequence = 1;
        foreach (var source in new[] { first, second })
        {
            // First line finishes seven pulses; second pauses with one left.
            var count = source == first ? 7 : 6;
            for (var i = 0; i < count; ++i)
            {
                var id = i == 0 ? AID.CloudToGroundFirst : AID.CloudToGroundRest;
                if (i != 0)
                    component.OnCastStarted(source, Cast(source, id, source.Position + new WDir(0, 6 * i)));
                var hit = Effect(id, sequence++);
                component.OnEventCast(source, hit);
                component.OnEventCast(source, hit);
            }
        }
        Check(component.ActiveAOEs(0, boss).Length == 1, "completed line retires while neighbor retains its final pulse");
        var final = component.ActiveAOEs(0, boss)[0].Origin;
        component.OnCastStarted(first, Cast(first, AID.CloudToGroundRest, final));
        Check(component.ActiveAOEs(0, boss).Length == 1, "reused helper takes over the pending line without creating a stale one");
        component.OnEventCast(first, Effect(AID.CloudToGroundRest, sequence++));
        Check(component.ActiveAOEs(0, boss).IsEmpty, "helper handoff completes both lines");
        component.OnCastStarted(first, Cast(first, AID.CloudToGroundFirst));
        component.OnCastFinished(first, first.CastInfo!);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "cancelled exaflare cast clears its line");
        component.OnCastStarted(first, Cast(first, AID.CloudToGroundFirst));
        component.OnActorDestroyed(first);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "destroyed exaflare source cannot leave a forecast");
    }

    private static void Hammers()
    {
        using var module = Module();
        var component = module.Components.OfType<GenericKnockback>().Single();
        var boss = module.PrimaryActor;
        var points = new[] { new WPos(-525, 101.5f), new WPos(-538, 79), new WPos(-551, 101.5f) };
        component.OnCastStarted(boss, Cast(boss, AID.ArcaneArmamentsHammers));
        for (var i = 0; i < 3; ++i)
        {
            var beacon = Add(module, (ulong)(101 + i), (uint)OID.HammerBeacon, points[i]);
            var status = new ActorStatus(2056, (ushort)(626 + i), Now.AddSeconds(15), 0);
            component.OnStatusGain(beacon, ref status);
        }
        var first = Cast(boss, AID.HammerLanding, points[0]);
        component.OnCastStarted(boss, first);
        component.OnCastStarted(boss, first);
        for (uint i = 0; i < 3; ++i)
        {
            var source = component.ActiveKnockbacks(0, boss);
            Check(source.Length == 1 && source[0].Origin == points[i] && source[0].Distance == 20, "hammer order comes from numbered beacon status");
            var hints = new AIHints();
            component.AddAIHints(0, boss, default, hints);
            Check(hints.ForbiddenZones.Count == 1, "next hammer supplies an AI landing constraint");
            Check(hints.ForbiddenZones[0].shapeDistance.Contains(points[i] + (points[i] - module.Center).Normalized()), "outward hammer landing is forbidden");
            Check(!hints.ForbiddenZones[0].shapeDistance.Contains(points[i] + (module.Center - points[i]).Normalized()), "inward hammer landing remains available");
            var hit = Effect(i == 0 ? AID.HammerLanding : AID.HammerLandingRepeat, i + 1);
            component.OnEventCast(boss, hit);
            component.OnEventCast(boss, hit);
            first.EventHappened = true;
            component.OnCastFinished(boss, first);
        }
        Check(component.ActiveKnockbacks(0, boss).IsEmpty, "three distinct hammer hits retire the sequence");
        component.OnCastStarted(boss, Cast(boss, AID.HammerLanding, points[0]));
        component.OnCastFinished(boss, boss.CastInfo!);
        Check(component.ActiveKnockbacks(0, boss).IsEmpty, "interrupted first hammer cancels pending impacts");
    }

    private static void FairSkies()
    {
        using var module = Module();
        var component = AOEs(module, "ScaldingWaves");
        var boss = module.PrimaryActor;
        var large = new[] { Add(module, 101, (uint)OID.AnalaFamiliarLarge, new(-546, 69)), Add(module, 102, (uint)OID.AnalaFamiliarLarge, new(-530, 69)) };
        foreach (var caster in large)
        {
            var cast = Cast(caster, AID.ScaldingWavesFirst);
            component.OnCastStarted(caster, cast);
            component.OnCastStarted(caster, cast);
        }
        var preview = component.ActiveAOEs(0, boss).ToArray();
        Check(preview.Length == 10 && preview.Count(a => a.Risky) == 2, "future cascades cannot require moving into initial beams");
        var firstTime = preview[0].Activation;
        foreach (var caster in large)
        {
            component.OnEventCast(caster, Effect(AID.ScaldingWavesFirst, (uint)caster.InstanceID));
            caster.CastInfo!.EventHappened = true;
            component.OnCastFinished(caster, caster.CastInfo);
        }
        Check(component.ActiveAOEs(0, boss).ToArray().Count(a => a.Risky) == 4, "cascade avoidance starts after both initial beams resolve");
        var helper = Add(module, 103, (uint)OID.AnalaFamiliar, module.Center);
        uint sequence = 200;
        for (var step = 0; step < 7; ++step)
        {
            module.WorldState.Frame = new(firstTime.AddSeconds(2.8 + 2.1 * step), 0, 0, 0, 0, 1);
            var xs = step < 3 ? new[] { -552f - 4 * step, -524f + 4 * step, -540f + 4 * step, -536f - 4 * step }
                : new[] { -540f + 4 * step, -536f - 4 * step };
            foreach (var x in xs)
            {
                helper.PosRot = new(x, 18, 94, 0);
                var effect = Effect(AID.ScaldingWavesRest, sequence++);
                var before = component.NumCasts;
                component.OnEventCast(helper, effect);
                component.OnEventCast(helper, effect);
                Check(component.NumCasts == before + 1, "one due pulse advances once despite helper reuse and overlapping branch positions");
            }
        }
        Check(component.ActiveAOEs(0, boss).IsEmpty, "all twenty fair-sky pulses retire with no stale lines");
        component.OnCastStarted(large[0], Cast(large[0], AID.ScaldingWavesFirst));
        component.OnCastFinished(large[0], large[0].CastInfo!);
        Check(component.ActiveAOEs(0, boss).IsEmpty, "cancelled initial beam clears both dependent cascades");
    }
}
