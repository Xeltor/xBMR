using System.Reflection;
using System.Runtime.Loader;
using BossMod.CombatAssistance.Support;
using BossMod.CombatAssistance.Config;
using BossMod.CombatAssistance.Integrations;

internal static class RsrMeleeFallbackTests
{
    private static void Require(bool value, string message) => Checks.Check(value, message);

    public static void Regression()
    {
        Move[] moves = [Move.Ingress, Move.Shukuchi];
        var original = moves.ToArray();
        var action = new FakeAction(moves);
        var age = TimeSpan.Zero;
        var allowed = true;
        var manual = false;
        var preview = false;
        var reports = new List<bool>();
        var binding = new RsrMeleeFallbackBinding(action, moves, () => (uint)action.Last,
            () => age, () => allowed, () => manual, () => preview, reports.Add);
        var canUse = typeof(IFakeAction).GetMethod("CanUse")!;
        var use = typeof(IFakeAction).GetMethod("Use")!;
        object?[] Args() => [null, false, false, false, false, false, false, false, false, (byte)0, false, 0];
        bool Check() => binding.Invoke(canUse, Args()) is true;

        Require(!Check(), "the initial movement window remains withheld");
        age = TimeSpan.FromMilliseconds(799);
        Require(!Check(), "do not release before the movement window expires");
        age = TimeSpan.FromMilliseconds(800);
        Require(Check(), "a completed Ingress cannot indefinitely veto an otherwise usable Harpe");
        Require(action.Last == Move.Ingress && moves.SequenceEqual(original), "history and table restored after success");
        age = TimeSpan.FromSeconds(20);
        Require(Check(), "idle time alone must release the hold without needing an intervening action");

        foreach (var gate in new[] { "range", "cast", "cooldown", "enabled", "target" })
        {
            action.Rejection = gate;
            Require(!Check(), "native rejection survives the correction: " + gate);
            Require(moves.SequenceEqual(original), "table restored after rejection: " + gate);
        }
        action.Rejection = null;
        action.Throw = true;
        var threw = false;
        try { Check(); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw && moves.SequenceEqual(original), "exception restores history before leaving CanUse");
        action.Throw = false;
        action.DuringCheck = () => moves[1] = Move.Other;
        Require(Check() && moves[0] == Move.Ingress && moves[1] == Move.Other,
            "restoration owns one slot and preserves unrelated table edits");
        moves[1] = Move.Shukuchi;
        action.DuringCheck = () => moves[0] = Move.Other;
        threw = false;
        try { Check(); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw && moves[0] == Move.Other, "foreign takeover of the leased slot is preserved and reported");
        moves[0] = Move.Ingress;
        action.DuringCheck = null;
        manual = true;
        Require(!Check(), "an explicit manual action command retains native eligibility");
        manual = false;
        allowed = false;
        Require(!Check(), "manual input, disabled feature, wrong job or mode cannot use the correction");
        allowed = true;
        Require(Check(), "automatic candidate selected before handoff");
        allowed = false;
        Require(binding.Invoke(use, []) is false && action.Uses == 0, "a corrected cached action yields at manual handoff");
        allowed = true;
        preview = true;
        var count = reports.Count;
        Require(Check() && reports.Count == count, "preview neither reports execution nor commits corrected selection");
        preview = false;
        action.Last = Move.Other;
        Require(Check() && reports.Count == count, "unrelated action history passes through unchanged");
        action.Last = Move.Ingress;
        age = TimeSpan.FromSeconds(-1);
        Require(!Check(), "a clock reversal cannot authorize release");

        // Exercise the real generated-getter lease, including a retained proxy.
        age = TimeSpan.FromSeconds(2);
        var rotation = new ThirdPartyRotation(action);
        IFakeAction proxy;
        using (var lease = RsrSupportActionLease.Acquire(rotation, "HarpePvE", (_, method, arguments) => binding.Invoke(method, arguments)))
        {
            proxy = rotation.HarpePvE;
            Require(proxy.CanUse(out var selected) && ReferenceEquals(selected, proxy), "the selected action retains the guarded proxy");
            Require(selected.Use() && action.Uses == 1, "normal RSR execution still owns the request");
        }
        Require(ReferenceEquals(rotation.HarpePvE, action) && !proxy.CanUse(out _), "dispose restores getter and makes retained proxies transparent");

        var config = new AssistancePreferences();
        Require(config.FixRsrMeleeRangedAfterMovement, "new settings default to correction enabled");
        config.FixRsrMeleeRangedAfterMovement = false;
        var saved = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        Require(!Newtonsoft.Json.JsonConvert.DeserializeObject<AssistancePreferences>(saved)!.FixRsrMeleeRangedAfterMovement,
            "saved opt-out survives deserialization");
        config.ResetAll();
        Require(config.FixRsrMeleeRangedAfterMovement && config.Enabled, "reset restores assistance defaults; native activation remains a separate control");
        Require(RsrMeleeFallbackController.ActionProperty(41) == null && RsrMeleeFallbackController.ActionProperty(39) == "HarpePvE",
            "Viper positioning and unsupported jobs are outside this compatibility correction");
    }

