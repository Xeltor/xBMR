using BossMod.CombatAssistance.Integrations;

public static class RsrMitigationSelectionTests
{
    public static void Run()
    {
        var rotation = new Rotation();
        var original = rotation.AddlePvE;
        var errors = new List<Exception>();
        var scope = new RsrMitigationDutyScope(typeof(Center).GetProperty(nameof(Center.CurrentDutyRotation))!);
        using var lease = RsrMitigationSelectionLease.Acquire(rotation, nameof(Rotation.AddlePvE), 7560,
            _ => scope.NativeDefense, errors.Add);
        scope.Enter();
        try
        {
            Checks.Check(rotation.AddlePvE.CanUse(out var emergency) && ReferenceEquals(emergency, original),
                "earlier emergency selection retains original concrete action");
            Center.CurrentDutyRotation!.DefenseAreaAbility(null, out _);
            Checks.Check(!rotation.AddlePvE.CanUse(out var delayed) && delayed == null,
                "only native defense selection can yield a communicated early slot");
            IAction.ForceEnable = true;
            Checks.Check(rotation.AddlePvE.CanUse(out var forced) && ReferenceEquals(forced, original), "manual force bypasses mitigation handoff");
            IAction.ForceEnable = false;
            IAction.ActionPreview = true;
            Checks.Check(rotation.AddlePvE.CanUse(out _), "preview never consumes mitigation handoff");
            IAction.ActionPreview = false;
            Center.CurrentDutyRotation.GeneralAbility(null, out _);
            Checks.Check(rotation.AddlePvE.CanUse(out var later) && ReferenceEquals(later, original), "later priority selector cannot inherit defense suppression");
        }
        finally { scope.Exit(); }
        Checks.Check(Center.CurrentDutyRotation == null && errors.Count == 0, "callback scope restores original duty ownership");
        Center.CurrentDutyRotation = new SuccessfulDuty();
        scope.Enter();
        try
        {
            Checks.Check(Center.CurrentDutyRotation!.DefenseAreaAbility(null, out _) && !scope.NativeDefense,
                "authored duty defense retains priority and never enables job suppression");
        }
        finally { scope.Exit(); Center.CurrentDutyRotation = null; }
    }
    public interface IAction
    {
        static bool ActionPreview { get; set; }
        static bool ForceEnable { get; set; }
        bool CanUse(out IAction? action, bool a = false, bool b = false, bool c = false, bool d = false,
            bool e = false, bool f = false, bool g = false, bool h = false, bool i = false, bool j = false, bool k = false);
    }
    public sealed class Action : IAction
    {
        public uint AdjustedID => 7560;
        public bool CanUse(out IAction? action, bool a = false, bool b = false, bool c = false, bool d = false,
            bool e = false, bool f = false, bool g = false, bool h = false, bool i = false, bool j = false, bool k = false)
        { action = this; return true; }
    }
    public sealed class Rotation
    {
        private Lazy<IAction> _AddlePvECreator = new(() => new Action());
        public IAction AddlePvE => this._AddlePvECreator.Value;
    }
    public static class Center { public static Duty? CurrentDutyRotation { get; set; } }
    public class Duty
    {
        public virtual bool DefenseAreaAbility(object? gcd, out object? action) { action = null; return false; }
        public virtual bool GeneralAbility(object? gcd, out object? action) { action = null; return false; }
    }
    public sealed class SuccessfulDuty : Duty
    {
        public override bool DefenseAreaAbility(object? gcd, out object? action) { action = new(); return true; }
    }
}
