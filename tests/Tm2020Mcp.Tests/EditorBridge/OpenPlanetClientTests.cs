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
