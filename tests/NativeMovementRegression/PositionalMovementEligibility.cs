using BossMod;
using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Planning;
using System.Runtime.CompilerServices;

public static partial class Checks
{
    private static void PositionalMovementEligibility()
    {
        Actor Make(ulong id, ActorType type, bool ally = false)
        {
            // The real constructor queries game sheets; supply only consumed fields.
            var actor = (Actor)RuntimeHelpers.GetUninitializedObject(typeof(Actor));
            actor.InstanceID = id;
            actor.Type = type;
            actor.IsAlly = ally;
            actor.HitboxRadius = 1;
            return actor;
        }
        var player = Make(1, ActorType.Player);
        var enemy = Make(2, ActorType.Enemy);
        var tank = Make(3, ActorType.Player);
        var npc = Make(4, ActorType.Enemy, true);
        var actors = new ActorState();
        foreach (var actor in new[] { player, enemy, tank, npc }) actors.Actors.Add(actor.InstanceID, actor);
        var front = new WPos(0, 2);
        var rear = new WPos(0, -2);
        var flank = new WPos(2, 0);
        var hints = new AIHints();
        var evidence = new RsrActionEvidence(10, enemy.InstanceID, default, [], false, false, 1, 3, 0, 0, 0, 2.5f, 0, 0, 0.6f);
        ExecutionActor Value(Actor actor) => new(actor.InstanceID, actor.Position, 0, actor.HitboxRadius, actor.Rotation);
        foreach (var desired in new[] { Positional.Rear, Positional.Flank })
        {
            void Verify(bool allowed, string situation)
            {
                var positional = PositionalMovement.ForTarget(desired, enemy, player, actors);
                Check(positional == (allowed ? desired : Positional.Any), $"{desired}: {situation}.");
                var goal = hints.GoalSingleTarget(enemy, desired, player, actors);
                var valid = desired == Positional.Rear ? rear : flank;
                Check(allowed ? goal(valid) > goal(front) : goal(valid) == goal(front),
                    $"Native rotation goals {(allowed ? "retain" : "remove")} angular movement for {situation}.");
                var action = RsrExecutionCapture.Create(evidence, Value(player), [Value(enemy)], [], 2, positional, null, out _);
                Check(action?.Opportunity is { } opportunity && opportunity.TargetId == enemy.InstanceID
                    && opportunity.Evaluate(valid).Allowed && opportunity.Evaluate(front).Allowed == !allowed,
                    $"RSR movement retains attack range and recipient while gating {desired} for {situation}.");
            }

            enemy.TargetID = player.InstanceID;
            Verify(false, "enemy attacking the player");
            enemy.CastInfo = new ActorCastInfo();
            Verify(false, "enemy casting at the player");
            enemy.CastInfo = null;
            foreach (var unknown in new ulong[] { 0, 0xE0000000, 99, enemy.InstanceID })
            {
                enemy.TargetID = unknown;
                Verify(false, "missing, unresolved or self victim");
            }
            enemy.TargetID = tank.InstanceID;
            Verify(true, "another living player holding the enemy");
            tank.IsDead = true;
            Verify(false, "dead victim");
            tank.IsDead = false;
            tank.IsDestroyed = true;
            Verify(false, "destroyed victim");
            tank.IsDestroyed = false;
            enemy.TargetID = npc.InstanceID;
            Verify(true, "allied battle NPC holding the enemy");
            foreach (var type in new[] { ActorType.Pet, ActorType.Chocobo, ActorType.Buddy })
            {
                npc.Type = type;
                Verify(true, "allied combat companion holding the enemy");
            }
            npc.Type = ActorType.Enemy;
            npc.IsAlly = false;
            Verify(false, "unconfirmed hostile NPC victim");
            npc.IsAlly = true;
            enemy.NameID = 541;
            enemy.TargetID = player.InstanceID;
            Verify(true, "fixed striking dummy targeting the player");
            enemy.NameID = 0;
            Check(PositionalMovement.ForTarget(desired, null, player, actors) == Positional.Any,
                "A missing target cannot grant positional movement.");
            enemy.Omnidirectional = true;
            Verify(false, "omnidirectional enemy");
            enemy = Make(2, ActorType.Enemy);
            actors.Actors[enemy.InstanceID] = enemy;
        }
        Check(PositionalMovement.ForTarget(Positional.Front, enemy, player, actors) == Positional.Front
            && PositionalMovement.ForTarget(Positional.Any, enemy, player, actors) == Positional.Any,
            "Rear/flank eligibility preserves independent front and unrestricted positioning.");
    }
}
