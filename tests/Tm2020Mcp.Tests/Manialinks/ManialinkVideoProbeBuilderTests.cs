using Tm2020Mcp.Manialinks;

namespace Tm2020Mcp.Tests.Manialinks;

public sealed class ManialinkVideoProbeBuilderTests
{
    [Fact]
    public void Build_ReturnsVideoTagWithEscapedData()
    {
        var builder = new ManialinkVideoProbeBuilder();

        var xml = builder.Build("file://Media/Videos/gps<1>.webm", music: true, play: true, hidden: true);

        Assert.Contains("<manialink version=\"3\">", xml);
        Assert.Contains("<video data=\"file://Media/Videos/gps&lt;1&gt;.webm\" music=\"1\" play=\"1\" hidden=\"1\" />", xml);
    }
}
