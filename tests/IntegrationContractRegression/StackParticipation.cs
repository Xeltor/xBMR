using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class PublishedLineStack(BossModule module) : GenericBaitStack(module);

    private static void StackParticipationPublications()
    {
        var (world, player, module) = ProjectionFixture();
        player.PosRot = new(2.9f, 0, 0, 0);
        var actors = (Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!;
        Actor Add(int slot, float x, float y = 0, bool dead = false)
        {
            var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = (ulong)slot + 1; actor.PosRot = new(x, y, 0, 0); actor.IsDead = dead;
            world.Party.Members[slot] = new(actor.InstanceID, actor.InstanceID, false); actors[slot] = actor; return actor;
        }
        var target = Add(1, 2.011f); var forbidden = Add(2, 1.2f); var dead = Add(3, 2, dead: true); var upstairs = Add(4, 2, 10);
        var floors = new ArenaBoundsRect(8, 8).Shape;
        typeof(BossModule).GetField(nameof(BossModule.Arena))!.SetValue(module,
            new MiniArena(default, new ArenaBoundsCustom([new Circle(default, 8)], WorldProjectionLayers: [new(floors, 0), new(floors, 10)])));
        var stack = new PublishedStacks(module);
        var native = new GenericStackSpread.Stack(target, 1, 2, 2, world.FutureTime(3), new(4));
        stack.Stacks.Add(native); module.Components.Add(stack);
        var context = new IntegrationSession.Context(1, "fixture", 1, "hints-ready");
        JsonDocument Capture(int work = 8192) => JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, player,
            context, new(clock: () => 0, frequency: 1000, evaluations: work))));
        using (var snapshot = Capture())
        {
            Check(snapshot.RootElement.GetProperty("entries")[0].GetProperty("standardAIHints").GetBoolean()
                && snapshot.RootElement.GetProperty("entries")[0].GetProperty("extraAISpreadThreshold").GetSingle() == stack.ExtraAISpreadThreshold,
                "unchanged native stack AI implementation and extra spread threshold are explicit publication facts");
            var root = snapshot.RootElement; var facts = root.GetProperty("entries")[0].GetProperty("participation");
            Check(root.GetProperty("party").GetArrayLength() == 5 && root.GetProperty("party")[3].GetProperty("isDead").GetBoolean(),
                "stack publication includes the same native party including dead actors, without another party read");
            Check(root.GetProperty("worldTimeTicks").GetString() == world.CurrentTime.Ticks.ToString(), "mechanic timestamps have their exact native world clock in the same publication");
            Check(facts.GetProperty("complete").GetBoolean() && facts.GetProperty("eligibleAliveActorIds").GetArrayLength() == 2,
                "native stack eligibility excludes dead, forbidden and other-floor participants");
            Check(facts.GetProperty("occupantCount").GetInt32() == native.NumInside(module) && facts.GetProperty("occupantCount").GetInt32() == 2,
                "occupancy matches actual native quantized circle and full same-floor party, using the quantized native target");
            Check(facts.GetProperty("localInside").GetBoolean() && facts.GetProperty("localEligible").GetBoolean()
                && facts.GetProperty("maxSize").GetInt32() == 2, "native capacity and current local membership travel together");
        }
        native.RestrictToArenaProjectionLayer = null; stack.Stacks[0] = native;
        using (var allFloors = Capture()) Check(allFloors.RootElement.GetProperty("entries")[0].GetProperty("participation").GetProperty("occupantCount").GetInt32() == 3,
            "explicit all-floor participation preserves the native layer opt-out");
        native.RestrictToArenaProjectionLayer = true; stack.Stacks[0] = native; target.IsDead = true;
        using (var includedDeadTarget = Capture()) Check(includedDeadTarget.RootElement.GetProperty("entries")[0].GetProperty("participation").GetProperty("activeTarget").GetBoolean(),
            "native IncludeDeadTargets remains distinct from living participant eligibility");
        target.IsDead = false; module.Components.Clear();
        var line = new PublishedLineStack(module) { ForbiddenPlayers = new(1), EnableHints = false };
        var source = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
        var bait = new GenericBaitAway.Bait(source, target, new AOEShapeRect(10, 1), world.FutureTime(2),
            forbidden: new(4), customRotation: 1f.Radians(), offset: new(0, 1));
        line.CurrentBaits.Add(bait); module.Components.Add(line);
        using (var snapshot = Capture())
        {
            var entry = snapshot.RootElement.GetProperty("entries")[0]; var facts = entry.GetProperty("stackParticipation");
            Check(entry.GetProperty("nativeBaitStack").GetBoolean(), "line stack intent comes from the native component family rather than shape guessing");
            Check(facts.GetProperty("minSize").ValueKind == JsonValueKind.Null && facts.GetProperty("maxSize").ValueKind == JsonValueKind.Null,
                "native bait stack never acquires invented size limits");
            Check(facts.GetProperty("localEligible").GetBoolean() && entry.GetProperty("forbiddenBaiterActorIds")[0].GetString() == "1",
                "forbidden baiters are kept distinct from actual line-stack participation exclusions");
            Check(facts.GetProperty("constraintOrigin")[1].GetSingle() == 1
                && Math.Abs(facts.GetProperty("constraintRotation").GetSingle() - Angle.FromDirection(target.Position - new WPos(0, 1)).Rad) < 0.00001f
                && entry.GetProperty("rotation").GetSingle() == 1,
                "line stack uses the actual native offset origin/AI aim independently of custom display rotation");
        }
        module.Components.Clear(); var away = new GenericBaitAway(module); away.CurrentBaits.Add(bait); module.Components.Add(away);
        using (var regular = Capture()) Check(!regular.RootElement.GetProperty("entries")[0].GetProperty("nativeBaitStack").GetBoolean()
            && regular.RootElement.GetProperty("entries")[0].GetProperty("stackParticipation").ValueKind == JsonValueKind.Null,
            "an ordinary rectangular bait never becomes a stack by resemblance");
        using (var limited = Capture(1)) Check(!limited.RootElement.GetProperty("publishedEntriesComplete").GetBoolean(),
            "bounded publication cannot claim complete stack eligibility after budget exhaustion");
    }
}
