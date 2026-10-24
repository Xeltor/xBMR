// Adapted from XCAI 4a0f7968; external RSR selection remains scoped to its original update.
using System;
using System.Collections.Generic;

namespace BossMod.CombatAssistance.Support;

internal enum SupportAction
{
    Resurrection,
    Kardia,
    DancePartner,
    FeyUnion,
    Balance,
    Spear,
    Bole,
    Arrow,
    Spire,
    Ewer
}

internal enum SupportRole
{
    Tank,
    Healer,
    Melee,
    Ranged
}

// A current observation, not a combat-history record. Availability for an
// application is deliberately separate from value as an ongoing recipient.
internal readonly record struct SupportRecipient(
    ulong Id,
    SupportRole Role,
    bool Alive,
    bool CanApply,
    float Health,
    int Attackers = 0,
    bool IncomingHit = false,
    float Shield = 0,
    bool HasOtherPartner = false,
    bool HasOtherKardion = false,
    bool HasEquivalentCard = false,
    bool HealingSuppressed = false,
    float DamageMultiplier = 1,
    int JobPriority = 0,
    bool CannotAttack = false,
    bool HasOtherFeyUnion = false,
    bool RefreshingOwnCard = false);

internal readonly record struct SupportDecision(ulong Recipient, bool Change, string Reason);

internal static class SupportRecipientPolicy
{
    public static bool IsAssignment(SupportAction action)
        => action is SupportAction.Kardia or SupportAction.DancePartner or SupportAction.FeyUnion;

    public static float Score(SupportAction action, in SupportRecipient member)
    {
        if (member.Id == 0 || !member.Alive || !float.IsFinite(member.Health) ||
            !float.IsFinite(member.Shield) || !float.IsFinite(member.DamageMultiplier)) return -1;

        var missing = 1 - Math.Clamp(member.Health, 0, 1);
        var pressure = Math.Min(Math.Max(member.Attackers, 0), 4) * 0.35f + (member.IncomingHit ? 1 : 0);
        var tank = member.Role == SupportRole.Tank;
        var healing = member.HealingSuppressed ? 0 : missing;
        var protection = Math.Max(0, pressure - Math.Clamp(member.Shield, 0, 1));

        switch (action)
        {
            case SupportAction.Kardia:
                // Actual tank responsibility dominates a briefly injured DPS.
                // Other Sages' assignments are a tie-breaker, not a prohibition.
                return member.HealingSuppressed ? 0 : (tank ? 1 : 0) + pressure * 2 + healing;
            case SupportAction.FeyUnion:
                // A finite healing resource must not be spent on pure overheal.
                return member.HealingSuppressed ? 0 : healing * 3 + pressure * 0.7f;
            case SupportAction.DancePartner:
            case SupportAction.Balance:
            case SupportAction.Spear:
                if (action == SupportAction.DancePartner && member.HasOtherPartner) return -1;
                if (action != SupportAction.DancePartner && member.HasEquivalentCard) return -1;
                // This is a conservative prior, never a claim to measured DPS.
                // Job ordering only breaks near-equivalent role choices.
                var damage = member.Role is SupportRole.Melee or SupportRole.Ranged ? 1f : 0.5f;
                damage *= Math.Clamp(member.DamageMultiplier, 0, 1);
                damage *= 1 - Math.Clamp(member.JobPriority, 0, 30) * 0.005f;
                if (member.CannotAttack) damage *= 0.1f;
                if (action == SupportAction.Balance)
                    damage *= member.Role is SupportRole.Melee or SupportRole.Tank ? 2 : 1;
                if (action == SupportAction.Spear)
                    damage *= member.Role is SupportRole.Ranged or SupportRole.Healer ? 2 : 1;
                return damage;
            case SupportAction.Bole:
                return member.HasEquivalentCard ? -1 : protection * 2 + missing * 0.2f + (tank ? 0.1f : 0);
            case SupportAction.Spire:
                return member.HasEquivalentCard ? -1 : protection + missing * 0.5f + (tank ? 0.1f : 0);
            case SupportAction.Arrow:
                return member.HasEquivalentCard ? -1 : member.HealingSuppressed ? 0 : healing * (1 + pressure);
            case SupportAction.Ewer:
                return member.HasEquivalentCard ? -1 : member.HealingSuppressed ? 0 : healing + pressure * 0.5f;
            default:
                return -1;
        }
    }

    // Observed coverage only resolves equal healing value. It never substitutes
    // for health/pressure or makes an established assignment move.
    public static int CoverageTieBreak(SupportAction action, in SupportRecipient member)
        => (action == SupportAction.FeyUnion && member.HasOtherFeyUnion ||
            action == SupportAction.Kardia && member.HasOtherKardion) ? 1 : 0;

    public static SupportRecipient? Best(SupportAction action, IReadOnlyList<SupportRecipient> party,
        ulong current, bool executableOnly)
    {
        SupportRecipient? best = null;
        var score = -1f;
        foreach (var member in party)
        {
            if (executableOnly && !member.CanApply) continue;
            var value = Score(action, member);
            if (value < 0) continue;
            if (best == null || value > score || (value == score &&
                (member.Id == current || (best.Value.Id != current &&
                 (CoverageTieBreak(action, member) < CoverageTieBreak(action, best.Value) ||
                  CoverageTieBreak(action, member) == CoverageTieBreak(action, best.Value) && member.Id < best.Value.Id)))))
            {
                best = member;
                score = value;
            }
        }

        return best;
    }
}

