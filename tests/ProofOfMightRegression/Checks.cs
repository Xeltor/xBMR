using System.Numerics;
using BossMod;
using BossMod.Components;
using BossMod.RealmReborn.Quest.Job.Lancer.ProofOfMight;

static class Checks
{
    private static int assertions;
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        ++assertions;
    }

    private static Actor Create(WorldState ws, ulong id, uint oid, WPos pos, bool ally = false, bool targetable = true)
    {
        ws.Execute(new ActorState.OpCreate(id, oid, (int)id, 0, "", 0, ActorType.Enemy, Class.None, 29,
            new Vector4(pos.X, 61, pos.Z, 0), oid == (uint)OID.Lindwurm ? 1.4f : 0.5f,
            new(100, 100, 0, 0, 0), targetable, ally, 0, 0, 0));
        return ws.Actors.Find(id)!;
    }

    public static void Run(string gameDirectory)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"));
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        var ws = new WorldState(1, "synthetic") { Frame = new(Now, 0, 0, 0, 0, 1), CurrentZone = 239, CurrentCFCID = 309 };
        var npc = Create(ws, 1, (uint)OID.Thibain, new(-391, -160), true, false);
        var first = Create(ws, 2, (uint)OID.Lindwurm, new(-394, -166));
        var second = Create(ws, 3, (uint)OID.Lindwurm, new(-388, -166));
        var reserve = Create(ws, 4, (uint)OID.Lindwurm, new(-450, -242), targetable: false);
        first.TargetID = npc.InstanceID;
        second.TargetID = 99;
        first.InCombat = second.InCombat = true;
        // Already-running casts must be initialized when the phase activates.
        ActorCastInfo Cast(Actor a) => new() { Action = new(ActionType.Spell, (uint)AID.FrostBreath), Location = a.PosRot.AsVector3(), TotalTime = 2.2f };
        ws.Execute(new ActorState.OpCastInfo(first.InstanceID, Cast(first)));
        ws.Execute(new ActorState.OpCastInfo(second.InstanceID, Cast(second)));
        using var module = new ProofOfMight(ws, npc);
        module.Update();
        Check(BossModuleRegistry.FindByOID((uint)OID.Thibain)?.ModuleType == typeof(ProofOfMight), "generated module registration uses stable ally");
        Check(module.StateMachine.ActiveState != null, "untargetable ally does not prevent pull activation");
        var breath = module.Components.OfType<SimpleAOEs>().Single();
        Check(breath.Casters.Count == 2, "phase activation bootstraps both preexisting casts");
        Check(Math.Abs((breath.Casters[0].Activation - Now).TotalSeconds - 2.5) < 0.001, "NPC cast correction supplies the full warning deadline");
        var shape = breath.Casters[0].Shape;
        Check(shape.Check(first.Position + new WDir(0, 7.35f), first.Position, default), "cone includes inner radial boundary");
        Check(!shape.Check(first.Position + new WDir(0, 7.45f), first.Position, default), "cone excludes outer radial boundary");
        Check(!shape.Check(first.Position + new WDir(0, -4), first.Position, default), "rear remains safe from this cone");
        var hints = new AIHints();
        hints.PotentialTargets.Add(new(first, 0, false));
        hints.PotentialTargets.Add(new(second, 0, false));
        module.CalculateAIHints(0, second, default, hints);
        Check(hints.ForbiddenZones.Count == 2, "AI receives both simultaneous cones");
        Check(hints.PotentialTargets[0].Priority > hints.PotentialTargets[1].Priority, "NPC attacker outranks enemy attacking player");
        hints.PotentialTargets[0].Priority = -1;
        module.CalculateAIHints(0, second, default, hints);
        Check(hints.PotentialTargets[0].Priority == -1, "pending-death target priority is preserved");
        hints.PotentialTargets[1].Priority = AIHints.Enemy.PriorityForbidden;
        module.CalculateAIHints(0, second, default, hints);
        Check(hints.PotentialTargets[1].Priority == AIHints.Enemy.PriorityForbidden, "forbidden target priority is preserved");
        ws.Execute(new ActorState.OpCastInfo(first.InstanceID, null));
        Check(breath.Casters.Count == 1 && breath.Casters[0].ActorID == second.InstanceID, "cancel retires only the matching caster");
        ws.Execute(new ActorState.OpDead(first.InstanceID, true));
        module.Update();
        Check(module.StateMachine.ActiveState != null && breath.Casters.Count == 1, "first lindwurm death keeps remaining mechanics active");
        ws.Execute(new ActorState.OpDead(second.InstanceID, true));
        module.Update();
        Check(module.Components.Count == 0, "all active enemies dead clears phase despite hidden reserve");

        using var retry = new ProofOfMight(ws, npc);
        first.IsDead = second.IsDead = false;
        first.CastInfo = second.CastInfo = null;
        retry.Update();
        Check(retry.Components.OfType<SimpleAOEs>().Single().Casters.Count == 0, "new attempt has no stale cones");
        npc.IsDead = true;
        retry.Update();
        Check(retry.Components.Count == 0, "protected NPC death clears components on failure");
        Console.WriteLine($"Proof of Might synthetic regression: {assertions} checks passed.");
    }
}
