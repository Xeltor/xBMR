namespace BossMod.Integration;

internal sealed class IntegrationModuleEnablement(BossModuleConfig config, uint oid, Func<bool> live) : IntegrationLeases.IResource
{
    public string Key => $"module:{oid:X}";
    public int MaxLeaseMs => 2000;
    public IntegrationLeases.State Read()
    {
        var state = config.ModuleEnablementOwners.Read(oid);
        return state == null ? new("unavailable", -1, null) : new(state.Enabled ? "true" : "false", state.Revision, state.LeaseId);
    }

    public IntegrationLeases.Write Apply(string leaseId, IntegrationLeases.State expected, string value)
    {
        var state = Read();
        if (value is not ("true" or "false") || state.Revision != expected.Revision || state.Value != expected.Value)
            return new(false, "changed-or-invalid-module-enablement", state.Revision);
        var accepted = config.ModuleEnablementOwners.Apply(oid, leaseId, state.Revision, value == "true", live);
        var after = Read();
        return new(accepted && after.LeaseId == leaseId, accepted ? "applied" : "module-ownership-changed", after.Revision);
    }

    public bool Renew(string leaseId, long revision, int leaseMs) => config.ModuleEnablementOwners.Renew(oid, leaseId, revision, leaseMs);
    public bool Release(string leaseId, long revision) => config.ModuleEnablementOwners.Release(oid, leaseId, revision);
}
