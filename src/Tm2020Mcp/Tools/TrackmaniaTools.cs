using System.ComponentModel;
using ModelContextProtocol.Server;
using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Tools;

[McpServerToolType]
public sealed class TrackmaniaTools
{
    private readonly OpenPlanetClient _client;

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
}

