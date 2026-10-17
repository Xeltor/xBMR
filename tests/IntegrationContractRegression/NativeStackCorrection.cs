using BossMod;
using BossMod.Components;
using BossMod.Integration;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

static partial class Checks
{
    private sealed class CustomStackPolicy(BossModule module) : GenericStackSpread(module)
    {
        public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
            => base.AddAIHints(slot, actor, assignment, hints);
    }
    private static void NativeStackCorrection()
    {
        var (world, actor, module) = ProjectionFixture();
        var target = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor)); target.InstanceID = 2; target.PosRot = new(5, 0, 0, 0);
        world.Party.Members[1] = new(2, 2, false);
        ((Actor?[])typeof(PartyState).GetField("_actors", InternalInstance)!.GetValue(world.Party)!)[1] = target;
        var component = new PublishedStacks(module);
        component.Stacks.Add(new(actor, 1, 2, 4, world.FutureTime(3)));
        component.Stacks.Add(new(target, 1, 2, 4, world.FutureTime(3.5)));
        module.Components.Add(component);
        var hints = new AIHints();
        var enabled = typeof(AIHints).GetField("StackCarrierSeparationActive", InternalInstance)!;
        component.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Any(z => z.shapeDistance is SDInvertedCircle), "uncorrected native base case reproduces erroneous join-other-carrier behavior");
        hints.Clear(); enabled.SetValue(hints, true); component.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].shapeDistance is SDCircle
            && hints.ForbiddenZones[0].shapeDistance.Distance(target.Position) < 0 && hints.ForbiddenZones[0].shapeDistance.Distance(actor.Position) > 0,
            "opted-in native base handler separates concurrent carriers using its own double-radius constraint");
        Check(component.Stacks.All(s => s.ForbiddenPlayers.Raw == 0), "native carrier correction never borrows or edits source stack masks");
        var context = new IntegrationSession.Context(1, "fixture", 1, "hints-ready");
        using (var json = JsonDocument.Parse(IntegrationSession.Serialize(IntegrationMechanics.Capture(module, actor, context,
            new(clock: () => 0, frequency: 1000), nativeCarrierSeparation: true))))
        {
            var other = json.RootElement.GetProperty("entries")[1];
            Check(other.GetProperty("localCarrierSeparation").GetBoolean() && !other.GetProperty("participation").GetProperty("localEligible").GetBoolean()
                && other.GetProperty("excludedActorIds").GetArrayLength() == 0,
                "native participation exposes effective local separation while retaining original mask provenance");
        }
        var custom = new CustomStackPolicy(module); custom.Stacks.AddRange(component.Stacks);
        hints.Clear(); enabled.SetValue(hints, true); custom.AddAIHints(0, actor, default, hints);
        Check(hints.ForbiddenZones.Any(z => z.shapeDistance is SDInvertedCircle), "custom AddAIHints retains ownership even when it calls the base implementation");
        var later = component.Stacks[1]; later.Activation = world.FutureTime(5); component.Stacks[1] = later;
        Check(!IntegrationStackCorrection.Required(component, actor, later), "separate future stacks are never combined into a concurrent carrier assignment");
        var policy = new IntegrationStackCorrection(); var firstLive = true; var secondLive = true;
        Check(policy.Set("first", true, () => firstLive) && policy.Set("second", true, () => secondLive) && policy.Enabled,
            "two owners can independently opt into native carrier correction");
        policy.Set("first", false, null); Check(policy.Enabled, "one owner opt-out cannot remove another owner's correction lease");
        secondLive = false; Check(!policy.Enabled, "owner/source retirement disables the next native build without editing saved settings");
        firstLive = false; Check(!policy.Set("first", true, () => firstLive), "expired native policy cannot be renewed from a stale source");
        var assembly = typeof(AIHints).Assembly;
        Type Type(string name) => assembly.GetType("BossMod." + name, true)!;
        object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), BindingFlags.Public | InternalInstance, null, args, null)!;
        object Blank(string name) => RuntimeHelpers.GetUninitializedObject(Type(name));
        void Set(object o, string name, object? value) => o.GetType().GetField(name, BindingFlags.Public | InternalInstance)!.SetValue(o, value);
        object? Call(object o, string name, params object?[] args) => o.GetType().GetMethod(name, BindingFlags.Public | InternalInstance)!.Invoke(o, args);
        long clock = 1;
        using var session = (IDisposable)New("Integration.IntegrationSession", (Func<long>)(() => clock), 1000L);
        var instance = (string)session.GetType().GetField("InstanceGeneration", InternalInstance)!.GetValue(session)!;
        var owner = (string)Property(Call(session, "Register", New("Integration.IntegrationSession+Registration", 1, instance, "stack-policy", 2000), (Action<string>)(_ => { }))!, "OwnerToken");
        Call(session, "BeginFrame", "native", true); Call(session, "Publish", "hints-ready", (Func<object, string>)(c => IntegrationSession.Serialize(c)));
        var manager = Blank("BossModuleManager"); Set(manager, nameof(BossModuleManager.WorldState), world); Set(manager, "_activeModule", module);
        var coordinator = Blank("Integration.IntegrationCoordinator"); Set(coordinator, "Session", session);
        var nativePolicy = New("Integration.IntegrationStackCorrection"); Set(coordinator, "_stackCorrection", nativePolicy);
        Set(coordinator, "<bossmod>P", manager); Set(coordinator, "<hints>P", new AIHints());
        var movement = (MovementOverride)Blank("MovementOverride"); Set(coordinator, "<movement>P", movement);
        Set(coordinator, "<rotation>P", Blank("Autorotation.RotationModuleManager"));
        string Request(bool value) => IntegrationSession.Serialize(new
        { context = session.GetType().GetProperty("CurrentContext", InternalInstance)!.GetValue(session), ownerToken = owner, enabled = value });
        using (var receipt = JsonDocument.Parse((string)Call(coordinator, "SetStackCarrierSeparation", Request(true))!))
            Check(receipt.RootElement.GetProperty("accepted").GetBoolean() && receipt.RootElement.GetProperty("nextBuildEnabled").GetBoolean()
                && !receipt.RootElement.GetProperty("currentBuildEnabled").GetBoolean(), "actual native policy endpoint distinguishes requested next-build behavior from current built hints");
        bool NativeEnabled() => (bool)nativePolicy.GetType().GetProperty("Enabled", InternalInstance)!.GetValue(nativePolicy)!;
        movement.UserMove = new(1, 0); Check(!NativeEnabled(), "manual control prevents future corrected native hint builds"); movement.UserMove = default;
        Call(session, "Clear", New("Integration.IntegrationSession+Lease", 1, instance, owner, 0)); Check(!NativeEnabled(), "native owner clear invalidates the correction policy");
        using (var receipt = JsonDocument.Parse((string)Call(coordinator, "SetStackCarrierSeparation", Request(true))!)) { }
        clock += 251; Check(!NativeEnabled(), "native policy uses original frame expiry rather than registration lease age");
        var enemy = new AIHints.Enemy(actor, 0, true);
        Check(enemy.DesiredRotation == null, "actual native target defaults leave facing unset rather than automatically copying actor rotation");
        enemy.DesiredRotation = actor.Rotation;
        Check(enemy.DesiredRotation == actor.Rotation, "explicit authored equal-facing requests retain their native value");
    }
}
