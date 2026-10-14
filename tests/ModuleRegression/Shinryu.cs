using System.Numerics;
using System.Reflection;
using BossMod;
using BossMod.Components;

static partial class Regression
{
    private const string ShinryuPrefix = "Dawntrail.Alliance.A35ShinryuParadox.";
    private static readonly WPos ShinryuPad = new(820, -826);
    private static (BossModule Module, GenericAOEs Component, Actor Player) Shinryu()
    {
        var module = Module(ShinryuPrefix + "A35ShinryuParadox", new(820, -820));
        Set(module, "_actorArenaProjectionLayers", new Dictionary<ulong, int>());
        var arena = ((WPos, ArenaBoundsCustom))module.GetType().GetMethod("BuildArena", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        module.Arena.Bounds = arena.Item2;
        var player = Actor(1, 0, ShinryuPad, true);
        module.WorldState.Party.Members[0] = new(0, player.InstanceID, false);
        var actors = new Actor?[PartyState.MaxAllies];
        actors[0] = player;
        Set(module.WorldState.Party, "_actors", actors);
        return (module, Component<GenericAOEs>(module, ShinryuPrefix + "FloorAOEs"), player);
    }

    private static void ShinryuDirections()
    {
        foreach (var light in new[] { false, true })
        {
            var (_, component, player) = Shinryu();
            var status = new ActorStatus(light ? 5352u : 5353u, 0, Now.AddSeconds(20), 0);
            component.OnStatusGain(player, ref status);
            component.OnCastStarted(player, Cast(49113, ShinryuPad));
            foreach (var upper in new[] { false, true })
            {
                player.PosRot = new Vector4(820, upper ? -879 : -900, -826, 0);
                var aoes = component.ActiveAOEs(0, player);
                var aoe = aoes.ToArray().Single(a => a.ArenaProjectionLayer == (upper ? 1 : 0));
                var shouldStay = light == upper;
                // Probe the authored proxy centre and an interior point outside its radius.
                // These are predicate checks, not measurements of a physical transition.
                Check((aoe.ShapeDistance!.Distance(ShinryuPad) <= 0) == shouldStay,
                    $"{(light ? "light" : "dark")} {(upper ? "upper" : "lower")}: avoid transition only on assigned floor");
                var outside = new WPos(830, -826);
                Check((aoe.ShapeDistance.Distance(outside) <= 0) != shouldStay,
                    "wrong floor selects the existing transition proxy");
                Check(aoe.Color == (shouldStay ? Colors.SafeFromAOE : 0), "render colour agrees with the assigned floor");
                var text = new BossComponent.TextHints();
                component.AddHints(0, player, text);
                Check(text.Count == 1 && text[0].Item1 == (light ? "Be on top floor!" : "Be on bottom floor!")
                    && text[0].Item2 == !shouldStay, "text hint reports the same floor and current risk");
                var hints = new AIHints();
                component.AddAIHints(0, player, default, hints);
                Check(hints.ForbiddenZones.Count == 1, "AI receives only the actor's authored physical layer");
                Check((hints.ForbiddenZones[0].shapeDistance.Distance(ShinryuPad) <= 0) == shouldStay,
                    "native AI predicate agrees with the floor assignment");
            }
        }
    }

    private static void ShinryuResolution()
    {
        foreach (var visualFirst in new[] { false, true })
        foreach (var radianceFirst in new[] { false, true })
        {
            var (module, component, player) = Shinryu();
            var status = new ActorStatus(5352, 0, Now.AddSeconds(20), 0);
            component.OnStatusGain(player, ref status);
            var cast = Cast(49113, ShinryuPad);
            component.OnCastStarted(player, cast);
            if (visualFirst) component.OnCastFinished(player, cast);
            Check(component.ActiveAOEs(0, player).Length == 2 && component.NumCasts == 0,
                "visual finish cannot retire Twilight before its helpers");
            var first = radianceFirst ? 49115u : 49116u;
            var last = radianceFirst ? 49116u : 49115u;
            component.OnEventCast(player, Effect(first));
            component.OnEventCast(player, Effect(first));
            component.OnCastStarted(player, cast); // repeated callback must not discard the first hit
            Check(component.ActiveAOEs(0, player).Length == 2 && component.NumCasts == 0,
                "first/duplicate helper and repeated cast callback retain the pending counterpart");
            component.OnEventCast(player, Effect(last));
            Check(component.ActiveAOEs(0, player).Length == 0 && component.NumCasts == 1,
                "both distinct helpers retire Twilight exactly once");
            component.OnEventCast(player, Effect(last));
            component.OnCastFinished(player, cast);
            Check(component.NumCasts == 1, "duplicate last helper and late visual cannot double count");
            // Native phase/component retirement discards a partially resolved instance.
            component.OnStatusGain(player, ref status);
            component.OnCastStarted(player, Cast(49113, ShinryuPad));
            component.OnEventCast(player, Effect(first));
            typeof(BossModule).GetMethod("DeactivateComponent")!.MakeGenericMethod(component.GetType()).Invoke(module, null);
            var fresh = Component<GenericAOEs>(module, ShinryuPrefix + "FloorAOEs");
            fresh.OnEventCast(player, Effect(last));
            Check(fresh.NumCasts == 0 && fresh.ActiveAOEs(0, player).Length == 0, "reactivation starts without previous helper state");
        }
    }

    private static void ShinryuAssignments()
    {
        var (_, component, player) = Shinryu();
        var light = new ActorStatus(5352, 0, Now.AddSeconds(20), 0);
        var dark = new ActorStatus(5353, 0, Now.AddSeconds(20), 0);
        component.OnStatusGain(player, ref light);
        component.OnCastStarted(player, Cast(49107, ShinryuPad));
        Check(component.ActiveAOEs(0, player)[1].Color == Colors.SafeFromAOE,
            "an early Twilight assignment cannot override preceding Breath's lower floor");
        component.OnCastStarted(player, Cast(49113, ShinryuPad));
        component.OnStatusGain(player, ref light);
        Check(component.ActiveAOEs(0, player)[0].Color == Colors.SafeFromAOE, "status refresh preserves light's upper assignment");
        component.OnStatusGain(player, ref dark);
        Check(component.ActiveAOEs(0, player).Length == 0, "conflicting assignments do not invent a floor");
        component.OnStatusLose(player, ref light);
        Check(component.ActiveAOEs(0, player)[1].Color == Colors.SafeFromAOE, "replacement retains only the dark assignment");
        component.OnStatusLose(player, ref dark);
        Check(component.ActiveAOEs(0, player).Length == 0, "lost assignment does not persist in the bit mask");
        component.OnStatusGain(Actor(2, 0, ShinryuPad), ref light);
        Check(component.ActiveAOEs(63, player).Length == 0, "unknown actor cannot set slot 63 through a negative lookup");
        component.OnStatusGain(player, ref light);
        component.OnEventCast(player, Effect(49115));
        component.OnCastStarted(player, Cast(49110, ShinryuPad));
        component.OnEventCast(player, Effect(49116));
        Check(component.NumCasts == 0 && component.ActiveAOEs(0, player).Length == 2,
            "a new recognized mechanic replaces an incomplete Twilight without false completion");
        component.OnCastFinished(player, Cast(49113, ShinryuPad));
        Check(component.ActiveAOEs(0, player).Length == 2, "late Twilight visual cannot clear a newer Tail");
        component.OnCastFinished(player, Cast(49110, ShinryuPad));
        Check(component.ActiveAOEs(0, player).Length == 0, "Tail still retires at its own cast finish");
    }

    private static void ShinryuBootstrap()
    {
        var (module, component, player) = Shinryu();
        typeof(BossModule).GetMethod("DeactivateComponent")!.MakeGenericMethod(component.GetType()).Invoke(module, null);
        player.Statuses[0] = new ActorStatus(5352, 0, Now.AddSeconds(20), 0);
        var boss = Actor(2, 19858, new(820, -845));
        boss.CastInfo = Cast(49113, boss.Position);
        module.WorldState.Actors.Actors.Add(player.InstanceID, player);
        module.WorldState.Actors.Actors.Add(boss.InstanceID, boss);
        component = Component<GenericAOEs>(module, ShinryuPrefix + "FloorAOEs");
        Check(component.ActiveAOEs(0, player).Length == 2 && component.ActiveAOEs(0, player)[0].Color == Colors.SafeFromAOE,
            "generated activation bootstraps existing assignment and Twilight cast");
        component.OnEventCast(boss, Effect(49115));
        component.OnEventCast(boss, Effect(49116));
        Check(component.NumCasts == 1 && component.ActiveAOEs(0, player).Length == 0,
            "bootstrapped cast resolves through the same helper lifecycle");
        foreach (var id in new[] { 49107u, 49110u, 49130u })
        {
            var cast = Cast(id, boss.Position);
            component.OnCastStarted(boss, cast);
            var aoes = component.ActiveAOEs(0, player);
            Check(aoes.Length == 2 && aoes.ToArray().Single(a => a.Color == Colors.SafeFromAOE).ArenaProjectionLayer == (id == 49107 ? 0 : 1),
                "Breath and both Tail variants retain their own floor independently of colour");
            component.OnCastFinished(boss, cast);
            Check(component.ActiveAOEs(0, player).Length == 0, "each swipe still retires on its own finish");
        }
    }
}
