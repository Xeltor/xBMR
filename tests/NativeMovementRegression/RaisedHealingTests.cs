using BossMod.CombatAssistance.Support;



internal static class RaisedHealingTests
{
    public static void Recovery()
    {
        var now = DateTime.UtcNow;
        var policy = new RaisedHealingPolicy();
        bool Observe(bool dead, uint hp, DateTime at, bool visible = true, bool party = false)
            => policy.Observe(42, visible, dead, party, hp, 100, at);
        Checks.Check(!Observe(false, 20, now), "Unraised strangers never become healing patients");
        policy.ObserveRaise(42, now);
        Checks.Check(!Observe(true, 0, now.AddSeconds(1)), "Wait for raise acceptance");
        Checks.Check(Observe(false, 20, now.AddSeconds(2)), "Revival begins recovery");
        Checks.Check(Observe(false, 80, now.AddSeconds(3)), "One recovery may need multiple heals");
        Checks.Check(!Observe(false, 100, now.AddSeconds(4)), "Another healer reaching full HP also consumes recovery");
        Checks.Check(!Observe(false, 40, now.AddSeconds(5)), "Later damage never reopens recovery");
        policy.ObserveRaise(42, now.AddSeconds(6));
        Checks.Check(Observe(false, 20, now.AddSeconds(7)), "A new confirmed raise opens a new episode");
        Checks.Check(!Observe(true, 0, now.AddSeconds(8)) && !Observe(false, 20, now.AddSeconds(9)), "Second death abandons the old recovery");
        policy.ObserveRaise(42, now);
        Checks.Check(!Observe(false, 20, now.AddSeconds(90)), "Unaccepted raises expire");
        policy.ObserveRaise(42, now);
        Checks.Check(Observe(false, 20, now.AddSeconds(1)) && !Observe(false, 20, now.AddSeconds(31)), "Active recovery expires after thirty seconds");
        policy.ObserveRaise(42, now);
        Checks.Check(!Observe(false, 20, now, party: true) && policy.Actors.Length == 0, "Joining party releases external responsibility");
        policy.ObserveRaise(42, now);
        Observe(false, 20, now);
        Checks.Check(!Observe(false, 20, now.AddSeconds(1), visible: false) && policy.Actors.Length == 0, "Leaving visibility after revival releases responsibility");
        policy.ObserveRaise(42, now);
        policy.Clear();
        Checks.Check(policy.Actors.Length == 0, "Reset discards recovery");
    }
}
