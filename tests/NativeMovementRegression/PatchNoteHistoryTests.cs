using BossMod.CombatAssistance.Runtime;

internal static class PatchNoteHistoryTests
{
    public static void MissedUpdates()
    {
        PatchNoteEntry[] entries = [new("first", "2.50.0", ["First"]), new("second", "2.51.0", ["Second"]), new("third", "2.52.0", ["Third"])];
        Checks.Check(PatchNoteHistory.Pending(entries, null).Single().Id == "third", "existing users see the introducing patch only");
        var initial = PatchNoteHistory.Acknowledge(entries, null, [entries[2]]);
        Checks.Check(PatchNoteHistory.Pending(entries, initial).Length == 0, "initial dismissal establishes the baseline");
        Checks.Check(PatchNoteHistory.Pending(entries, ["first"]).Select(e => e.Id).SequenceEqual(["second", "third"]), "skipping updates shows every missed patch");
        var acknowledged = PatchNoteHistory.Acknowledge(entries, ["first"], [entries[1]]);
        Checks.Check(PatchNoteHistory.Pending(entries, acknowledged).Single().Id == "third", "only displayed entries are acknowledged");
        var promoted = entries.Select(e => e with { Version = "2.53.0" }).ToArray();
        Checks.Check(PatchNoteHistory.Pending(promoted, initial).Length == 0, "testing promotion with the same IDs does not repeat notes");
        PatchNoteEntry[] backfilled = [new("old", "2.22.0", ["Old"], true), new("recent", "2.49.0", ["Recent"], true), new("intro", "Development build", ["Intro"])];
        Checks.Check(PatchNoteHistory.Pending(backfilled, ["intro"], "2.49.0").Length == 0, "backfilled history is not new for current users");
        Checks.Check(PatchNoteHistory.Pending(backfilled, ["intro"], "2.22.0").Single().Id == "recent", "users with an older recorded baseline see only newer backfilled releases");
        Checks.Check(PatchNoteHistory.Pending(backfilled, ["intro"]).Length == 0, "unknown history does not advertise backfilled notes as new");
        Checks.Check(PatchNoteHistory.Load().All(e => e.Version != "current"), "imported history keeps its original version labels");
        Checks.Check(PatchNoteHistory.Load().Length > 0, "notes ship inside the assembly");
    }
}
