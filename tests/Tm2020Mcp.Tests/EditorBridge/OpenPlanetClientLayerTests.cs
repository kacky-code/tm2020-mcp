using System.Net;
using Tm2020Mcp.EditorBridge;

namespace Tm2020Mcp.Tests.EditorBridge;

public sealed class OpenPlanetClientLayerTests
{
    [Fact]
    public async Task GetUiLayersAsync_ParsesLayerList()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"connected":true,"layers":[
                      {"index":0,"attachId":"Unassigned","type":"Normal","visible":true,"animInProgress":false,"scriptRunning":true,"xmlLength":420,"tag":"<manialink version=\"3\" id=\"UIModule_Race_Chrono\">"},
                      {"index":1,"attachId":"Unassigned","type":"ScoresTable","visible":false,"animInProgress":true,"scriptRunning":false,"xmlLength":99,"tag":"<manialink version=\"3\">"}
                    ]}
                    """)
            }));

        var client = new OpenPlanetClient(http, "http://bridge/");

        var result = await client.GetUiLayersAsync();

        Assert.NotNull(result);
        Assert.True(result.Connected);
        Assert.Equal(2, result.Layers.Count);
        Assert.Equal("Normal", result.Layers[0].Type);
        Assert.True(result.Layers[0].Visible);
        Assert.True(result.Layers[0].ScriptRunning);
        Assert.Equal(420, result.Layers[0].XmlLength);
        Assert.Contains("UIModule_Race_Chrono", result.Layers[0].Tag);
        Assert.Equal("ScoresTable", result.Layers[1].Type);
        Assert.False(result.Layers[1].Visible);
        Assert.True(result.Layers[1].AnimInProgress);
    }

    [Fact]
    public async Task GetUiLayersAsync_ReportsNotConnectedRatherThanEmpty()
    {
        // In the menus there is no playground. An empty list for that reason must not read
        // as "the HUD has no layers", or an agent draws the wrong conclusion.
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"connected":false,"layers":[],"error":"not connected to a server"}
                    """)
            }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var result = await client.GetUiLayersAsync();

        Assert.NotNull(result);
        Assert.False(result.Connected);
        Assert.Empty(result.Layers);
        Assert.Equal("not connected to a server", result.Error);
    }

    [Fact]
    public async Task GetUiLayerXmlAsync_RequestsTheIndexedLayer()
    {
        HttpRequestMessage? captured = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<manialink version=\"3\"><label text=\"hi\"/></manialink>")
            };
        }));

        var client = new OpenPlanetClient(http, "http://bridge");

        var xml = await client.GetUiLayerXmlAsync(7);

        Assert.NotNull(captured);
        Assert.Equal("http://bridge/layers/7", captured.RequestUri?.ToString());
        Assert.Contains("<manialink", xml);
    }

    [Fact]
    public async Task GetUiLayerXmlAsync_ReturnsNullWhenLayerMissing()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"error":"no layer at index 99"}""")
            }));

        var client = new OpenPlanetClient(http, "http://bridge");

        Assert.Null(await client.GetUiLayerXmlAsync(99));
    }
}
