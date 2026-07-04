using System.Text;
using Anchor.Config;
using Luban;

using BuffRow = Anchor.Config.game.buff;
using EventRow = Anchor.Config.game.gameEvent;
using PlayerAttributeRow = Anchor.Config.game.playerAttribute;

const int DynamicQualityAttributeId = 1010;
const int CurrentMonthWeekIndexAttributeId = 1051;
const int TotalWeekIndexAttributeId = 1052;

var root = FindProjectRoot(AppContext.BaseDirectory);
var dataDir = Path.Combine(root, "Assets", "Resources", "Config", "Luban", "Bin");
var errors = new List<string>();
var output = new StringBuilder();

output.AppendLine("[TableConfigValidator] Begin");
output.AppendLine($"DataDir: {dataDir}");

var tables = new Tables(LoadTableBytes);
var playerAttributeIds = ValidatePlayerAttributes(tables.TbplayerAttribute.DataList, errors, output);
var writableAttributeIds = new HashSet<int>(playerAttributeIds);
writableAttributeIds.Remove(CurrentMonthWeekIndexAttributeId);
writableAttributeIds.Remove(TotalWeekIndexAttributeId);
var readableAttributeIds = new HashSet<int>(playerAttributeIds)
{
    DynamicQualityAttributeId,
    // Flow progress mirrors are valid for event trigger conditions, but not writable effects.
    CurrentMonthWeekIndexAttributeId,
    TotalWeekIndexAttributeId
};
ValidateEvents(tables.TbgameEvent.DataList, writableAttributeIds, readableAttributeIds, errors, output);
ValidateBuffs(tables.Tbbuff.DataList, writableAttributeIds, errors, output);

if (errors.Count > 0)
{
    output.AppendLine("[TableConfigValidator] FAIL");
    Console.WriteLine(output.ToString());
    foreach (var error in errors)
        Console.Error.WriteLine("[TableConfigValidator] " + error);
    return 1;
}

output.AppendLine("[TableConfigValidator] PASS");
Console.WriteLine(output.ToString());
return 0;

ByteBuf LoadTableBytes(string fileName)
{
    var path = Path.Combine(dataDir, fileName + ".bytes");
    if (!File.Exists(path))
        throw new FileNotFoundException("Missing Luban data file.", path);

    var bytes = File.ReadAllBytes(path);
    if (bytes.Length == 0)
        throw new InvalidOperationException($"Luban data file is empty: {path}");

    return new ByteBuf(bytes);
}

static void ValidateEvents(
    IReadOnlyList<EventRow> rows,
    ISet<int> writableAttributeIds,
    ISet<int> readableAttributeIds,
    ICollection<string> errors,
    StringBuilder output)
{
    output.AppendLine($"Events: {rows.Count}");
    if (rows.Count == 0)
        errors.Add("game.TbgameEvent has no data rows.");

    var ids = new HashSet<int>();
    foreach (var row in rows)
    {
        CheckId("Event", row.Id, ids, errors);
        CheckRequired("Event", row.Id, "title", row.Title, errors);
        CheckRequired("Event", row.Id, "content", row.Content, errors);
        CheckRange("Event", row.Id, "ratio", row.Ratio, 0f, 1f, errors);
        CheckAttributePairs("Event", row.Id, "yesEffects", row.YesEffects, writableAttributeIds, "writable player attribute", errors);
        CheckAttributePairs("Event", row.Id, "noEffects", row.NoEffects, writableAttributeIds, "writable player attribute", errors);
        CheckAttributePairs("Event", row.Id, "triggerGreaterOrEqualConditions", row.TriggerGreaterOrEqualConditions, readableAttributeIds, "readable attribute", errors);
        CheckAttributePairs("Event", row.Id, "triggerLessThanConditions", row.TriggerLessThanConditions, readableAttributeIds, "readable attribute", errors);

        output.AppendLine(
            $"  Event[{row.Id}] title={row.Title}, ratio={row.Ratio}, yes={FormatPairs(row.YesEffects)}, no={FormatPairs(row.NoEffects)}, triggerGe={FormatPairs(row.TriggerGreaterOrEqualConditions)}, triggerLt={FormatPairs(row.TriggerLessThanConditions)}");
    }
}

static HashSet<int> ValidatePlayerAttributes(
    IReadOnlyList<PlayerAttributeRow> rows,
    ICollection<string> errors,
    StringBuilder output)
{
    output.AppendLine($"PlayerAttributes: {rows.Count}");
    if (rows.Count == 0)
        errors.Add("game.TbplayerAttribute has no data rows.");

    var ids = new HashSet<int>();
    foreach (var row in rows)
    {
        CheckId("PlayerAttribute", row.Id, ids, errors);
        CheckRequired("PlayerAttribute", row.Id, "displayName", row.DisplayName, errors);

        output.AppendLine(
            $"  PlayerAttribute[{row.Id}] displayName={row.DisplayName}, defaultValue={row.DefaultValue}");
    }

    return ids;
}

