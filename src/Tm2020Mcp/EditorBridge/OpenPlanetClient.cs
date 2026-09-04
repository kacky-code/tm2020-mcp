using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tm2020Mcp.EditorBridge;

public sealed class OpenPlanetClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private string _baseUrl;

    public OpenPlanetClient()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, "http://127.0.0.1:29100")
    {
    }

    public OpenPlanetClient(HttpClient httpClient, string baseUrl)
    {
        _http = httpClient;
        _baseUrl = NormalizeBaseUrl(baseUrl);
    }

    public void SetBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Base URL cannot be empty.", nameof(url));

        _baseUrl = NormalizeBaseUrl(url);
    }

    public async Task<OpenPlanetStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/status", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<OpenPlanetStatusDto>(JsonOptions, cancellationToken) is { } dto
                ? dto.ToStatus()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<OpenPlanetResult> AutosaveMapEditorAsync(CancellationToken cancellationToken = default)
    {
        return await PostAsync("/save", content: null, cancellationToken);
    }

    public async Task<OpenPlanetResult> CreateMapAsync(NewMapRequest request, CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(request, options: JsonOptions);
        return await PostAsync("/map/new", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> PlaceMapBlocksAsync(IReadOnlyList<MapBlockPlacement> blocks, CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new MapBlocksRequest(blocks), options: JsonOptions);
        return await PostAsync("/map/blocks", content, cancellationToken);
    }

    /// <param name="probe">
    /// Ask the bridge to read engine state off a block handle it has just deleted. That is a
    /// deliberate experiment, not a default: reading a freed handle is exactly the question
    /// being asked, and it could take the client down.
    /// </param>
    public async Task<OpenPlanetResult> RemoveMapBlocksAsync(
        IReadOnlyList<MapBlockRemoval> blocks,
        bool probe = false,
        CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new RemoveBlocksRequest(blocks, probe), options: JsonOptions);
        return await PostAsync("/map/blocks/remove", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> SaveMapAsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(new SaveMapRequest(fileName), options: JsonOptions);
        return await PostAsync("/map/save", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> PreviewManialinkXmlAsync(string xml, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return await PostAsync("/manialink/preview", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> ClearManialinkPreviewAsync(CancellationToken cancellationToken = default)
    {
        return await PostAsync("/manialink/clear", content: null, cancellationToken);
    }

    public async Task<IReadOnlyList<ManialinkEvent>?> GetRecentManialinkEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/manialink/events", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ManialinkEventsDto>(JsonOptions, cancellationToken) is { } dto
                ? dto.Events.Select(e => new ManialinkEvent(e.Index, e.Body)).ToArray()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lists the server-sent HUD layers on the local client.
    /// </summary>
    /// <remarks>
    /// Reads the player's own client, so it needs Trackmania running, connected to a server,
    /// with the bridge plugin loaded. A layer's XML is what a Nadeo UI module actually renders,
    /// which is otherwise unreadable because the module scripts are not published.
    /// </remarks>
    public async Task<UiLayerList?> GetUiLayersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/layers", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<UiLayerListDto>(JsonOptions, cancellationToken);
            if (dto is null)
                return null;

            var layers = (dto.Layers ?? [])
                .Select(layer => new UiLayer(
                    layer.Index,
                    layer.AttachId ?? string.Empty,
                    layer.Type ?? string.Empty,
                    layer.Visible,
                    layer.AnimInProgress,
                    layer.ScriptRunning,
                    layer.XmlLength,
                    layer.Tag ?? string.Empty))
                .ToList();

            return new UiLayerList(dto.Connected, layers, dto.Error);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns one layer's ManiaLink XML, or null when there is no layer at that index.
    /// </summary>
    public async Task<string?> GetUiLayerXmlAsync(int index, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/layers/{index}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<OpenPlanetResult> RecordManialinkEventAsync(string body, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await PostAsync("/manialink/events", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> ClearManialinkEventsAsync(CancellationToken cancellationToken = default)
    {
        return await PostAsync("/manialink/events/clear", content: null, cancellationToken);
    }

    private async Task<OpenPlanetResult> PostAsync(string path, HttpContent? content, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.PostAsync($"{_baseUrl}{path}", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new OpenPlanetResult(response.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            return new OpenPlanetResult(false, ex.Message);
        }
    }

    private static string NormalizeBaseUrl(string url) => url.Trim().TrimEnd('/');

    private sealed record UiLayerListDto(
        [property: JsonPropertyName("connected")] bool Connected,
        [property: JsonPropertyName("layers")] List<UiLayerDto>? Layers,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record UiLayerDto(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("attachId")] string? AttachId,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("visible")] bool Visible,
        [property: JsonPropertyName("animInProgress")] bool AnimInProgress,
        [property: JsonPropertyName("scriptRunning")] bool ScriptRunning,
        [property: JsonPropertyName("xmlLength")] int XmlLength,
        [property: JsonPropertyName("tag")] string? Tag);

    private sealed record OpenPlanetStatusDto(
        [property: JsonPropertyName("running")] bool Running,
        [property: JsonPropertyName("editor_open")] bool EditorOpen,
        [property: JsonPropertyName("map_editor")] bool MapEditor,
        [property: JsonPropertyName("interface_designer")] bool InterfaceDesigner,
        [property: JsonPropertyName("module_editor")] bool ModuleEditor,
        [property: JsonPropertyName("manialink_preview")] bool ManialinkPreview)
    {
        public OpenPlanetStatus ToStatus()
        {
            return new OpenPlanetStatus(
                Running,
                EditorOpen,
                MapEditor,
                InterfaceDesigner,
                ModuleEditor,
                ManialinkPreview);
        }
    }

    private sealed record MapBlocksRequest(
        [property: JsonPropertyName("blocks")] IReadOnlyList<MapBlockPlacement> Blocks);

    private sealed record RemoveBlocksRequest(
        [property: JsonPropertyName("blocks")] IReadOnlyList<MapBlockRemoval> Blocks,
        [property: JsonPropertyName("probe")] bool Probe);

    private sealed record SaveMapRequest(
        [property: JsonPropertyName("file_name")] string FileName);

    private sealed record ManialinkEventsDto(
        [property: JsonPropertyName("events")] IReadOnlyList<ManialinkEventDto> Events);

    private sealed record ManialinkEventDto(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("body")] string Body);
}
