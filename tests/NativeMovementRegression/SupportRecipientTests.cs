using BossMod.CombatAssistance.Support;

internal static class SupportRecipientTests
{
    private static readonly DateTime Start = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

    public static void SustainedResponsibility()
    {
        var assignment = new SupportAssignment();
        SupportRecipient[] party = [Tank(1, 0), Tank(2, 3), new(3, SupportRole.Melee, true, true, 0.4f)];
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 1, Start).Change, "Do not transfer on the first sample.");
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 1, Start.AddSeconds(3)).Change, "Respect assignment hold.");
        for (var second = 4; second < 8; second++) assignment.Evaluate(SupportAction.Kardia, party, 1, Start.AddSeconds(second));
        var transfer = assignment.Evaluate(SupportAction.Kardia, party, 1, Start.AddSeconds(8));
        Require(transfer.Change && transfer.Recipient == 2, "Sustained active tank responsibility beats off-tank and briefly injured DPS.");
        assignment.Expect(2, Start.AddSeconds(8));
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(9)).Change, "Observe successful transfer.");
        party[0] = Tank(1, 4);
        party[1] = Tank(2, 0);
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(10)).Change, "No immediate bounce back.");
        for (var second = 11; second < 18; second++) assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(second));
        Require(assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(18)).Recipient == 1,
            "An expected transfer must not be mistaken for a manual assignment.");
    }

    public static void RangeAndManualOwnership()
    {
        var assignment = new SupportAssignment();
        SupportRecipient[] party = [Tank(1, 3) with { CanApply = false }, Tank(2, 0)];
        Require(assignment.Evaluate(SupportAction.Kardia, party, 1, Start).Recipient == 1,
            "An out-of-cast-range incumbent is still the preferred healing recipient.");
        var initial = new SupportAssignment();
        Require(initial.Evaluate(SupportAction.Kardia, party, 0, Start).Recipient == 2,
            "With no assignment, establish useful reachable coverage rather than leave the buff absent.");
        party[0] = Tank(1, 0);
        party[1] = Tank(2, 4) with { CanApply = false };
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 1, Start.AddSeconds(30)).Change,
            "Never clear an existing assignment before the preferred replacement is executable.");
        party[1] = Tank(2, 4);
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 1, Start.AddSeconds(31)).Change,
            "Entering range does not complete a previously unobservable transfer window.");

        assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(32));
        party[0] = Tank(1, 4);
        party[1] = Tank(2, 0);
        Require(!assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(80)).Change,
            "An externally changed assignment is manual and must remain owned by the player.");
        var removed = new SupportAssignment();
        removed.Evaluate(SupportAction.DancePartner, party, 1, Start);
        Require(!removed.Evaluate(SupportAction.DancePartner, party, 0, Start.AddSeconds(3)).Change,
            "Manual removal must not immediately restore the partnership.");
        removed.Reset();
        Require(removed.Evaluate(SupportAction.DancePartner, party, 0, Start.AddSeconds(4)).Change,
            "Management reset releases the manual removal hold.");
        party[1] = party[1] with { Alive = false };
        Require(assignment.Evaluate(SupportAction.Kardia, party, 2, Start.AddSeconds(81)).Recipient == 1,
            "Death permits recovery from a lost manual assignment.");
    }

    public static void DamageAndCards()
    {
        SupportRecipient[] party =
        [
            new(1, SupportRole.Melee, true, true, 1, JobPriority: 0),
            new(2, SupportRole.Melee, true, true, 1, JobPriority: 1),
            new(3, SupportRole.Ranged, true, true, 1)
        ];
        var assignment = new SupportAssignment();
        assignment.Evaluate(SupportAction.DancePartner, party, 2, Start);
        Require(!assignment.Evaluate(SupportAction.DancePartner, party, 2, Start.AddMinutes(1)).Change,
            "Small job-priority differences do not justify disrupting an existing partnership.");
        party[1] = party[1] with { DamageMultiplier = 0.5f };
        Require(!assignment.Evaluate(SupportAction.DancePartner, party, 2, Start.AddSeconds(61)).Change,
            "A newly observed disadvantage needs confirmation.");
        for (var second = 62; second < 72; second++) assignment.Evaluate(SupportAction.DancePartner, party, 2, Start.AddSeconds(second));
        Require(assignment.Evaluate(SupportAction.DancePartner, party, 2, Start.AddSeconds(72)).Change,
            "A lasting large disadvantage warrants a transfer.");
        Require(!assignment.Evaluate(SupportAction.DancePartner, party, 2, Start.AddSeconds(80)).Change,
            "A gap in observations must not count as sustained improvement.");
        Require(SupportRecipientPolicy.Best(SupportAction.Balance, party, 0, true)?.Id == 1,
            "Balance uses its melee role benefit.");
        Require(SupportRecipientPolicy.Best(SupportAction.Spear, party, 0, true)?.Id == 3,
            "Spear uses its ranged role benefit.");
        party[0] = party[0] with { CanApply = false };
        Require(SupportRecipientPolicy.Best(SupportAction.Balance, party, 0, true)?.Id != 1,
            "A card chooses an executable recipient now instead of holding the draw/play cycle.");
        party[2] = party[2] with { HasOtherPartner = true };
        Require(SupportRecipientPolicy.Best(SupportAction.DancePartner, party, 0, true)?.Id == 2,
            "Do not duplicate another Dancer's partner buff.");
        party[2] = party[2] with { HasOtherPartner = false, HasEquivalentCard = true };
        Require(SupportRecipientPolicy.Best(SupportAction.Spear, party, 0, true)?.Id == 2,
            "Avoid overwriting an equivalent active card.");
    }

    public static void HealingCardsAndUnion()
    {
        var tank = Tank(1, 3) with { Health = 1 };
        var injured = new SupportRecipient(2, SupportRole.Melee, true, true, 0.4f);
        SupportRecipient[] party = [tank, injured];
        Require(SupportRecipientPolicy.Best(SupportAction.Bole, party, 0, true)?.Id == 1, "Mitigate incoming pressure.");
        Require(SupportRecipientPolicy.Best(SupportAction.Spire, party, 0, true)?.Id == 1, "Shield ahead of damage.");
        Require(SupportRecipientPolicy.Best(SupportAction.Arrow, party, 0, true)?.Id == 2, "Amplify useful healing.");
        Require(SupportRecipientPolicy.Best(SupportAction.Ewer, party, 0, true)?.Id == 2, "Put regen on useful healing need.");
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, party, 0, true)?.Id == 2,
            "Gauge healing should not default to an uninjured tank solely because of role.");
        party[1] = injured with { HealingSuppressed = true };
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, party, 0, true)?.Id == 1,
            "Respect explicit healing suppression.");
        party[0] = tank with { Health = float.NaN };
        party[1] = injured with { Alive = false };
        Require(SupportRecipientPolicy.Best(SupportAction.Ewer, party, 0, true) == null, "Invalid observations cannot win.");
    }

    public static void CardRefreshOwnership()
    {
        Require(!SupportEffectOwnership.BlocksCardRecipient(3890, 3890, true, true),
            "An RSR-eligible own card can refresh before its observed status vanishes.");
        Require(SupportEffectOwnership.BlocksCardRecipient(3890, 3890, true, false) &&
            SupportEffectOwnership.BlocksCardRecipient(3890, 3890, false, true) &&
            !SupportEffectOwnership.BlocksCardRecipient(3888, 3890, false, true) &&
            !SupportEffectOwnership.BlocksCardRecipient(0, 0, true, true),
            "Unapproved local refresh and foreign overlap remain distinct from a different or unknown effect.");
        var urgent = new SupportRecipient(1, SupportRole.Tank, true, true, 0.4f, IncomingHit: true,
            HasEquivalentCard: SupportEffectOwnership.BlocksCardRecipient(3890, 3890, true, true), RefreshingOwnCard: true);
        var other = new SupportRecipient(2, SupportRole.Tank, true, true, 0.9f);
        Require(SupportRecipientPolicy.Best(SupportAction.Bole, [urgent, other], 0, true)?.Id == 1,
            "An approved refresh retains imminent-damage priority.");
        var local = new SupportDecision(1, false, "refresh");
        Require(SupportRecipientPolicy.Best(SupportAction.Bole, [urgent with { HasEquivalentCard = true }, other], 0, true)?.Id == 2,
            "Another Astrologian's equivalent card still favors a useful uncovered recipient.");
    }

    public static void ObservedKardiaCoverage()
    {
        HashSet<ulong> sages = [10];
        Require(SupportEffectOwnership.IsOtherKardion(2605, 10, 20, sages),
            "A living visible party Sage establishes observed Kardion coverage.");
        Require(!SupportEffectOwnership.IsOtherKardion(2605, 0, 20, sages) &&
            !SupportEffectOwnership.IsOtherKardion(2605, 20, 20, sages) &&
            !SupportEffectOwnership.IsOtherKardion(2605, 11, 20, sages) &&
            !SupportEffectOwnership.IsOtherKardion(2605, 10, 0, sages) &&
            !SupportEffectOwnership.IsOtherKardion(1223, 10, 20, sages) &&
            !SupportEffectOwnership.IsOtherKardion(2605, 10, 20, new HashSet<ulong>()),
            "Unknown, own, absent, dead or non-Sage sources and other effects do not establish peer Kardion coverage.");
        var covered = new SupportRecipient(1, SupportRole.Tank, true, true, 0.5f, HasOtherKardion: true);
        var uncovered = covered with { Id = 2, HasOtherKardion = false };
        var initial = new SupportAssignment().Evaluate(SupportAction.Kardia, [covered, uncovered], 0, Start);
        Require(initial.Recipient == 2, "Equal needs establish complementary Sage coverage.");
        Require(SupportRecipientPolicy.Best(SupportAction.Kardia, [covered, uncovered with { Health = 0.6f }], 0, true)?.Id == 1,
            "Another Sage's assignment cannot outweigh greater healing need.");
        Require(SupportRecipientPolicy.Best(SupportAction.Kardia, [covered with { IncomingHit = true }, uncovered], 0, true)?.Id == 1,
            "Imminent damage wins over complementary coverage.");
        Require(SupportRecipientPolicy.Best(SupportAction.Kardia, [covered, uncovered], 1, true)?.Id == 1,
            "Equal needs retain an established assignment.");
        Require(new SupportAssignment().Evaluate(SupportAction.Kardia, [covered, uncovered with { CanApply = false }], 0, Start).Recipient == 1,
            "A covered reachable ally still receives useful healing.");
        Require(SupportRecipientPolicy.Best(SupportAction.Kardia, [covered with { HasOtherKardion = false }, uncovered], 0, true)?.Id == 1,
            "Lost coverage restores stable local ordering.");
    }

    public static void ObservedFairyCoverage()
    {
        var covered = new SupportRecipient(1, SupportRole.Tank, true, true, 0.5f, HasOtherFeyUnion: true);
        var uncovered = covered with { Id = 2, HasOtherFeyUnion = false };
        SupportRecipient[] party = [covered, uncovered];
        var initial = new SupportAssignment().Evaluate(SupportAction.FeyUnion, party, 0, Start);
        Require(initial.Change && initial.Recipient == 2, "Equal healing need complements a visible foreign fairy tether.");
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, [uncovered, covered], 0, true)?.Id == 2,
            "Coverage preference is independent of enumeration order.");
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, party, 1, true)?.Id == 1,
            "An established local tether wins an equal-value tie.");
        var existing = new SupportAssignment();
        existing.Evaluate(SupportAction.FeyUnion, party, 1, Start);
        Require(!existing.Evaluate(SupportAction.FeyUnion, party, 1, Start.AddSeconds(30)).Change,
            "Foreign coverage alone never causes a transfer or resource disruption.");
        party[0] = covered with { Health = 0.1f };
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, party, 0, true)?.Id == 1,
            "Greater healing need wins even while another Scholar is helping.");
        party[0] = covered with { IncomingHit = true };
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, party, 0, true)?.Id == 1,
            "Imminent damage outranks complementary coverage.");
        party[0] = covered;
        party[1] = uncovered with { CanApply = false };
        Require(new SupportAssignment().Evaluate(SupportAction.FeyUnion, party, 0, Start).Recipient == 1,
            "Unreachable alternatives never prevent useful overlapping healing.");
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion, [covered], 0, true)?.Id == 1,
            "The sole useful recipient is never withheld on observed coverage.");
        Require(SupportRecipientPolicy.Best(SupportAction.Kardia, [covered, uncovered], 0, true)?.Id == 1,
            "A fairy tether does not imply equivalent Kardia coverage.");
        Require(SupportRecipientPolicy.Best(SupportAction.FeyUnion,
            [covered with { HasOtherFeyUnion = false }, uncovered], 0, true)?.Id == 1,
            "Disappearing effect immediately restores ordinary deterministic selection.");

        HashSet<ulong> scholars = [10];
        Require(SupportEffectOwnership.IsOtherFeyUnion(1223, 11, 10, 20, scholars) &&
            SupportEffectOwnership.IsOtherFeyUnion(1223, 10, 0, 20, scholars),
            "Observed effect can identify a visible party Scholar directly or through its fairy owner.");
        Require(!SupportEffectOwnership.IsOtherFeyUnion(1223, 21, 20, 20, scholars) &&
            !SupportEffectOwnership.IsOtherFeyUnion(1223, 20, 0, 20, scholars) &&
            !SupportEffectOwnership.IsOtherFeyUnion(1223, 11, 0, 20, scholars) &&
            !SupportEffectOwnership.IsOtherFeyUnion(1223, 0, 0, 20, scholars) &&
            !SupportEffectOwnership.IsOtherFeyUnion(1223, 11, 10, 20, new HashSet<ulong>()) &&
            !SupportEffectOwnership.IsOtherFeyUnion(2605, 11, 10, 20, scholars),
            "Own effects, unresolved owners, missing Scholars and different effects are not foreign fairy coverage.");
    }

    private static SupportRecipient Tank(ulong id, int attackers) => new(id, SupportRole.Tank, true, true, 0.9f, attackers);
    private static void Require(bool condition, string message)
    {
        Checks.Check(condition, message);
    }
}
