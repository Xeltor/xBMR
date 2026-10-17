using System;
using System.Text.Json;

namespace IntegrationExample;

// Invoke synchronously inside a native stage callback. The caller first checks the
// independently versioned directional-angle-scopes capability and owns a live token.
internal static class BossModV1DirectionalScope
{
    internal static bool TryRequest(JsonElement context, string ownerToken, uint actionId, string settingKey,
        Func<string, string> getSettings, Func<string, Func<bool>, string> withDirectionalAngle,
        Func<bool> requestAlreadySafetyCheckedAction)
    {
        using var settings = JsonDocument.Parse(getSettings(context.GetRawText()));
        var root = settings.RootElement;
        if (!root.TryGetProperty("available", out var available) || !available.GetBoolean())
            return false;
        foreach (var setting in root.GetProperty("settings").EnumerateArray())
        {
            if (setting.GetProperty("key").GetString() != settingKey || !setting.GetProperty("synchronousOnly").GetBoolean()
                || setting.GetProperty("leased").GetBoolean())
                continue;
            var request = JsonSerializer.Serialize(new
            {
                context, ownerToken, actionId,
                expectedRevision = setting.GetProperty("revision").GetInt64(),
                expectedValue = setting.GetProperty("value").GetString()
            });
            using var result = JsonDocument.Parse(withDirectionalAngle(request, requestAlreadySafetyCheckedAction));
            var response = result.RootElement;
            return response.TryGetProperty("accepted", out var accepted) && accepted.GetBoolean()
                && response.TryGetProperty("releaseComplete", out var released) && released.GetBoolean()
                && response.TryGetProperty("actionRequested", out var requested) && requested.GetBoolean();
        }
        return false;
    }
}