    public static int InstalledContract(string directory)
    {
        var context = new AssemblyLoadContext("rsr-melee-fallback-contract", isCollectible: true);
        context.Resolving += (_, name) =>
        {
            var path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(Path.GetFullPath(path)) : null;
        };
        try
        {
            var basic = context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(directory, "RotationSolver.Basic.dll")));
            var plugin = context.LoadFromAssemblyPath(Path.GetFullPath(Path.Combine(directory, "RotationSolver.dll")));
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var helper = basic.GetType("RotationSolver.Basic.Helpers.IActionHelper", true)!;
            var moves = (Array)helper.GetProperty("MovingActions", statics)!.GetValue(null)!;
            var values = moves.Cast<object>().Select(Convert.ToUInt32).ToArray();
            Require(values.Length > 0 && values.Distinct().Count() == values.Length && !values.Contains(0u), "history table has unique nonzero actions without a fixed release-specific length");
            Require(new uint[] { 24401, 96, 2262, 7492 }.All(values.Contains), "all four supported job movement actions exist in the native veto");
            var data = basic.GetType("RotationSolver.Basic.DataCenter", true)!;
            Require(data.GetProperty("TimeSinceLastAction", statics)!.PropertyType == typeof(TimeSpan), "history age remains available without spoofing LastAction");
            var actionInterface = basic.GetType("RotationSolver.Basic.Actions.IBaseAction", true)!;
            Require(actionInterface.GetMethod("CanUse")!.GetParameters().Length == 12, "native CanUse contract");
            foreach (var job in new uint[] { 22, 30, 34, 39 })
            {
                var baseName = job switch { 22 => "Dragoon", 30 => "Ninja", 34 => "Samurai", _ => "Reaper" };
                var rotation = basic.GetType($"RotationSolver.Basic.Rotations.Basic.{baseName}Rotation", true)!;
                var property = RsrMeleeFallbackController.ActionProperty(job)!;
                Require(rotation.GetProperty(property)!.PropertyType == actionInterface, "native fallback property for " + job);
                var field = rotation.GetField("_" + property + "Creator", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(field != null && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Lazy<>), "generated lease slot for " + job);
            }
            Console.WriteLine("PASS installed RSR melee fallback contract: " + basic.ManifestModule.ModuleVersionId);
            return 0;
        }
        finally { context.Unload(); }
    }

    public enum Move : uint { Other = 1, Shukuchi = 2262, Ingress = 24401 }

    public interface IFakeAction
    {
        bool CanUse(out IFakeAction action, bool a = false, bool b = false, bool c = false, bool d = false,
            bool e = false, bool f = false, bool g = false, bool h = false, byte i = 0, bool j = false, int k = 0);
        bool Use();
    }

    public sealed class FakeAction(Move[] moves) : IFakeAction
    {
        public Move Last = Move.Ingress;
        public string? Rejection;
        public bool Throw;
        public Action? DuringCheck;
        public int Uses;
        public bool CanUse(out IFakeAction action, bool a = false, bool b = false, bool c = false, bool d = false,
            bool e = false, bool f = false, bool g = false, bool h = false, byte i = 0, bool j = false, int k = 0)
        {
            action = this;
            if (Throw) throw new InvalidOperationException("native eligibility failed");
            DuringCheck?.Invoke();
            return Rejection == null && !moves.Contains(Last);
        }
        public bool Use() { Uses++; return true; }
    }

    public class FakeRotation(IFakeAction action)
    {
        private readonly Lazy<IFakeAction> _HarpePvECreator = new(() => action);
        public IFakeAction HarpePvE => _HarpePvECreator.Value;
    }
    public sealed class ThirdPartyRotation(IFakeAction action) : FakeRotation(action);

}
