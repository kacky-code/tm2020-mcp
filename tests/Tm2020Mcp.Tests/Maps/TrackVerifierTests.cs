using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class TrackVerifierTests
{
    private readonly TrackVerifier _verifier = new();

    [Fact]
    public void Verify_AcceptsATrackLaidAlongTheMeasuredForwardVector()
    {
        // North is +Z, measured across 450 maps. See docs/tm2020-map-geometry.md.
        var blocks = new List<MapBlock>
        {
            new("RoadTechStart", 24, 9, 24, "North"),
            new("RoadTechStraight", 24, 9, 25, "North"),
            new("RoadTechFinish", 24, 9, 26, "North")
        };

        var result = _verifier.Verify(blocks);

        Assert.True(result.Connected);
        Assert.Null(result.Failure);
        Assert.Equal(3, result.Path.Count);
    }

    [Fact]
    public void Verify_CatchesTheReversedTrackThatPlaceBlockReportsAsSuccess()
    {
        // Exactly the layout the first live attempt produced: every block placed fine, but
        // the start faces +Z while the track was laid towards -Z.
        var blocks = new List<MapBlock>
        {
            new("RoadTechStart", 24, 9, 24, "North"),
            new("RoadTechStraight", 24, 9, 23, "North"),
            new("RoadTechFinish", 24, 9, 22, "North")
        };

        var result = _verifier.Verify(blocks);

        Assert.False(result.Connected);
        Assert.Contains("(24, 9, 25) is empty", result.Failure);
    }

    [Fact]
    public void Verify_CatchesAFinishThatIsTurnedAround()
    {
        var blocks = new List<MapBlock>
        {
            new("RoadTechStart", 10, 9, 10, "North"),
            new("RoadTechStraight", 10, 9, 11, "North"),
            new("RoadTechFinish", 10, 9, 12, "South")
        };

        var result = _verifier.Verify(blocks);

        Assert.False(result.Connected);
        Assert.Contains("turned around", result.Failure);
    }

    [Fact]
    public void Verify_WalksTheEastAxisWhichIsMirroredFromTheObviousReading()
    {
        // East is -X, not +X.
        var blocks = new List<MapBlock>
        {
            new("RoadTechStart", 30, 9, 30, "East"),
            new("RoadTechStraight", 29, 9, 30, "East"),
            new("RoadTechFinish", 28, 9, 30, "East")
        };

        Assert.True(_verifier.Verify(blocks).Connected);
    }

    [Fact]
    public void Verify_ReportsWhenThereIsNoGridStartBlock()
    {
        var blocks = new List<MapBlock>
        {
            new("RoadTechStraight", 1, 9, 1, "North"),
            new("RoadTechStart", 2, 9, 2, "", IsFree: true)
        };

        var result = _verifier.Verify(blocks);

        Assert.False(result.Connected);
        Assert.Contains("No grid start block", result.Failure);
    }

    [Fact]
    public void Format_LeadsWithTheVerdict()
    {
        var blocks = new List<MapBlock>
        {
            new("RoadTechStart", 0, 9, 0, "North"),
            new("RoadTechFinish", 0, 9, 1, "North")
        };

        var text = TrackVerifier.Format(_verifier.Verify(blocks));

        Assert.StartsWith("Connected:", text);
        Assert.Contains("RoadTechFinish <0, 9, 1> dir=North", text);
    }
}
