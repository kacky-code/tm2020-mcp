using System.ComponentModel;
using ModelContextProtocol.Server;
using Tm2020Mcp.EmojiChat;
using Tm2020Mcp.EditorBridge;
using Tm2020Mcp.Manialinks;

namespace Tm2020Mcp.Tools;

[McpServerToolType]
public sealed class TrackmaniaTools
{
    private readonly OpenPlanetClient _client;
    private readonly EmojiChatAnalyzer _emojiChat = new();
    private readonly ManialinkInspector _manialinks = new();
    private readonly ManialinkVideoProbeBuilder _videoProbe = new();
    private readonly ManialinkValidator _validator = new();
    private readonly ManialinkMediaProbe _mediaProbe = new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });

    public TrackmaniaTools(OpenPlanetClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "set_openplanet_bridge_url"), Description("Configure the TM2020 OpenPlanet bridge base URL. Defaults to http://127.0.0.1:29100.")]
    public string SetOpenPlanetBridgeUrl(
        [Description("Bridge base URL, for example http://127.0.0.1:29100.")] string url)
    {
        try
        {
            _client.SetBaseUrl(url);
            return $"OpenPlanet bridge URL set to: {url.Trim().TrimEnd('/')}";
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_tm2020_status"), Description("Return current TM2020 OpenPlanet bridge and editor status.")]
    public async Task<string> GetTm2020Status()
    {
        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that Trackmania 2020 is running and the TM2020Bridge plugin is loaded.";

        return $"running={status.Running}, editor_open={status.EditorOpen}, map_editor={status.MapEditor}, interface_designer={status.InterfaceDesigner}, module_editor={status.ModuleEditor}, manialink_preview={status.ManialinkPreview}";
    }

    [McpServerTool(Name = "preview_manialink_xml"), Description("Push raw ManiaLink XML into TM2020 through the OpenPlanet TM2020Bridge plugin.")]
    public async Task<string> PreviewManialinkXml(
        [Description("Full ManiaLink XML.")] string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "Error: XML is empty.";

        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that TM2020Bridge is loaded.";

        if (!status.MapEditor)
            return "OpenPlanet bridge is running, but the map editor is not active. ManiaLink preview currently targets the map editor PluginMapType.ManialinkText path.";

        var result = await _client.PreviewManialinkXmlAsync(xml);
        return result.Success
            ? $"OpenPlanet: ManiaLink preview updated ({xml.Length} chars).\n{result.Body}"
            : $"OpenPlanet: ManiaLink preview failed.\n{result.Body}";
    }

    [McpServerTool(Name = "preview_manialink_file"), Description("Read a ManiaLink XML file from disk and push it into TM2020.")]
    public async Task<string> PreviewManialinkFile(
        [Description("Absolute path to a .xml file.")] string path)
    {
        if (!File.Exists(path))
            return $"Error: File does not exist: {path}";

        var xml = await File.ReadAllTextAsync(path);
        return await PreviewManialinkXml(xml);
    }

    [McpServerTool(Name = "clear_manialink_preview"), Description("Clear the current TM2020 ManiaLink XML preview.")]
    public async Task<string> ClearManialinkPreview()
    {
        var result = await _client.ClearManialinkPreviewAsync();
        return result.Success
            ? $"OpenPlanet: ManiaLink preview cleared.\n{result.Body}"
            : $"OpenPlanet: Failed to clear ManiaLink preview.\n{result.Body}";
    }

    [McpServerTool(Name = "autosave_map_editor"), Description("Trigger AutoSave in the current TM2020 map editor via OpenPlanet.")]
    public async Task<string> AutosaveMapEditor()
    {
        var result = await _client.AutosaveMapEditorAsync();
        return result.Success
            ? $"OpenPlanet: map editor autosave triggered.\n{result.Body}"
            : $"OpenPlanet: autosave failed.\n{result.Body}";
    }

    [McpServerTool(Name = "get_recent_manialink_events"), Description("Return recent ManiaLink event payloads recorded by the OpenPlanet bridge.")]
    public async Task<string> GetRecentManialinkEvents()
    {
        var events = await _client.GetRecentManialinkEventsAsync();
        if (events is null)
            return "OpenPlanet bridge not reachable or event endpoint unavailable.";

        if (events.Count == 0)
            return "No ManiaLink events recorded.";

        return string.Join(
            "\n",
            events.Select(e => $"[{e.Index}] {e.Body}"));
    }

    [McpServerTool(Name = "record_manialink_event"), Description("Record a ManiaLink event payload in the OpenPlanet bridge event buffer. Useful for probe/debug flows.")]
    public async Task<string> RecordManialinkEvent(
        [Description("Event payload, usually JSON with control id/action/source fields.")] string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Error: event body is empty.";

        var result = await _client.RecordManialinkEventAsync(body);
        return result.Success
            ? $"OpenPlanet: ManiaLink event recorded.\n{result.Body}"
            : $"OpenPlanet: failed to record ManiaLink event.\n{result.Body}";
    }

    [McpServerTool(Name = "clear_manialink_events"), Description("Clear the OpenPlanet bridge ManiaLink event buffer.")]
    public async Task<string> ClearManialinkEvents()
    {
        var result = await _client.ClearManialinkEventsAsync();
        return result.Success
            ? $"OpenPlanet: ManiaLink event buffer cleared.\n{result.Body}"
            : $"OpenPlanet: failed to clear ManiaLink event buffer.\n{result.Body}";
    }

    [McpServerTool(Name = "inspect_manialink_interactions"), Description("Inspect ManiaLink XML for interactive label/quad controls with action, scriptaction, or scriptevents.")]
    public string InspectManialinkInteractions(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml)
    {
        return _manialinks.InspectInteractiveControls(xml);
    }

    [McpServerTool(Name = "validate_manialink_xml"), Description("Check ManiaLink XML against Trackmania 2020 constraints: element names, media formats the client can actually decode, the 320x180 coordinate space, script-event wiring, duplicate ids, and Interface Designer paste-safety. Run this before pushing XML into the game.")]
    public string ValidateManialinkXml(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml,
        [Description("Where the XML is going: \"manialink\" for a document pushed to the game or served as HUD, \"designer\" for a fragment pasted into the in-game Interface Designer.")] string target = "manialink")
    {
        var parsed = target.Trim().ToLowerInvariant() switch
        {
            "designer" or "interfacedesigner" or "interface-designer" => ManialinkTarget.InterfaceDesigner,
            "manialink" or "" => ManialinkTarget.Manialink,
            _ => (ManialinkTarget?)null
        };

        if (parsed is null)
            return $"Unknown target '{target}'. Use 'manialink' or 'designer'.";

        return ManialinkValidator.Format(_validator.Validate(xml, parsed.Value));
    }

    [McpServerTool(Name = "validate_manialink_file"), Description("Read a ManiaLink XML file from disk and validate it against Trackmania 2020 constraints.")]
    public string ValidateManialinkFile(
        [Description("Absolute path to a .xml file.")] string path,
        [Description("Where the XML is going: \"manialink\" or \"designer\".")] string target = "manialink")
    {
        if (!File.Exists(path))
            return $"File not found: {path}";

        return ValidateManialinkXml(File.ReadAllText(path), target);
    }

    [McpServerTool(Name = "check_manialink_media"), Description("Fetch every http(s) image, video and audio URL in ManiaLink XML and report the ones the game will silently fail to render: dead URLs, non-200 responses, web pages served where a media file was expected, and animated WebP confirmed from its header. Needs no running game.")]
    public async Task<string> CheckManialinkMedia(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml)
    {
        return ManialinkValidator.Format(await _mediaProbe.ProbeAsync(xml));
    }

    [McpServerTool(Name = "analyze_emoji_chat_message"), Description("Analyze a Kacky EmojiChat message for emoji shortcodes, Trackmania format codes, unknown emoji, and ManiaLink-safe text.")]
    public string AnalyzeEmojiChatMessage(
        [Description("Raw chat message.")] string message,
        [Description("Optional comma-separated known emoji names to merge with defaults.")] string? knownEmojiNames = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: message is empty.";

        var analysis = _emojiChat.Analyze(message, knownEmojiNames);
        return $"""
            Original: {analysis.Original}
            Plain text: {analysis.PlainText}
            Emoji tokens: {FormatList(analysis.EmojiTokens)}
            Unknown emoji: {FormatList(analysis.UnknownEmoji)}
            Trackmania format codes: {FormatList(analysis.TrackmaniaFormatCodes)}
            ManiaLink-safe text: {analysis.ManialinkSafeText}
            """;
    }

    [McpServerTool(Name = "build_emoji_chat_preview_xml"), Description("Build a small paste-safe ManiaLink fragment to preview one EmojiChat message.")]
    public string BuildEmojiChatPreviewXml(
        [Description("Raw chat message.")] string message,
        [Description("Optional comma-separated known emoji names to merge with defaults.")] string? knownEmojiNames = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: message is empty.";

        return _emojiChat.BuildLabelPreviewXml(message, knownEmojiNames);
    }

    [McpServerTool(Name = "build_manialink_video_probe_xml"), Description("Build a small ManiaLink XML document with a video tag for GPS/video experiments.")]
    public string BuildManialinkVideoProbeXml(
        [Description("Video data path or URL, for example file://Media/Videos/gps.webm.")] string data,
        [Description("Whether to route the video as music/audio.")] bool music = true,
        [Description("Whether playback starts immediately.")] bool play = true,
        [Description("Whether the video element is hidden.")] bool hidden = false)
    {
        try
        {
            return _videoProbe.Build(data, music, play, hidden);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values);
    }
}
