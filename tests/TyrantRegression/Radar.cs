using System.Text.Json;
using BossMod;
using BossMod.Components;
using BossMod.Dawntrail.Raid.M11NTheTyrant;

static partial class Checks
{
    private static void RadarWaves()
    {
        foreach (var (name, firstAction, laterAction, radius) in new (string, uint, uint, float)[]
        {
            ("PowerfulGust", 46041, 46041, 60),
            ("Cometite", 46026, 46026, 4),
            ("Explosion1", 46035, 47033, 60),
            ("MajesticMeteor", 46058, 46058, 6)
        })
        {
            using var m = Module(); var p = Player(m, 1, new(100, 100));
            var component = Component<GenericAOEs>(m, name);
            // Deliberately create the later wave first; cast arrival order is not resolution order.
            var later = Create(m, Actor(10, (uint)OID.Helper, new(100, 100)));
            var first = Create(m, Actor(11, (uint)OID.Helper, new(100, 100)));
            var sameWave = Create(m, Actor(12, (uint)OID.Helper, new(100, 100)));
            Start(m, later, laterAction, 7.7f, 45f.Degrees());
            Start(m, first, firstAction, 5.7f);
            Start(m, sameWave, firstAction, 5.75f, 90f.Degrees());
            var aoes = component.ActiveAOEs(0, p).ToArray();
            Check(aoes.Count(a => a.Color == Colors.Danger) == 2 && aoes.Single(a => a.ActorID == 10).Color == Colors.AOE, name + " highlights the complete earliest wave, previews the next");
            Check(aoes[0].ActorID == 10 && aoes[^1].ActorID == 11, name + " draws previews below imminent hazards");
            Check(aoes.Count(a => a.Activation <= AOESequence.PreviewDeadline(aoes)) == 3, name + " keeps the entire current and next wave visible");
            Check(aoes.All(a => a.Risky), name + " never disables future AI hazards to change presentation");
            var hints = Hints(component, p);
            Check(hints.ForbiddenZones.Count == 3 && hints.ForbiddenZones.Max(z => z.activation) == Now.AddSeconds(8), name + " preserves every AI deadline");
            Check(aoes.All(a => a.Shape is AOEShapeCircle circle ? circle.Radius == radius : a.Shape is AOEShapeRect rect ? rect.LengthFront == radius : a.Shape is AOEShapeCone cone && cone.Radius == radius), name + " retains authored dimensions");
            // Adjacent frame arrivals belong to the same wave, while the later source remains a preview.
            Finish(m, first);
            Check(component.ActiveAOEs(0, p).ToArray().Single(a => a.ActorID == 12).Color == Colors.Danger, name + " retains unfinished siblings in the current wave");
            Finish(m, sameWave);
            Check(component.ActiveAOEs(0, p)[0].Color == Colors.Danger, name + " promotes the next wave after prior sources retire");
            m.WorldState.Execute(new ActorState.OpCastInfo(10, null));
            Check(component.ActiveAOEs(0, p).Length == 0, name + " clears a cancelled remaining wave");
        }
        using (var m = Module())
        {
            var p = Player(m, 1, new(100, 100));
            var component = Component<GenericAOEs>(m, "MajesticMeteor");
            for (var i = 0; i < 3; ++i)
            {
                var h = Create(m, Actor((ulong)(10 + i), (uint)OID.Helper, new(100, 100)));
                Start(m, h, 46058, 3.7f + 2 * i);
            }
            var aoes = component.ActiveAOEs(0, p).ToArray();
            Check(aoes.Length == 3 && Hints(component, p).ForbiddenZones.Count == 3, "third meteor wave remains available internally");
            Check(aoes.Count(a => a.Activation <= AOESequence.PreviewDeadline(aoes)) == 2, "third meteor wave does not add extra radar shading");
            Finish(m, m.WorldState.Actors.Find(10)!);
            aoes = component.ActiveAOEs(0, p).ToArray();
            Check(aoes.Length == 2 && aoes.All(a => a.Activation <= AOESequence.PreviewDeadline(aoes)), "previously hidden third wave becomes the next radar preview");
        }
        // Dance's observed half-second offset is a distinct wave, not a same-frame batch.
        using (var m = Module())
        {
            var p = Player(m, 1, new(100, 100));
            var first = Create(m, Actor(10, (uint)OID.Helper, new(100, 100)));
            var second = Create(m, Actor(11, (uint)OID.Helper, new(100, 100)));
            Start(m, first, 46035, 5.7f); Start(m, second, 47033, 6.2f);
            var aoes = Component<GenericAOEs>(m, "Explosion1").ActiveAOEs(0, p).ToArray();
            Check(aoes.Single(a => a.ActorID == 10).Color == Colors.Danger && aoes.Single(a => a.ActorID == 11).Color == Colors.AOE, "Dance's 0.5s stagger remains visually distinct");
        }
        // Simultaneous opposite Fire and Fury cones must both remain hazards.
        using (var m = Module())
        {
            var p = Player(m, 1, new(100, 100));
            var first = Create(m, Actor(10, (uint)OID.Helper, new(100, 100)));
            var second = Create(m, Actor(11, (uint)OID.Helper, new(100, 100)));
            Start(m, first, 46072, 5.7f); Start(m, second, 46073, 5.7f, 180f.Degrees());
            var component = Component<GenericAOEs>(m, "FireAndFury");
            Check(component.ActiveAOEs(0, p).Length == 2 && Hints(component, p).ForbiddenZones.Count == 2, "simultaneous Fire and Fury cones are not treated as alternate waves");
        }
    }

