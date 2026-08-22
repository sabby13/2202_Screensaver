using System.Text.Json;

namespace GlassButterfly;

/// <summary>
/// Persists settings to %APPDATA%\GlassButterfly\settings.json, the only place
/// the native host touches the filesystem for settings. Handles missing and
/// corrupt files, clamps the butterfly count, and — per spec — falls back a
/// missing custom wallpaper to builtin:rome.
/// </summary>
internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    // Single-line JSON for anything injected into the page or sent over the
    // postMessage bridge. MUST stay compact: the bridge template embeds the
    // initial settings inline (both in a // comment and in `var initial = ...;`),
    // so multi-line JSON would break out of the comment / statement and throw a
    // SyntaxError, leaving window.glass undefined and the page blank.
    private static readonly JsonSerializerOptions JsonCompact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly string _path;

    public AppSettings Current { get; private set; }

    public SettingsStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassButterfly");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                var fresh = new AppSettings();
                Persist(fresh);
                return fresh;
            }

            var parsed = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Json)
                         ?? new AppSettings();
            return Sanitize(parsed);
        }
        catch
        {
            // Missing/corrupt/unreadable — reset to a known-good state.
            var fallback = new AppSettings();
            try { Persist(fallback); }
            catch { /* ignore write failures */ }
            return fallback;
        }
    }

    private static AppSettings Sanitize(AppSettings s)
    {
        if (s.ButterflyCount < 0) s.ButterflyCount = 0;
        if (s.ButterflyCount > AppSettings.MaxButterflies) s.ButterflyCount = AppSettings.MaxButterflies;

        string bg = s.BackgroundImage ?? AppSettings.DefaultBackground;
        if (bg.StartsWith(AppSettings.BuiltinPrefix, StringComparison.Ordinal))
        {
            string id = bg[AppSettings.BuiltinPrefix.Length..];
            if (Array.IndexOf(AppSettings.BuiltinIds, id) < 0)
                bg = AppSettings.DefaultBackground;
        }
        else if (string.IsNullOrWhiteSpace(bg) || !File.Exists(bg))
        {
            // A custom wallpaper that no longer exists falls back to the default.
            bg = AppSettings.DefaultBackground;
        }
        s.BackgroundImage = bg;
        return s;
    }

    /// <summary>Merge a partial patch onto the current settings and persist.</summary>
    public AppSettings Save(JsonElement patch)
    {
        AppSettings next = Current.Clone();
        if (patch.ValueKind == JsonValueKind.Object)
        {
            if (patch.TryGetProperty("backgroundImage", out var b) && b.ValueKind == JsonValueKind.String)
                next.BackgroundImage = b.GetString() ?? next.BackgroundImage;
            if (patch.TryGetProperty("use24Hour", out var u) &&
                (u.ValueKind == JsonValueKind.True || u.ValueKind == JsonValueKind.False))
                next.Use24Hour = u.GetBoolean();
            if (patch.TryGetProperty("showSeconds", out var sec) &&
                (sec.ValueKind == JsonValueKind.True || sec.ValueKind == JsonValueKind.False))
                next.ShowSeconds = sec.GetBoolean();
            if (patch.TryGetProperty("butterflyCount", out var c) && c.ValueKind == JsonValueKind.Number
                && c.TryGetInt32(out int count))
                next.ButterflyCount = count;
        }

        Current = Sanitize(next);
        try { Persist(Current); }
        catch { /* best-effort; keep in-memory value */ }
        return Current;
    }

    private void Persist(AppSettings s) => File.WriteAllText(_path, JsonSerializer.Serialize(s, Json));

    // All three feed the renderer (page injection or postMessage), never disk,
    // so they use the compact options.
    public string Serialize(AppSettings s) => JsonSerializer.Serialize(s, JsonCompact);

    public string SerializeReply(int id, object? result) =>
        JsonSerializer.Serialize(new { id, result }, JsonCompact);

    public string SerializeBroadcast(AppSettings s) =>
        JsonSerializer.Serialize(new { type = "settingsChanged", payload = s }, JsonCompact);
}
