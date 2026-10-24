using System.Numerics;

namespace BossMod.CombatAssistance.Knockback;

internal sealed record KnockbackActivationWindow(DateTime EarliestUtc, DateTime ExpectedUtc,
    DateTime LatestUtc, DateTime LandingLatestUtc);

// One short-lived completion for a native published source. Polling does not
// renew it, and neither source removal nor a timeout is physical completion.
internal sealed record KnockbackCompletionEvidence(uint Territory, ulong Actor, ulong SourceActor,
    uint SourceDataId, uint CastActionId, uint EffectActionId, uint EffectCounter,
    Vector2 Launch, Vector2 Landing, DateTime StartedUtc, DateTime EndedUtc, DateTime ObservedUtc,
    ulong CastGeneration = 0);
