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

    public async Task<OpenPlanetResult> PreviewManialinkXmlAsync(string xml, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        return await PostAsync("/manialink/preview", content, cancellationToken);
    }

    public async Task<OpenPlanetResult> ClearManialinkPreviewAsync(CancellationToken cancellationToken = default)
    {
        return await PostAsync("/manialink/clear", content: null, cancellationToken);
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
}

