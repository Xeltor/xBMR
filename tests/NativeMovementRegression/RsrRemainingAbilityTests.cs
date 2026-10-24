using BossMod.CombatAssistance.Integrations;
using BossMod.CombatAssistance.Support;

internal static class RsrRemainingAbilityTests
{
    public static void Run()
    {
        var rotation = new RotationSolver.Basic.Rotations.CustomRotation();
        var select = RsrSupportDutySlot.RemainingAbilities(rotation, typeof(TestRoleCenter));
        var original = new FakeSupportDuty();
        RsrSupportDutyTests.CurrentDuty = original;
        var fallback = new object();
        var slot = new RsrSupportDutySlot(typeof(RsrSupportDutyTests).GetProperty(nameof(RsrSupportDutyTests.CurrentDuty))!,
            select, args => { TestRoleCenter.Calls.Add("native"); args[1] = fallback; return true; });
        slot.Enter();
        foreach (var winner in new[] { "job", "potion", "role", "speed", "native" })
        {
            TestRoleCenter.Ready = winner;
            TestRoleCenter.AutoStatus = TestRoleStatus.Speed;
            TestRoleCenter.Calls.Clear();
            var found = RsrSupportDutyTests.CurrentDuty!.GeneralAbility("gcd", out var selected);
            var expected = winner == "native" ? fallback : TestRoleCenter.Action;
            Checks.Check(found && ReferenceEquals(selected, expected), "Preserve concrete original ability before native fallback: " + winner);
            string[] order = ["job", "potion", "role", "speed", "native"];
            Checks.Check(TestRoleCenter.Calls.SequenceEqual(order.Take(Array.IndexOf(order, winner) + 1)),
                "Original ability priority remains intact: " + winner);
        }
        TestRoleCenter.Ready = "speed";
        TestRoleCenter.AutoStatus = TestRoleStatus.None;
        TestRoleCenter.Calls.Clear();
        Checks.Check(RsrSupportDutyTests.CurrentDuty!.GeneralAbility("gcd", out var result) && ReferenceEquals(result, fallback)
            && TestRoleCenter.Calls.SequenceEqual(["job", "potion", "role", "native"]), "Original speed option remains binding.");
        slot.Exit();
        Checks.Check(ReferenceEquals(RsrSupportDutyTests.CurrentDuty, original), "Restore original duty after native ability scope.");
        RsrSupportDutyTests.CurrentDuty = null;
        Checks.Check(TankArmsLengthPolicy.ShouldUse(true, true, 2, false, float.MaxValue, false), "Tank slow has useful trash coverage.");
        foreach (var time in new[] { 0f, 120f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Checks.Check(!TankArmsLengthPolicy.ShouldUse(true, true, 2, false, time, false), "Known cooldown overlap or unknown clock preserves immunity.");
        Checks.Check(TankArmsLengthPolicy.ShouldUse(true, true, 2, false, 120.001f, false), "Actual cooldown boundary is explicit.");
        Checks.Check(!TankArmsLengthPolicy.ShouldUse(false, true, 2, false, float.MaxValue, false)
            && !TankArmsLengthPolicy.ShouldUse(true, false, 2, false, float.MaxValue, false)
            && !TankArmsLengthPolicy.ShouldUse(true, true, 1, false, float.MaxValue, false)
            && !TankArmsLengthPolicy.ShouldUse(true, true, 2, true, float.MaxValue, false)
            && !TankArmsLengthPolicy.ShouldUse(true, true, 2, false, float.MaxValue, true), "Role, trash, recipient, existing immunity and manual gates remain independent.");
        Checks.Check(TankArmsLengthPolicy.IsDungeonTrash("D260Example", true)
            && !TankArmsLengthPolicy.IsDungeonTrash("D261Boss", true)
            && !TankArmsLengthPolicy.IsDungeonTrash("D260Example", false)
            && !TankArmsLengthPolicy.IsDungeonTrash(null, true), "Only verified dungeon trash receives slow mitigation.");
    }
}

[Flags]
public enum TestRoleStatus { None = 0, Speed = 1 }
public static class TestRoleCenter
{
    public static int Role => 1;
    public static TestRoleStatus AutoStatus { get; set; }
    public static string Ready { get; set; } = "";
    public static List<string> Calls { get; } = [];
    public static object Action { get; } = new();
    public static bool Choose(string name, out object? action)
    { Calls.Add(name); action = Ready == name ? Action : null; return action != null; }
}

namespace RotationSolver.Basic.Rotations
{
    // Deliberately matches the supported reflection boundary without loading RSR.
    public class CustomRotation
    {
        public virtual bool GeneralAbility(object gcd, out object? action) => TestRoleCenter.Choose("job", out action);
        private static bool UseMpPotion(object gcd, out object? action) => TestRoleCenter.Choose("potion", out action);
        private bool GeneralUsingAbility(int role, object gcd, out object? action) => TestRoleCenter.Choose("role", out action);
        public virtual bool SpeedAbility(object gcd, out object? action) => TestRoleCenter.Choose("speed", out action);
    }
}
