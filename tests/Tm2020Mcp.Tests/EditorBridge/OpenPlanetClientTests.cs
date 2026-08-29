using System.Net;
using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Tests.EditorBridge;

public sealed class OpenPlanetClientTests
{
    [Fact]
    public async Task GetStatusAsync_ParsesBridgeStatus()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"running":true,"editor_open":true,"map_editor":false,"interface_designer":true,"module_editor":false,"manialink_preview":true}
                    """)
            }));

        var client = new OpenPlanetClient(http, "http://bridge/");

        var status = await client.GetStatusAsync();

        Assert.NotNull(status);
        Assert.True(status.Running);
        Assert.True(status.EditorOpen);
        Assert.False(status.MapEditor);
        Assert.True(status.InterfaceDesigner);
        Assert.False(status.ModuleEditor);
        Assert.True(status.ManialinkPreview);
    }

    [Fact]
    public async Task PreviewManialinkXmlAsync_PostsXmlBody()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"preview":true,"chars":10}""")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.PreviewManialinkXmlAsync("<manialink />");

        Assert.True(result.Success);
        Assert.Equal("http://bridge/manialink/preview", captured?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("application/xml", captured?.Content?.Headers.ContentType?.MediaType);
        Assert.Equal("<manialink />", capturedBody);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNullWhenBridgeIsUnavailable()
    {
        using var http = new HttpClient(new ThrowingHandler());
        var client = new OpenPlanetClient(http, "http://bridge");

        var status = await client.GetStatusAsync();

        Assert.Null(status);
    }

    [Fact]
    public async Task GetRecentManialinkEventsAsync_ParsesEventBuffer()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"events":[{"index":0,"body":"{\"id\":\"gps\",\"action\":\"open\"}"}]}
                    """)
            }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var events = await client.GetRecentManialinkEventsAsync();

        Assert.NotNull(events);
        var evt = Assert.Single(events);
        Assert.Equal(0, evt.Index);
        Assert.Equal("""{"id":"gps","action":"open"}""", evt.Body);
    }

    [Fact]
    public async Task CreateMapAsync_PostsTheEngineNamesTheBridgeExpects()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"created":true,"map_editor":false}""")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.CreateMapAsync(new NewMapRequest());

        Assert.True(result.Success);
        Assert.Equal("http://bridge/map/new", captured?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("application/json", captured?.Content?.Headers.ContentType?.MediaType);
        Assert.Contains("\"environment\":\"Stadium\"", capturedBody);
        Assert.Contains("\"decoration\":\"48x48Screen155Day\"", capturedBody);
        Assert.Contains("\"map_type\":\"TrackMania\\\\TM_Race\"", capturedBody);
        Assert.Contains("\"use_simple_editor\":false", capturedBody);
    }

    [Fact]
    public async Task PlaceMapBlocksAsync_WrapsTheBlocksInABlocksArray()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"requested":1,"placed":1,"blocks":[]}""")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.PlaceMapBlocksAsync([new MapBlockPlacement("RoadTechStart", 24, 24)]);

        Assert.True(result.Success);
        Assert.Equal("http://bridge/map/blocks", captured?.RequestUri?.ToString());
        Assert.Equal(
            """{"blocks":[{"name":"RoadTechStart","x":24,"z":24,"y":-1,"dir":"North"}]}""",
            capturedBody);
    }

    [Fact]
    public async Task SaveMapAsAsync_PostsTheFileName()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"saved":true}""")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.SaveMapAsAsync("MCP/dummy.Map.Gbx");

        Assert.True(result.Success);
        Assert.Equal("http://bridge/map/save", captured?.RequestUri?.ToString());
        Assert.Equal("""{"file_name":"MCP/dummy.Map.Gbx"}""", capturedBody);
    }

    [Fact]
    public async Task CreateMapAsync_ReportsFailureBodyWhenTheBridgeRefuses()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"an editor is already open"}""")
            }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.CreateMapAsync(new NewMapRequest());

        Assert.False(result.Success);
        Assert.Contains("an editor is already open", result.Body);
    }

    [Fact]
    public async Task RemoveMapBlocksAsync_PostsCoordinatesAndKeepsTheProbeOff()
    {
        string? capturedBody = null;
        HttpRequestMessage? captured = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            captured = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"requested":1,"removed":1,"probed":false,"blocks":[]}""")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.RemoveMapBlocksAsync([new MapBlockRemoval(24, 24)]);

        Assert.True(result.Success);
        Assert.Equal("http://bridge/map/blocks/remove", captured?.RequestUri?.ToString());
        Assert.Equal(
            """{"blocks":[{"x":24,"z":24,"y":-1}],"probe":false}""",
            capturedBody);
    }

    [Fact]
    public async Task RemoveMapBlocksAsync_SendsTheProbeFlagOnlyWhenAsked()
    {
        string? capturedBody = null;
        using var http = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        await client.RemoveMapBlocksAsync([new MapBlockRemoval(1, 2, 9)], probe: true);

        Assert.Contains("\"probe\":true", capturedBody);
        Assert.Contains("\"y\":9", capturedBody);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseFactory(request, cancellationToken);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("offline");
        }
    }
}