// One instance per persistent ability. Stores only assignment commitment and
// the currently qualifying challenger; it never samples or retains a fight.
internal sealed class SupportAssignment
{
    private ulong observed;
    private ulong expected;
    private DateTime expectedUntil;
    private ulong challenger;
    private DateTime challengerSince;
    private DateTime changedAt;
    private DateTime lastObservation;
    private ulong manualRecipient;
    private bool manualRemoval;
    private bool initialized;

    public bool ManualOwned => this.manualRecipient != 0 || this.manualRemoval;

    public void Reset()
    {
        this.observed = this.expected = this.challenger = this.manualRecipient = 0;
        this.challengerSince = this.changedAt = this.lastObservation = default;
        this.expectedUntil = default;
        this.initialized = false;
        this.manualRemoval = false;
    }

    // Called only after a successful executable request, not on a prediction.
    // The actual status observation still confirms the assignment.
    public void Expect(ulong recipient, DateTime now)
    {
        this.expected = recipient;
        this.expectedUntil = now.AddSeconds(2);
    }

    public SupportDecision Evaluate(SupportAction action, IReadOnlyList<SupportRecipient> party,
        ulong current, DateTime now, bool manualSelection = false)
    {
        if (!SupportRecipientPolicy.IsAssignment(action))
            throw new ArgumentException("Cards do not use assignment commitment.", nameof(action));

        if (this.initialized && now < this.lastObservation) this.Reset();
        if (this.initialized && now - this.lastObservation > TimeSpan.FromSeconds(2)) this.ClearChallenger();
        if (now > this.expectedUntil) this.expected = 0;
        this.lastObservation = now;
        if (!this.initialized)
        {
            this.initialized = true;
            this.observed = current;
            this.changedAt = now;
        }
        else if (current != this.observed)
        {
            if (current == 0 && action != SupportAction.FeyUnion && now > this.expectedUntil)
            {
                foreach (var member in party)
                    if (member.Id == this.observed && member.Alive) this.manualRemoval = true;
            }
            if (current != 0)
            {
                this.manualRemoval = false;
                if (current != this.expected) this.manualRecipient = current;
            }
            this.observed = current;
            if (current == this.expected) this.expected = 0;
            this.changedAt = now;
            this.ClearChallenger();
        }

        if (manualSelection)
        {
            this.manualRecipient = current;
            this.manualRemoval = current == 0;
        }
        if (this.manualRemoval && current == 0)
            return new(0, false, "Keeping manual removal until territory change or management reset");
        SupportRecipient? incumbent = null;
        foreach (var member in party)
            if (member.Id == current && member.Alive) incumbent = member;

        if (incumbent == null) this.manualRecipient = 0;
        if (this.manualRecipient != 0 && this.manualRecipient == current)
            return new(current, false, "Keeping manual assignment");

        // Evaluate the whole party first. Filtering to cast range here would
        // turn a temporary range crossing into an assignment change.
        var best = SupportRecipientPolicy.Best(action, party, current, executableOnly: false);
        if (best == null)
        {
            this.ClearChallenger();
            return new(current, false, "No eligible recipient");
        }

        if (best.Value.Id == current)
        {
            this.ClearChallenger();
            return new(current, false, "Current recipient remains valuable");
        }

        if (incumbent == null && !best.Value.CanApply)
        {
            var available = SupportRecipientPolicy.Best(action, party, current, executableOnly: true);
            if (available != null)
                return new(available.Value.Id, true, "Establish useful coverage while the preferred recipient is unreachable");
        }
        if (!best.Value.CanApply)
        {
            this.ClearChallenger();
            return new(current, false, "Preferred recipient is not currently reachable");
        }

        if (incumbent == null)
            return new(best.Value.Id, true, current == 0 ? "Establish assignment" : "Recover lost recipient");

        var currentValue = SupportRecipientPolicy.Score(action, incumbent.Value);
        var bestValue = SupportRecipientPolicy.Score(action, best.Value);
        var requiredGain = action == SupportAction.DancePartner ? 0.2f : 0.5f;
        if (bestValue - currentValue < requiredGain || bestValue < currentValue * 1.25f)
        {
            this.ClearChallenger();
            return new(current, false, "Improvement is too small to justify a transfer");
        }

        if (this.challenger != best.Value.Id)
        {
            this.challenger = best.Value.Id;
            this.challengerSince = now;
        }

        var confirmation = action == SupportAction.DancePartner ? 10 : 3;
        var minimumHold = action == SupportAction.DancePartner ? 30 : 8;
        if ((now - this.challengerSince).TotalSeconds < confirmation ||
            (now - this.changedAt).TotalSeconds < minimumHold)
            return new(current, false, "Waiting for a sustained improvement");

        return new(best.Value.Id, true, "Sustained benefit justifies transfer");
    }

    private void ClearChallenger()
    {
        this.challenger = 0;
        this.challengerSince = default;
    }
}
