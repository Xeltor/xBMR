using BossMod.CombatAssistance.Integrations;

internal static class RsrSupportDutyTests
{
    public static FakeSupportDuty? CurrentDuty { get; set; }

    public static void PriorityAndRestoration()
    {
        var original = new FakeSupportDuty();
        CurrentDuty = original;
        var order = new List<string>();
        var jobReady = true;
        var adapter = new RsrSupportDutySlot(typeof(RsrSupportDutyTests).GetProperty(nameof(CurrentDuty))!,
            arguments => { order.Add("job"); arguments[1] = "job action"; return jobReady; },
            arguments => { order.Add("support"); arguments[1] = "support action"; return true; },
            arguments => { order.Add("recovery"); arguments[0] = "recovery action"; return true; });
        adapter.Enter();
        Require(CurrentDuty!.HealSingleAbility("gcd", out var heal) && Equals(heal, "heal"), "Preserve original duty healing.");
        Require(CurrentDuty.GeneralGCD(out var recovery) && Equals(recovery, "recovery action"), "Recovery uses the general GCD fallback.");
        original.HealReady = true;
        order.Clear();
        Require(CurrentDuty.GeneralGCD(out recovery) && Equals(recovery, "duty action") && order.Count == 0,
            "An existing duty GCD always precedes raised-player recovery.");
        order.Clear();
        Require(CurrentDuty.GeneralAbility("gcd", out var action) && Equals(action, "job action") && order.SequenceEqual(["job"]),
            "Existing job general actions precede any transfer.");
        order.Clear();
        jobReady = false;
        Require(CurrentDuty.GeneralAbility("gcd", out action) && Equals(action, "support action") && order.SequenceEqual(["job", "support"]),
            "Transfers use the empty general-ability slot.");
        adapter.Exit();
        Require(ReferenceEquals(CurrentDuty, original), "Restore the exact original duty instance.");
        adapter.Enter();
        var replacement = new FakeSupportDuty();
        CurrentDuty = replacement;
        adapter.Exit();
        Require(ReferenceEquals(CurrentDuty, replacement), "Preserve a later owner's replacement.");
        CurrentDuty = null;
    }

    private static void Require(bool value, string reason)
    {
        Checks.Check(value, reason);
    }
}

public class FakeSupportDuty
{
    public bool HealReady { get; set; }
    public virtual bool GeneralGCD(out object? action) { action = this.HealReady ? "duty action" : null; return this.HealReady; }
    public virtual bool GeneralAbility(object gcd, out object? action) { action = null; return false; }
    public virtual bool HealSingleAbility(object gcd, out object? action) { action = "heal"; return true; }
}
