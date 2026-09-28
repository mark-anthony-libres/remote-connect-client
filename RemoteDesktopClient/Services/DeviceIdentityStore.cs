using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteDesktopClient.Services;

internal static class DeviceIdentityStore
{
    private static readonly string DeviceIdentityFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteConnect", "device.json");

    public static string? LoadInstallKey()
    {
        try
        {
            if (!File.Exists(DeviceIdentityFilePath))
                return null;
            var savedState = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(DeviceIdentityFilePath));
            return savedState?.InstallKey;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceIdentityStore: failed to read {DeviceIdentityFilePath}, treating as first connect. {ex}");
            return null;
        }
    }

    public static void SaveInstallKey(string installKey)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DeviceIdentityFilePath)!);
            File.WriteAllText(DeviceIdentityFilePath, JsonSerializer.Serialize(new PersistedState(installKey)));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceIdentityStore: failed to save install key to {DeviceIdentityFilePath}. {ex}");
        }
    }

    public static void ClearInstallKey()
    {
        try
        {
            File.Delete(DeviceIdentityFilePath);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeviceIdentityStore: failed to delete {DeviceIdentityFilePath}. {ex}");
        }
    }

    private sealed record PersistedState([property: JsonPropertyName("install_key")] string InstallKey);
}
