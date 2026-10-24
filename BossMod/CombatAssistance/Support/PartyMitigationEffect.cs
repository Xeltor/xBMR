// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
namespace BossMod.CombatAssistance.Support;

internal readonly record struct PartyMitigationEffect(ulong Actor, ulong Source, uint Status, float RemainingSeconds);
