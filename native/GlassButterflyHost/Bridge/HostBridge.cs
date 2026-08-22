using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace GlassButterfly;

/// <summary>
/// The typed native bridge behind window.glass. It exposes only the capabilities
/// the renderer actually needs — read/write settings, pick an image, quit — via
/// postMessage. No arbitrary filesystem access is handed to JavaScript.
/// </summary>
internal sealed class HostBridge
{
    private readonly CoreWebView2 _core;
    private readonly SettingsStore _store;
    private readonly Action _quit;

    public HostBridge(CoreWebView2 core, SettingsStore store, Action quit)
    {
        _core = core;
        _store = store;
        _quit = quit;
        _core.WebMessageReceived += OnMessage;
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }
        if (string.IsNullOrEmpty(raw)) return;

        JsonElement req;
        try { req = JsonSerializer.Deserialize<JsonElement>(raw); }
        catch { return; }
        if (req.ValueKind != JsonValueKind.Object) return;

        string type = req.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        int id = req.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : 0;

        switch (type)
        {
            case "getSettings":
                Reply(id, _store.Current);
                break;

            case "saveSettings":
                JsonElement patch = req.TryGetProperty("payload", out var p) ? p : default;
                AppSettings saved = _store.Save(patch);
                Reply(id, saved);
                // Same-window live update (mirrors the Electron broadcast).
                _core.PostWebMessageAsString(_store.SerializeBroadcast(saved));
                break;

            case "selectBackgroundImage":
                Reply(id, PickImage());
                break;

            case "quit":
                _quit();
                break;
        }
    }

    private static string? PickImage()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Choose a background image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp",
            CheckFileExists = true
        };
        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }

    private void Reply(int id, object? result)
    {
        if (id == 0) return;
        _core.PostWebMessageAsString(_store.SerializeReply(id, result));
    }
}
