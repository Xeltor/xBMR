using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.Heavensward.Alliance.A35Diabolos;

static partial class Checks
{
    private static void OmenVisualTiming()
    {
        OmenVariant(false);
        OmenVariant(true);
    }

    private static void OmenVariant(bool hollow)
    {
        var name = hollow ? "Hollow Omen" : "Ruinous Omen";
        var visualAction = hollow ? 7202u : 7184u;
        var damageAction = hollow ? 7203u : 7185u;
        var offset = hollow ? 5 : 1;
        var world = World();
        var primary = Actor(1, hollow ? 0x1908u : 0x1907u, new(-350, -445));
        var helper = Actor(2, 0x19A, primary.Position);
        world.Actors.Actors[primary.InstanceID] = primary;
        world.Actors.Actors[helper.InstanceID] = helper;
        var player = Actor(20, 0, primary.Position, Class.SGE);
        foreach (var slot in new[] { 0, 8, 16 })
            Member(world, slot == 0 ? player : Actor((ulong)(20 + slot), 0, primary.Position, Class.BRD), slot);
        using BossModule module = hollow ? new BossMod.Heavensward.Alliance.A36DiabolosHollow.A36DiabolosHollow(world, primary) : new A35Diabolos(world, primary);
        module.StateMachine.Start(Now);
        var visual = Component<CastHint>(module, hollow ? "HollowOmen1" : "RuinousOmen1");
        var damage = Component<CastHint>(module, hollow ? "HollowOmen2" : "RuinousOmen2");
        ActorCastInfo Cast(uint aid, float remaining) => new()
        {
            Action = new(ActionType.Spell, aid), TotalTime = remaining,
            Location = new Vector3(primary.Position.X, 1, primary.Position.Z)
        };
        AIHints Forecast()
        {
            var hints = Hints(module, visual, 0, player);
            damage.AddAIHints(0, player, default, hints);
            return hints;
        }
        void StartBoth()
        {
            world.Execute(new ActorState.OpCastInfo(primary.InstanceID, Cast(visualAction, 14.7f)));
            world.Execute(new ActorState.OpCastInfo(helper.InstanceID, Cast(damageAction, 14.7f + offset)));
        }
        StartBoth();
        var hints = Forecast();
        Check(visual.Active && damage.Active && hints.PredictedDamage.Count == 1,
            name + ": simultaneous boss/helper casts expose one real raidwide instead of an extra visual-finish hit");
        Check(hints.PredictedDamage[0].Activation == module.CastFinishAt(helper.CastInfo)
            && Math.Abs((hints.PredictedDamage[0].Activation - module.CastFinishAt(primary.CastInfo)).TotalSeconds - offset) < 0.000001,
            name + ": forecast uses the helper's corrected deadline after the boss visual");
        Check(hints.PredictedDamage[0].Players[0] && hints.PredictedDamage[0].Players[8] && hints.PredictedDamage[0].Players[16],
            name + ": actual deadline covers all three alliances");
        var global = new BossComponent.GlobalHints();
        visual.AddGlobalHints(player, global);
        Check(global.Any(h => h.Contains(name)), "boss visual still provides its early cast warning");
        world.Frame = new(Now.AddSeconds(15), 0, 0, 0, 0, 1);
        world.Execute(new ActorState.OpCastEvent(primary.InstanceID, Hit(visualAction)));
        world.Execute(new ActorState.OpCastInfo(primary.InstanceID, null));
        Check(!visual.Active && damage.Active && Forecast().PredictedDamage.Count == 1,
            "native visual completion retains the independent helper prediction until its later damage");
        world.Frame = new(Now.AddSeconds(15.03 + offset), 0, 0, 0, 0, 1);
        world.Execute(new ActorState.OpCastEvent(helper.InstanceID, Hit(damageAction)));
        world.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(Forecast().PredictedDamage.Count == 0 && !damage.Active, "actual helper damage and native cast-end retire the sole prediction");

        StartBoth();
        world.Execute(new ActorState.OpCastInfo(primary.InstanceID, null));
        Check(!visual.Active && damage.Active && Forecast().PredictedDamage.Count == 1,
            "cancelled boss visual cannot erase an independently active helper cast");
        world.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(Forecast().PredictedDamage.Count == 0, "cancelled helper cast removes its forecast without requiring damage");
        StartBoth();
        world.Execute(new ActorState.OpCastInfo(helper.InstanceID, null));
        Check(visual.Active && Forecast().PredictedDamage.Count == 0, "visual warning alone cannot invent damage after helper cancellation");
        world.Execute(new ActorState.OpCastInfo(primary.InstanceID, null));
        global.Clear();
        visual.AddGlobalHints(player, global);
        Check(global.Count == 0, "cancelled visual retires its own cast warning");
    }
}