    private static void RecordedGustWaves()
    {
        using var m = Module(); var p = Player(m, 1, new(100, 100));
        var gust = Component<GenericAOEs>(m, "PowerfulGust");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GustCallbacks.json")));
        long ticks = 0; var reached = 0;
        foreach (var row in doc.RootElement.GetProperty("events").EnumerateArray())
        {
            var next = row.GetProperty("ticks").GetInt64();
            foreach (var actor in m.WorldState.Actors.Actors.Values)
                if (actor.CastInfo is {} cast) cast.ElapsedTime += (float)((next - ticks) / 1e7);
            ticks = next; m.WorldState.Frame = new(Now.AddTicks(ticks), 0, 0, 0, 0, 1);
            var id = row.GetProperty("actor").GetUInt64();
            var actorNow = m.WorldState.Actors.Find(id);
            switch (row.GetProperty("kind").GetString())
            {
                case "cast_start":
                    var pos = row.GetProperty("position");
                    actorNow ??= Create(m, Actor(id, (uint)OID.Helper, new(pos.GetProperty("x").GetSingle(), pos.GetProperty("z").GetSingle())));
                    var ci = Cast(46041, actorNow.Position, row.GetProperty("cast_total").GetSingle(), new(pos.GetProperty("facing").GetSingle()));
                    ci.ElapsedTime = row.GetProperty("cast_elapsed").GetSingle();
                    m.WorldState.Execute(new ActorState.OpCastInfo(id, ci));
                    break;
                case "action": Event(m, actorNow!, 46041); break;
                case "cast_end": m.WorldState.Execute(new ActorState.OpCastInfo(id, null)); break;
            }
            var seq = row.GetProperty("seq").GetInt32();
            if (seq == 92357)
            {
                var aoes = gust.ActiveAOEs(0, p).ToArray();
                Check(aoes.Length == 16 && aoes.Count(a => a.Color == Colors.Danger) == 8 && aoes.Count(a => a.Color == Colors.AOE) == 8, "recorded cardinal wave is distinct from all eight upcoming diagonal cones");
                Check(Hints(gust, p).ForbiddenZones.Count == 16, "recorded future cones retain their timed AI constraints");
                Check((aoes.Max(a => a.Activation) - aoes.Min(a => a.Activation)).TotalSeconds is > 2 and < 2.02, "recorded gust deadlines retain observed two-second separation");
                Check(aoes.Take(8).All(a => a.Color == Colors.AOE), "recorded previews draw before imminent cones");
                reached++;
            }
            if (seq == 94388)
            {
                var aoes = gust.ActiveAOEs(0, p).ToArray();
                Check(aoes.Length == 8 && aoes.All(a => a.Color == Colors.Danger), "recorded diagonal wave promotes after cardinal wave resolves");
                reached++;
            }
            if (seq == 95540)
            {
                Check(gust.ActiveAOEs(0, p).Length == 0 && Hints(gust, p).ForbiddenZones.Count == 0, "recorded final gust leaves no stale drawings or AI constraints");
                reached++;
            }
        }
        Check(reached == 3, "all recorded gust checkpoints were reached");
    }
}
