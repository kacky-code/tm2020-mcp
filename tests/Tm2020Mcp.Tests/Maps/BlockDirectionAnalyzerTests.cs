using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class BlockDirectionAnalyzerTests
{
    private readonly BlockDirectionAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_RecoversTheForwardVectorOfAStartBlock()
    {
        // Three maps, each with a North start whose road continues at +Z.
        var maps = Enumerable.Range(0, 3).Select(i => (IReadOnlyList<MapBlock>)
        [
            new MapBlock("RoadTechStart", i, 9, 0, "North"),
            new MapBlock("RoadTechStraight", i, 9, 1, "North")
        ]).ToList();

        var observation = Assert.Single(
            _analyzer.Analyze(maps, nameFilter: "RoadTechStart", minimumSamples: 3));

        Assert.Equal("North", observation.Direction);
        Assert.Equal(3, observation.Samples);
        Assert.Equal(3, observation.Neighbours["+Z"]);
        Assert.Equal(0, observation.Neighbours["-Z"]);
        Assert.Equal("+Z", observation.Forward);
    }

    [Fact]
    public void Analyze_LeavesASymmetricStraightWithoutAForwardVerdict()
    {
        // A straight chains both ways, so neither offset dominates and the analyzer must
        // not invent a direction for it.
        IReadOnlyList<MapBlock> map =
        [
            new MapBlock("RoadTechStraight", 0, 9, 0, "North"),
            new MapBlock("RoadTechStraight", 0, 9, 1, "North"),
            new MapBlock("RoadTechStraight", 0, 9, 2, "North")
        ];

        var middle = Assert.Single(_analyzer.Analyze([map], minimumSamples: 3));

        Assert.Equal(2, middle.Neighbours["+Z"]);
        Assert.Equal(2, middle.Neighbours["-Z"]);
        Assert.Null(middle.Forward);
    }

    [Fact]
    public void Analyze_SkipsFreeBlocksBecauseTheyCarryNoGridDirection()
    {
        // Campaign maps are largely free blocks; counting them would be noise.
        IReadOnlyList<MapBlock> map =
        [
            new MapBlock("RoadTechStart", 0, 9, 0, "", IsFree: true),
            new MapBlock("RoadTechStraight", 0, 9, 1, "", IsFree: true)
        ];

        Assert.Empty(_analyzer.Analyze([map], minimumSamples: 1));
    }

    [Fact]
    public void Analyze_HonoursTheSampleThreshold()
    {
        IReadOnlyList<MapBlock> map =
        [
            new MapBlock("RoadTechStraight", 0, 9, 0, "North"),
            new MapBlock("RoadTechStraight", 0, 9, 1, "North")
        ];

        Assert.Empty(_analyzer.Analyze([map], minimumSamples: 5));
        Assert.NotEmpty(_analyzer.Analyze([map], minimumSamples: 2));
    }

    [Fact]
    public void Format_SaysSoWhenNothingMatched()
    {
        Assert.Contains("free blocks", BlockDirectionAnalyzer.Format([]));
    }
}
