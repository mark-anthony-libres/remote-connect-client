using System.Globalization;
using System.Reflection;
using System.Text;
using RemoteDesktopClient.Core.Configuration;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ReleaseConfigGenerator <path-to-.env.release> <path-to-AppSettingsFactory.cs>");
    return 1;
}

var envReleasePath = args[0];
var appSettingsFactoryPath = args[1];

if (!File.Exists(envReleasePath))
{
    Console.Error.WriteLine($"Missing {envReleasePath}.");
    return 1;
}

var envValues = ParseEnvFile(envReleasePath);

var properties = typeof(AppSettings)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .OrderBy(p => p.MetadataToken)
    .ToArray();

var errors = new List<string>();

var propertyKeys = new Dictionary<PropertyInfo, string>();
foreach (var property in properties)
{
    var envKey = property.GetCustomAttribute<EnvKeyAttribute>()?.Key;
    if (envKey is null)
        errors.Add($"AppSettings.{property.Name} has no [EnvKey] attribute — every property must declare one.");
    else
        propertyKeys[property] = envKey;
}

if (errors.Count > 0)
{
    PrintErrors(errors);
    return 1;
}

var knownKeys = propertyKeys.Values.ToHashSet(StringComparer.Ordinal);
foreach (var unknownKey in envValues.Keys.Where(k => !knownKeys.Contains(k)))
    errors.Add($"{envReleasePath} has key '{unknownKey}', which doesn't match any AppSettings property.");

foreach (var property in properties)
{
    if (!envValues.ContainsKey(propertyKeys[property]))
        errors.Add($"{envReleasePath} is missing '{propertyKeys[property]}' (AppSettings.{property.Name}).");
}

if (errors.Count > 0)
{
    PrintErrors(errors);
    return 1;
}

var initializers = new List<string>();
foreach (var property in properties)
{
    var rawValue = envValues[propertyKeys[property]];
    try
    {
        initializers.Add($"        {property.Name} = {FormatLiteral(property.PropertyType, rawValue)},");
    }
    catch (Exception ex) when (ex is FormatException or NotSupportedException)
    {
        errors.Add($"{envReleasePath}: {propertyKeys[property]} ('{rawValue}') -> AppSettings.{property.Name}: {ex.Message}");
    }
}

if (errors.Count > 0)
{
    PrintErrors(errors);
    return 1;
}

var generated = $$"""
namespace RemoteDesktopClient.Core.Configuration;

internal static class AppSettingsFactory
{
    public static AppSettings Load() => new()
    {
{{string.Join("\n", initializers)}}
    };
}
""";

File.WriteAllText(appSettingsFactoryPath, generated, new UTF8Encoding(false));
Console.WriteLine($"Generated {appSettingsFactoryPath} from {envReleasePath} ({properties.Length} propert{(properties.Length == 1 ? "y" : "ies")}: {string.Join(", ", properties.Select(p => p.Name))}).");
return 0;

static void PrintErrors(List<string> errors)
{
    Console.Error.WriteLine(errors.Count == 1 ? "Release configuration error:" : $"Release configuration errors ({errors.Count}):");
    foreach (var error in errors)
        Console.Error.WriteLine($"  - {error}");
}

static Dictionary<string, string> ParseEnvFile(string path)
{
    var values = new Dictionary<string, string>();
    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            continue;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
            continue;

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        values[key] = value;
    }
    return values;
}

static string FormatLiteral(Type type, string rawValue)
{
    if (type == typeof(string))
        return $"\"{rawValue.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    if (type == typeof(bool))
        return bool.TryParse(rawValue, out var boolValue)
            ? (boolValue ? "true" : "false")
            : throw new FormatException($"'{rawValue}' is not a valid bool (expected true/false).");

    if (type == typeof(int))
        return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
            ? intValue.ToString(CultureInfo.InvariantCulture)
            : throw new FormatException($"'{rawValue}' is not a valid int.");

    if (type == typeof(long))
        return long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue)
            ? longValue.ToString(CultureInfo.InvariantCulture) + "L"
            : throw new FormatException($"'{rawValue}' is not a valid long.");

    if (type == typeof(double))
        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
            ? doubleValue.ToString(CultureInfo.InvariantCulture) + "d"
            : throw new FormatException($"'{rawValue}' is not a valid double.");

    throw new NotSupportedException(
        $"AppSettings property type '{type.FullName}' isn't supported by the release generator. Supported: string, int, long, double, bool.");
}
