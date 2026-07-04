using System.Text;
using Anchor.Config;
using Luban;

using BuffRow = Anchor.Config.game.buff;
using EventRow = Anchor.Config.game.gameEvent;

var root = FindProjectRoot(AppContext.BaseDirectory);
var dataDir = Path.Combine(root, "Assets", "Resources", "Config", "Luban", "Bin");
var errors = new List<string>();
var output = new StringBuilder();

output.AppendLine("[TableConfigValidator] Begin");
output.AppendLine($"DataDir: {dataDir}");

var tables = new Tables(LoadTableBytes);
ValidateEvents(tables.TbgameEvent.DataList, errors, output);
ValidateBuffs(tables.Tbbuff.DataList, errors, output);

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
        CheckCost("Event", row.Id, row.Cost, errors);
        CheckRange("Event", row.Id, "ratio", row.Ratio, 0f, 1f, errors);
        CheckFinite("Event", row.Id, "y1", row.Y1, errors);
        CheckFinite("Event", row.Id, "y2", row.Y2, errors);
        CheckFinite("Event", row.Id, "y3", row.Y3, errors);
        CheckFinite("Event", row.Id, "y4", row.Y4, errors);
        CheckFinite("Event", row.Id, "n1", row.N1, errors);
        CheckFinite("Event", row.Id, "n2", row.N2, errors);
        CheckFinite("Event", row.Id, "n3", row.N3, errors);
        CheckFinite("Event", row.Id, "n4", row.N4, errors);
        CheckFinite("Event", row.Id, "t1", row.T1, errors);
        CheckFinite("Event", row.Id, "t2", row.T2, errors);
        CheckFinite("Event", row.Id, "t3", row.T3, errors);
        CheckFinite("Event", row.Id, "t4", row.T4, errors);

        output.AppendLine(
            $"  Event[{row.Id}] title={row.Title}, type={row.Type}, ratio={row.Ratio}, cost={row.Cost}, y=({row.Y1},{row.Y2},{row.Y3},{row.Y4}), n=({row.N1},{row.N2},{row.N3},{row.N4}), t=({row.T1},{row.T2},{row.T3},{row.T4})");
    }
}

static void ValidateBuffs(
    IReadOnlyList<BuffRow> rows,
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
        CheckRequired("Buff", row.Id, "content", row.Content, errors);
        CheckCost("Buff", row.Id, row.Cost, errors);
        CheckFinite("Buff", row.Id, "c1", row.C1, errors);
        CheckFinite("Buff", row.Id, "c2", row.C2, errors);
        CheckFinite("Buff", row.Id, "c3", row.C3, errors);
        CheckFinite("Buff", row.Id, "c11", row.C11, errors);
        CheckFinite("Buff", row.Id, "t11", row.T11, errors);
        CheckFinite("Buff", row.Id, "c22", row.C22, errors);
        CheckFinite("Buff", row.Id, "t22", row.T22, errors);
        CheckFinite("Buff", row.Id, "c33", row.C33, errors);
        CheckFinite("Buff", row.Id, "t33", row.T33, errors);

        output.AppendLine(
            $"  Buff[{row.Id}] title={row.Title}, type={row.Type}, cost={row.Cost}, c=({row.C1},{row.C2},{row.C3},{row.C11},{row.C22},{row.C33}), t=({row.T11},{row.T22},{row.T33})");
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