static void ValidateBuffs(
    IReadOnlyList<BuffRow> rows,
    ISet<int> playerAttributeIds,
    ICollection<string> errors,
    StringBuilder output)
{
    output.AppendLine($"Buffs: {rows.Count}");
    if (rows.Count == 0)
        errors.Add("game.Tbbuff has no data rows.");

    var ids = new HashSet<int>();
    foreach (var row in rows)
    {
        CheckId("Buff", row.Id, ids, errors);
        CheckRequired("Buff", row.Id, "title", row.Title, errors);
        CheckRequired("Buff", row.Id, "brief", row.Brief, errors);
        CheckRequired("Buff", row.Id, "content", row.Content, errors);
        CheckCost("Buff", row.Id, row.Cost, errors);
        CheckWeight("Buff", row.Id, row.Weight, errors);
        CheckAttributePairs("Buff", row.Id, "effects", row.Effects, playerAttributeIds, "writable player attribute", errors);

        output.AppendLine(
            $"  Buff[{row.Id}] title={row.Title}, brief={row.Brief}, cost={row.Cost}, weight={row.Weight}, effects={FormatPairs(row.Effects)}");
    }
}

static void CheckId(string table, int id, ISet<int> ids, ICollection<string> errors)
{
    if (id <= 0)
        errors.Add($"{table} has invalid id: {id}.");

    if (!ids.Add(id))
        errors.Add($"{table} has duplicate id: {id}.");
}

static void CheckRequired(
    string table,
    int id,
    string field,
    string value,
    ICollection<string> errors)
{
    if (string.IsNullOrWhiteSpace(value))
        errors.Add($"{table}[{id}] field '{field}' is empty.");
}

static void CheckCost(string table, int id, int cost, ICollection<string> errors)
{
    if (cost < 0)
        errors.Add($"{table}[{id}] cost is negative: {cost}.");
}

static void CheckWeight(string table, int id, int weight, ICollection<string> errors)
{
    if (weight < 0)
        errors.Add($"{table}[{id}] weight is negative: {weight}.");
}

static void CheckAttributePairs(
    string table,
    int id,
    string field,
    int[][] pairs,
    ISet<int> validAttributeIds,
    string attributeKind,
    ICollection<string> errors)
{
    if (pairs == null)
    {
        errors.Add($"{table}[{id}] field '{field}' is null.");
        return;
    }

    for (var i = 0; i < pairs.Length; i++)
    {
        var pair = pairs[i];
        if (pair == null || pair.Length != 2)
        {
            errors.Add($"{table}[{id}] field '{field}' item {i} must be [attributeId, value].");
            continue;
        }

        if (!validAttributeIds.Contains(pair[0]))
        {
            errors.Add($"{table}[{id}] field '{field}' item {i} uses unknown {attributeKind} id: {pair[0]}.");
        }
    }
}

static string FormatPairs(int[][] pairs)
{
    if (pairs == null || pairs.Length == 0)
    {
        return "[]";
    }

    var parts = new string[pairs.Length];
    for (var i = 0; i < pairs.Length; i++)
    {
        parts[i] = pairs[i] == null ? "null" : "[" + string.Join(",", pairs[i]) + "]";
    }

    return "[" + string.Join(",", parts) + "]";
}

static void CheckRange(
    string table,
    int id,
    string field,
    float value,
    float min,
    float max,
    ICollection<string> errors)
{
    CheckFinite(table, id, field, value, errors);
    if (value < min || value > max)
        errors.Add($"{table}[{id}] field '{field}' is out of range [{min}, {max}]: {value}.");
}

static void CheckFinite(string table, int id, string field, float value, ICollection<string> errors)
{
    if (float.IsNaN(value) || float.IsInfinity(value))
        errors.Add($"{table}[{id}] field '{field}' is not finite: {value}.");
}

static string FindProjectRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);
    while (current != null)
    {
        var assetsDir = Path.Combine(current.FullName, "Assets");
        var configDir = Path.Combine(current.FullName, "Config");
        if (Directory.Exists(assetsDir) && Directory.Exists(configDir))
            return current.FullName;

        current = current.Parent;
    }

    throw new DirectoryNotFoundException($"Could not locate Unity project root from: {startDirectory}");
}
