using System.Collections;
using System.Reflection;
using System.Text.Json;
using BossMod;
using Lumina.Excel.Sheets;

static class Inspection
{
    public static void Run(string gameDirectory, string inventoryPath)
    {
        using var game = new Lumina.GameData(Path.Combine(gameDirectory, "sqpack"),
            new Lumina.LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.English });
        Service.LuminaGameData = game;
        Service.Config.Initialize();
        using var input = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var territories = game.GetExcelSheet<TerritoryType>() ?? throw new InvalidDataException("TerritoryType sheet is missing.");
        var battles = game.GetExcelSheet<QuestBattle>() ?? throw new InvalidDataException("QuestBattle sheet is missing.");
        var quests = game.GetExcelSheet<Quest>() ?? throw new InvalidDataException("Quest sheet is missing.");
        // The registry has no public metadata lookup for zone modules.
        var registry = (IDictionary)typeof(ZoneModuleRegistry).GetField("_modulesByCFC", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        foreach (var zone in input.RootElement.EnumerateArray().GroupBy(r => r.GetProperty("zone").GetUInt32()))
        {
            if (!territories.TryGetRow(zone.Key, out var territory))
                throw new InvalidDataException($"TerritoryType row {zone.Key} is missing.");
            var battleFound = battles.TryGetRow(territory.QuestBattle.RowId, out var battle);
            var questId = battleFound ? battle.Quest.RowId : 0;
            // Some QuestBattle references do not identify a row in the installed Quest sheet.
            // Retain the raw ID and explicit absence rather than dereferencing it with GetRow.
            var questFound = quests.TryGetRow(questId, out var quest);
            var oids = zone.SelectMany(r => r.GetProperty("enemy_oids").EnumerateArray().Select(o => o.GetUInt32())).Distinct();
            var modules = oids.Select(BossModuleRegistry.FindByOID).Where(m => m != null)
                .Select(m => new { OID = m!.PrimaryActorOID, Module = m.ModuleType.FullName, Maturity = m.Maturity.ToString(), Group = m.GroupType.ToString(), m.GroupID }).ToArray();
            var zoneModule = registry[territory.ContentFinderCondition.RowId] as ZoneModuleRegistry.Info;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Zone = zone.Key, CFC = territory.ContentFinderCondition.RowId,
                QuestBattle = territory.QuestBattle.RowId, QuestBattleFound = battleFound,
                QuestID = questId, QuestFound = questFound, Quest = questFound ? quest.Name.ToString() : null,
                Sync = battleFound ? (int?)battle.LevelSync : null,
                Recordings = zone.Count(), Complete = zone.All(r => r.GetProperty("complete").GetBoolean()),
                ZoneModule = zoneModule?.ModuleType.FullName, Modules = modules
            }));
        }
    }
}
