using Tm2020Mcp.Manialinks;

namespace Tm2020Mcp.Tests.Manialinks;

public sealed class ManialinkInspectorTests
{
    [Fact]
    public void InspectInteractiveControls_ReportsActionAndScriptEvents()
    {
        var inspector = new ManialinkInspector();
        const string xml = """
            <frame>
              <label id="gps" action="open-gps" text="GPS" />
              <quad id="emoji" scriptevents="1" />
              <label id="static" text="Static" />
            </frame>
            """;

        var result = inspector.InspectInteractiveControls(xml);

        Assert.Contains("Interactive controls: 2", result);
        Assert.Contains("label id=gps, action=open-gps", result);
        Assert.Contains("quad id=emoji", result);
        Assert.DoesNotContain("static", result);
    }
}
