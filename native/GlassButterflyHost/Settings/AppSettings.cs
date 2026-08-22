namespace GlassButterfly;

/// <summary>
/// Native mirror of the renderer's Settings schema. Serialized as camelCase
/// JSON so it matches the renderer contract (backgroundImage, use24Hour,
/// showSeconds, butterflyCount).
/// </summary>
internal sealed class AppSettings
{
    public string BackgroundImage { get; set; } = DefaultBackground;
    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = false;
    public int ButterflyCount { get; set; } = 1;

    public const int MaxButterflies = 3;
    public const string BuiltinPrefix = "builtin:";
    public const string DefaultBackground = BuiltinPrefix + "rome";
    public static readonly string[] BuiltinIds = { "rome", "uwu", "her" };
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public AppSettings Clone() => new()
    {
        BackgroundImage = BackgroundImage,
        Use24Hour = Use24Hour,
        ShowSeconds = ShowSeconds,
        ButterflyCount = ButterflyCount
    };
}
