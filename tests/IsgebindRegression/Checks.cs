using System.Numerics;
using BossMod;
using BossMod.Components;
using Isgebind = BossMod.RealmReborn.Dungeon.D10StoneVigil.D103Isgebind;

static class Checks
{
    // Synthetic nominal shape/timer/lifecycle contracts. These do not measure
    // physical telegraphs, floor geometry, server snapshots or live movement.
    static readonly DateTime Now = new(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc);
    static int assertions;
    static bool Legacy => Environment.GetEnvironmentVariable("ISGEBIND_BASELINE") == "1";
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
        ++assertions;
    }

    sealed class Fixture : IDisposable
    {
        public readonly WorldState World = new(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentCFCID = 11 };
        public readonly Actor Boss;
        public readonly Actor Player;
        public readonly Actor Tank;
        public readonly Isgebind.D103Isgebind Module;
        public Fixture(bool castingBeforeActivation = false)
        {
            Boss = Create(1, 0x5AF, new(0, -248));
            Player = Create(100, 0, new(0, -238), ActorType.Player);
            Tank = Create(101, 0, new(0, -243), ActorType.Player);
            World.Execute(new PartyState.OpModify(0, new(0, Player.InstanceID, false)));
            World.Execute(new PartyState.OpModify(1, new(0, Tank.InstanceID, false)));
            World.Execute(new ActorState.OpTarget(Boss.InstanceID, Tank.InstanceID));
            World.Execute(new ActorState.OpCombat(Boss.InstanceID, true));
            if (castingBeforeActivation) Cast(Boss, 1022, .7f);
            Module = new(World, Boss); Module.Update();
        }
        public Actor Create(ulong id, uint oid, WPos pos, ActorType type = ActorType.Enemy)
        {
            World.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, type, Class.None, 40,
                new Vector4(pos.X, 0, pos.Z, 0), .5f, new(100, 100, 0, 0, 0), true, false, 0, 0, 0));
            return World.Actors.Find(id)!;
        }
        public ActorCastInfo Cast(Actor caster, uint action, float remaining, WPos? location = null)
        {
            var pos = location ?? caster.Position;
            World.Execute(new ActorState.OpCastInfo(caster.InstanceID, new()
            {
                Action = new(ActionType.Spell, action), TargetID = caster.InstanceID, Location = new(pos.X, 0, pos.Z),
                Rotation = default, TotalTime = remaining
            }));
            return caster.CastInfo!;
        }
        public void Finish(Actor caster) => World.Execute(new ActorState.OpCastInfo(caster.InstanceID, null));
        public AIHints Hints()
        {
            var hints = new AIHints();
            foreach (var component in Module.Components) component.AddAIHints(0, Player, default, hints);
            return hints;
        }
        public SimpleAOEs AOE(uint action) => Module.Components.OfType<SimpleAOEs>().Single(c => c.WatchedAction == action);
        public GenericAOEs Touchdown => Module.Components.OfType<GenericAOEs>().Single(c => c.WatchedAction == 1027);
        public void Dispose() => Module.Dispose();
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game; Service.Config.Initialize();
        IdleAndCast(); BootstrapAndCleanup(); OtherMechanics();
        Console.WriteLine($"Isgebind synthetic regression: {assertions} checks passed; legacy_baseline={Legacy}.");
    }

    static void IdleAndCast()
    {
        using var f = new Fixture();
        var info = BossModuleRegistry.FindByType(typeof(Isgebind.D103Isgebind));
        Check(info?.PrimaryActorOID == 0x5AF && info.GroupID == 11, "normal Isgebind exact registration");
        Check(f.Module.StateMachine.ActiveState != null, "combat fixture activates normally");
        var idle = f.Hints();
        Check(idle.ForbiddenZones.Count == (Legacy ? 1 : 0), "idle non-tank must have no permanent Frost Breath AI constraint");
        Check(f.Module.CalculateHintsForRaidMember(0, f.Player).Any(h => h.Item2) == Legacy, "idle front position must not carry a timeless cleave warning");
        Check(Legacy ? idle.ForbiddenZones[0].activation == default && idle.ForbiddenZones[0].shapeDistance.Distance(f.Player.Position) < 0 : idle.ForbiddenZones.All(z => z.activation != default), "baseline demonstrates immediate timeless danger; candidate has none");
        f.Cast(f.Boss, 1022, .7f);
        var cast = f.AOE(1022).ActiveAOEs(0, f.Player).ToArray();
        Check(cast.Length == 1, "casted Frost Breath supplies exactly one AOE");
        Check(cast[0].Shape is AOEShapeCone { Radius: 27 } cone && Math.Abs(cone.HalfAngle.Rad - MathF.PI / 3) < 1e-6, "Frost Breath keeps nominal radius27 and half-angle60deg");
        Check(cast[0].Origin == f.Boss.Position && cast[0].Rotation == default, "Frost Breath retains cast origin and facing");
        Check(Math.Abs((cast[0].Activation - Now).TotalSeconds - 1) < 1e-5, "Frost Breath uses corrected future cast deadline");
        var hints = f.Hints();
        Check(hints.ForbiddenZones.Count == 1 && hints.ForbiddenZones[0].activation == cast[0].Activation, "one exact timed AI constraint accompanies the cast");
        var zone = hints.ForbiddenZones[0].shapeDistance;
        Check(zone.Distance(new(0, -238)) < 0 && zone.Distance(new(0, -258)) > 0, "cast cone distinguishes front and rear");
        Check(zone.Distance(new(0, -220.95f)) > 0 && zone.Distance(new(0, -221.05f)) < 0, "cast cone nominal range boundary preserves both sides");
        var a59 = 59f.Degrees().ToDirection() * 10f; var a61 = 61f.Degrees().ToDirection() * 10f;
        Check(zone.Distance(f.Boss.Position + a59) < 0 && zone.Distance(f.Boss.Position + a61) > 0, "cast cone nominal angular boundary preserves both sides");
        Check(f.Module.CalculateHintsForRaidMember(0, f.Player).Any(h => h.Item2), "active cast front receives risk warning");
        f.Finish(f.Boss);
        Check(f.AOE(1022).ActiveAOEs(0, f.Player).Length == 0 && f.Hints().ForbiddenZones.Count == (Legacy ? 1 : 0), "cast finish/cancel retires timed cone without a candidate idle replacement");
        Check(hints.ActionsToExecute.Entries.Count == 0 && idle.ActionsToExecute.Entries.Count == 0, "idle and Frost Breath guidance queue no game actions");
    }

    static void BootstrapAndCleanup()
    {
        using var f = new Fixture(castingBeforeActivation: true);
        Check(f.AOE(1022).ActiveAOEs(0, f.Player).Length == 1 && f.Hints().ForbiddenZones.Count == 1, "activation during Frost Breath bootstraps timed guidance");
        f.Finish(f.Boss);
        Check(f.AOE(1022).Casters.Count == 0 && f.Hints().ForbiddenZones.Count == (Legacy ? 1 : 0), "bootstrapped cast cancellation clears correctly");
        f.Cast(f.Boss, 1022, .7f); f.Module.StateMachine.Reset();
        Check(f.Module.Components.Count == 0 && f.Hints().ForbiddenZones.Count == 0, "native phase reset clears component and AI state");
        f.Module.Update();
        Check(f.AOE(1022).Casters.Count == 1 && f.Hints().ForbiddenZones.Count == 1, "re-entry bootstraps only the current cast");
        f.World.Execute(new ActorState.OpDead(f.Boss.InstanceID, true)); f.Module.Update();
        Check(f.Module.Components.Count == 0 && f.Hints().ForbiddenZones.Count == 0, "native primary death phase clears cone guidance");
        using var destroyed = new Fixture(castingBeforeActivation: true);
        destroyed.World.Execute(new ActorState.OpDestroy(destroyed.Boss.InstanceID)); destroyed.Module.Update();
        Check(destroyed.Module.Components.Count == 0 && destroyed.Hints().ForbiddenZones.Count == 0, "native primary destruction cancels cast and clears phase");
    }

    static void OtherMechanics()
    {
        using var f = new Fixture();
        f.Cast(f.Boss, 1025, 2.7f);
        var rime = f.Hints();
        Check(rime.PredictedDamage.Count == 1 && rime.PredictedDamage[0].Players.Raw == 3, "Rime Wreath retains its full-party damage mask");
        Check(Math.Abs((rime.PredictedDamage[0].Activation - Now).TotalSeconds - 3) < 1e-5 && rime.ForbiddenZones.Count == 0, "Rime Wreath retains timed unavoidable guidance without a cone");
        f.Finish(f.Boss);
        var icePos = new WPos(7, -241); f.Cast(f.Boss, 1023, 2.2f, icePos);
        var ice = f.AOE(1023).ActiveAOEs(0, f.Player).ToArray();
        Check(ice.Length == 1 && ice[0].Shape is AOEShapeCircle { Radius: 5 } && ice[0].Origin == icePos, "Sheet of Ice retains its ground radius5 location");
        Check(f.Hints().ForbiddenZones.Count == 1 && Math.Abs((ice[0].Activation - Now).TotalSeconds - 2.5) < 1e-5, "Sheet of Ice retains one timed AI hazard");
        f.Finish(f.Boss); Check(f.AOE(1023).Casters.Count == 0, "Sheet of Ice cancellation clears its own state");
        var helper = f.Create(2, 0x233C, new(-7, -241)); f.Cast(helper, 1024, 2.7f);
        var ice2 = f.AOE(1024).ActiveAOEs(0, f.Player).ToArray();
        Check(ice2.Length == 1 && ice2[0].Shape is AOEShapeCircle { Radius: 5 } && ice2[0].Origin == helper.Position, "helper ice retains independent radius5 geometry");
        Check(f.Hints().ForbiddenZones.Count == (Legacy ? 2 : 1) && Math.Abs((ice2[0].Activation - Now).TotalSeconds - 3) < 1e-5, "helper ice retains timed AI guidance beside an idle boss");
        f.Finish(helper);
        f.Cast(f.Boss, 1026, 3.7f);
        var cauterize = f.AOE(1026).ActiveAOEs(0, f.Player).ToArray();
        Check(cauterize.Length == 1 && cauterize[0].Shape is AOEShapeRect { LengthFront: 48, HalfWidth: 10 }, "Cauterize retains nominal48x20 rectangle");
        Check(f.Hints().ForbiddenZones.Count == 1 && Math.Abs((cauterize[0].Activation - Now).TotalSeconds - 4) < 1e-5, "Cauterize retains one timed AI constraint");
        f.Finish(f.Boss);
        f.World.Execute(new ActorState.OpPlayActionTimelineEvent(f.Boss.InstanceID, 0x008E));
        var touchdown = f.Touchdown.ActiveAOEs(0, f.Player).ToArray();
        Check(touchdown.Length == 1 && touchdown[0].Shape is AOEShapeCircle { Radius: 5 } && touchdown[0].Origin == Isgebind.D103Isgebind.ArenaCenter.Quantized(), "Touchdown retains radius5 at its existing arena center");
        Check(Math.Abs((touchdown[0].Activation - Now).TotalSeconds - 7.8) < 1e-5 && f.Hints().ForbiddenZones.Count == (Legacy ? 2 : 1), "Touchdown retains its existing timeline delay and AI constraint");
        f.World.Execute(new ActorState.OpCastEvent(f.Boss.InstanceID, new ActorCastEvent(ActionID.MakeSpell(Isgebind.AID.Touchdown), f.Boss.InstanceID, 0, 1, f.Boss.PosRot.AsVector3(), 1, 0, default)));
        Check(f.Touchdown.ActiveAOEs(0, f.Player).Length == 0 && f.Hints().ForbiddenZones.Count == (Legacy ? 1 : 0), "Touchdown resolution retires only its own circle");
        Check(f.Hints().ActionsToExecute.Entries.Count == 0, "adjacent mechanic guidance queues no actions");
    }
}
