using BossMod;
using BossMod.CombatAssistance.Knockback;
using BossMod.CombatAssistance.Planning;
using BossMod.Components;
using System.Numerics;

public static partial class Checks
{
    private static void NativeKnockbackObservation()
    {
        var start = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);
        var world = (WorldState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldState));
        var actors = new ActorState();
        typeof(WorldState).GetField(nameof(WorldState.Actors))!.SetValue(world, actors);
        typeof(WorldState).GetField(nameof(WorldState.Party))!.SetValue(world, new PartyState(actors));
        world.CurrentZone = 339; world.Frame.Timestamp = start;
        Actor MakeActor(ulong id, uint oid, ActorType type, Vector4 position)
        {
            var actor = (Actor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = id; actor.OID = oid; actor.Type = type; actor.PosRot = position; actor.PrevPosRot = position;
            actor.HPMP = new(100, 100, 0, 100, 100); actor.Statuses = new ActorStatus[Actor.NumStatuses];
            actor.PendingKnockbacks = [];
            actors.Actors.Add(id, actor);
            return actor;
        }
        var player = MakeActor(1, 0, ActorType.Player, new(3, 0, 0, 0));
        var source = MakeActor(10, 222, ActorType.Enemy, default);
        world.Party.Members[0] = new(1, 1, false);
        ((Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!)[0] = player;
        var module = new object();
        var manual = false;
        var rowReads = 0;
        using var observer = new NativeKnockbackObserver(world, () => manual, row =>
        { ++rowReads; return row == 7 ? new KnockbackEffectDefinition(6, 10, 0) : null; });
        void Tick(float seconds)
        { world.Frame.Timestamp = start.AddSeconds(seconds); observer.Update(module, true); }
        AuthoredKnockbackSource Published(float activation) => new(0, 0, "native fixture", default, 6, start.AddSeconds(activation),
            GenericKnockback.Kind.AwayFromOrigin, default, 0, 10, false, false, true, int.MaxValue, true, null, false, false, null, null, [])
            { CastActionId = 100, SourceDataId = 222 };
        void Track(AuthoredKnockbackSource publication) => observer.Track(new(world.CurrentTime, player.InstanceID, 339, 0,
            [publication], 1, 0, 0, true, false, false, false));
        ActorCastEvent Packet(uint sequence)
        {
            var effects = new ActionEffects();
            effects.AsSpan()[0] = new() { Type = ActionEffectType.Knockback, Value = 7 };
            var packet = new ActorCastEvent(new ActionID(ActionType.Spell, 101), 1, 0, 1, player.PosRot.XYZ(), sequence, sequence, default);
            packet.Targets.Add(new(1, effects));
            return packet;
        }
        void Push(float at, uint sequence)
        {
            var packet = Packet(sequence);
            actors.CastEvent.Fire(source, packet); actors.CastEvent.Fire(source, packet);
            for (var sample = 1; sample <= 12; ++sample)
            {
                player.PosRot.X = 3 + sample * .5f;
                Tick(at + sample * .05f);
            }
            Tick(at + .67f); Tick(at + .82f);
        }
        Tick(0);
        actors.CastEvent.Fire(source, Packet(1));
        Check(rowReads == 0 && observer.Status.TimingRecords == 0 && observer.Completion == null,
            "An unknown knockback does not read effect geometry, learn a record or publish native completion.");
        source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = .5f, Location = player.PosRot.XYZ() };
        var first = Published(.8f);
        Track(first);
        Check(!observer.TryBind(first, out _, out _, out _, out _),
            "A native publication alone cannot invent a physical duration; untimed handling retains native recovery.");
        source.CastInfo.ElapsedTime = .7f; Tick(.7f); Track(first);
        Tick(.8f); source.CastInfo = null; Push(.8f, 2);
        Check(observer.Status.TimingRecords == 1 && observer.Completion is { Actor: 1, SourceActor: 10, CastActionId: 100, EffectCounter: 2 } receipt
            && receipt.Launch == new Vector2(3, 0) && receipt.Landing == new Vector2(9, 0),
            "Only an authored publication, matched native effect and stable physical motion create a scoped completion; duplicates add no record.");
        var completed = observer.Completion;
        Tick(1.7f);
        Check(ReferenceEquals(completed, observer.Completion), "Current completion polling keeps the original observation clock.");
        Tick(1.9f);
        Check(observer.Completion == null, "Physical completion expires at the native freshness ceiling.");
        player.PosRot.X = 3;
        source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = .5f, Location = player.PosRot.XYZ() };
        var second = Published(3.8f);
        Tick(3); Track(second);
        Check(observer.TryBind(second, out var timing, out var generation, out var revision, out _)
            && generation != 0 && revision > 0 && timing is { } window && window.LandingLatestUtc > window.LatestUtc,
            "A known source can use its session native-effect timing without an unknown-geometry classifier or three prior casts.");
        Check(!observer.TryBind(second with { Distance = 12 }, out _, out _, out _, out _),
            "Native timing cannot substitute a changed authored displacement geometry.");
        source.CastInfo.ElapsedTime = .1f; Tick(3.1f); Track(second);
        Check(observer.TryBind(second, out var refreshed, out var refreshedGeneration, out _, out _)
            && refreshed == timing && refreshedGeneration == generation,
            "Repeated native publications retain cast generation and original absolute deadlines.");
        manual = true; Tick(3.2f);
        actors.CastEvent.Fire(source, Packet(3));
        Check(observer.Completion == null && !observer.Status.Pending, "Manual input prevents physical-completion ownership.");
        manual = false; module = new object(); Tick(3.3f);
        Check(observer.Status.TimingRecords == 0 && observer.Status.Sources == 0,
            "A module transition discards native session observations; there is no persisted learning or legacy import.");
        player.PosRot.X = 3;
        source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = .7f, Location = player.PosRot.XYZ() };
        Tick(4); Track(Published(5));
        source.CastInfo.ElapsedTime = .9f; Tick(4.9f); Track(Published(5));
        Tick(5); source.CastInfo = null; actors.CastEvent.Fire(source, Packet(4));
        player.PosRot.X = 3.5f; Tick(5.05f); Tick(5.1f); Tick(5.24f);
        Check(observer.Completion == null && observer.Status.Pending,
            "A short network pause partway through a native push cannot complete motion before the native distance/speed window.");
        module = new object(); player.PosRot.X = 3;
        source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = .7f };
        Tick(6); Track(Published(7));
        source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = .8f };
        Tick(6.1f); Track(Published(7.2f));
        var readsBeforeOverlap = rowReads;
        Tick(7.2f); source.CastInfo = null; actors.CastEvent.Fire(source, Packet(5));
        Check(observer.Completion == null && !observer.Status.Pending && observer.Status.TimingRecords == 0 && rowReads == readsBeforeOverlap,
            "Overlapping unmatched native cast generations cannot attribute an effect or create a timing record for the wrong cast.");
        module = new object(); source.CastInfo = new() { Action = new ActionID(ActionType.Spell, 100), TotalTime = 1 };
        Tick(8); Track(Published(8.8f));
        Check(observer.Status.Sources == 0,
            "An unrelated cast with a different native finish cannot bind to an older authored displacement publication.");
        observer.Dispose(); actors.CastEvent.Fire(source, Packet(4));
        Check(!observer.Status.Active && observer.Completion == null, "Unload removes the native effect subscription and observation ownership.");
    }
}
