namespace BossMod.CombatAssistance.Planning;

// Last framework-thread planning call only; DevBridge owns sampling/history.
internal readonly record struct MovementPlanningCost(double TotalMilliseconds, double InputMilliseconds,
    double CombatMilliseconds, double ContextMilliseconds, double ServiceMilliseconds, double ExecutionMilliseconds,
    long AllocatedBytes, int Cells, bool IdleSkipped);

// Immutable publication from the last finished/cancelled synchronous search job.
internal sealed record MovementSearchCost(double TotalMilliseconds, long AllocatedBytes, bool Cancelled);
