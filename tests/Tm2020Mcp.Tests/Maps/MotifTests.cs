using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class MotifTests
{
    private readonly MotifLearner _learner = new();
    private readonly MotifStamper _stamper = new();

    /// <summary>A loop-shaped motif: a three-wide wall over a base row with a support below.</summary>
    private static List<IReadOnlyList<MapBlock>> LoopCorpus(int maps = 10) =>
        Enumerable.Range(0, maps).Select(i => (IReadOnlyList<MapBlock>)
        [
            new MapBlock("LoopStart", 10, 9, i * 8, "North"),
            new MapBlock("LoopStart", 9, 9, i * 8, "North"),
            new MapBlock("LoopStart", 11, 9, i * 8, "North"),
            // Base row and support span the full width, as they do in real loops: a row
            // that only covers the centre anchor is correctly dropped by the threshold.
            new MapBlock("Base", 9, 9, (i * 8) - 1, "North"),
            new MapBlock("Base", 10, 9, (i * 8) - 1, "North"),
            new MapBlock("Base", 11, 9, (i * 8) - 1, "North"),
            new MapBlock("Support", 9, 8, (i * 8) - 1, "North"),
            new MapBlock("Support", 10, 8, (i * 8) - 1, "North"),
            new MapBlock("Support", 11, 8, (i * 8) - 1, "North")
        ]).ToList();

    [Fact]
    public void Learn_RecoversTheWholeStructureRelativeToTheAnchor()
    {
        var motif = _learner.Learn(LoopCorpus(), "LoopStart", radius: 2);

        // Three anchors per map, each seeing the others; the motif is measured around all.
        Assert.True(motif.Samples >= 10);
        Assert.Contains(motif.Blocks, b => b is { DX: -1, DY: 0, DZ: 0, Name: "LoopStart" });
        Assert.Contains(motif.Blocks, b => b is { DX: 1, DY: 0, DZ: 0, Name: "LoopStart" });
        Assert.Contains(motif.Blocks, b => b is { DY: -1, Name: "Support" });
    }

    [Fact]
    public void Learn_DropsBlocksBelowTheSupportThreshold()
    {
        var maps = LoopCorpus();
        maps[0] = [.. maps[0], new MapBlock("Bystander", 12, 9, 0, "North")];

        var motif = _learner.Learn(maps, "LoopStart", radius: 3, threshold: 0.5);

        Assert.DoesNotContain(motif.Blocks, b => b.Name == "Bystander");
    }

    [Fact]
    public void Learn_ReportsHonestlyWhenTheAnchorIsNotInTheCorpus()
    {
        var motif = _learner.Learn(LoopCorpus(), "NoSuchBlock");

        Assert.Equal(0, motif.Samples);
        Assert.Empty(motif.Blocks);
        Assert.Contains("No grid instances", MotifLearner.Format(motif));
    }

    [Fact]
    public void RotatedTo_TurnsOffsetsAndBlockDirectionsTogether()
    {
        // North is +Z and East is -X, so one clockwise step maps (x, z) to (-z, x).
        var motif = new BlockMotif("A", "North", 1, [new MotifBlock(0, 0, 1, "B", "North", 1.0)]);

        var east = motif.RotatedTo("East");
        var block = Assert.Single(east.Blocks);

        Assert.Equal(-1, block.DX);
        Assert.Equal(0, block.DZ);
        Assert.Equal("East", block.Direction);
    }

    [Fact]
    public void RotatedTo_FourStepsReturnToTheOriginal()
    {
        var motif = new BlockMotif("A", "North", 1,
        [
            new MotifBlock(2, 1, -1, "B", "West", 1.0),
            new MotifBlock(-1, 0, 3, "C", "South", 0.8)
        ]);

        var round = motif.RotatedTo("East").RotatedTo("South").RotatedTo("West").RotatedTo("North");

        Assert.Equal(motif.Blocks, round.Blocks);
    }

    [Fact]
    public void At_PlacesTheMotifAroundTheGivenCoordinate()
    {
        var motif = new BlockMotif("A", "North", 1,
        [
            new MotifBlock(0, 0, 0, "A", "North", 1.0),
            new MotifBlock(0, -1, 0, "Support", "North", 1.0)
        ]);

        var blocks = motif.At(20, 9, 30, "North");

        Assert.Contains(blocks, b => b is { Name: "A", X: 20, Y: 9, Z: 30 });
        Assert.Contains(blocks, b => b is { Name: "Support", X: 20, Y: 8, Z: 30 });
    }

    [Fact]
    public void Stamp_RefusesToHalfPlaceAStructure()
    {
        var motif = _learner.Learn(LoopCorpus(), "LoopStart", radius: 2);
        var existing = new[] { new MapBlock("RoadTechStraight", 21, 12, 30, "North") };

        var stamp = _stamper.Stamp(motif, 20, 12, 30, "North", existing);

        Assert.False(stamp.CanPlace);
        Assert.NotEmpty(stamp.Blocked);
        Assert.Contains("lands on an existing block", string.Join(" ", stamp.Problems));
    }

    [Fact]
    public void Stamp_RejectsAFootprintThatLeavesTheMap()
    {
        var motif = _learner.Learn(LoopCorpus(), "LoopStart", radius: 2);

        var stamp = _stamper.Stamp(motif, 0, 12, 30, "North", mapSize: 48);

        Assert.False(stamp.CanPlace);
        Assert.Contains("outside the map", string.Join(" ", stamp.Problems));
    }

    [Fact]
    public void Stamp_GivesTheBridgeExplicitHeights()
    {
        // Motifs carry supports below the anchor, so the ground scan must not be in charge.
        var motif = _learner.Learn(LoopCorpus(), "LoopStart", radius: 2);

        // Stamped at ground level, the support would land underground, so the whole motif
        // is lifted instead of being half-refused by the engine.
        var stamp = _stamper.Stamp(motif, 20, 9, 30, "North", groundY: 9);

        Assert.All(stamp.ToPlacements(), p => Assert.True(p.Y >= 9));
        Assert.Contains(stamp.Problems, note => note.Contains("Lifted the motif"));
        Assert.Contains(stamp.ToPlacements(), p => p.Name == "Support" && p.Y == 9);
    }

    [Fact]
    public void JsonRoundTripsAMotif()
    {
        var motif = _learner.Learn(LoopCorpus(), "LoopStart", radius: 2);
        var restored = BlockMotif.FromJson(motif.ToJson());

        Assert.Equal(motif.Anchor, restored.Anchor);
        Assert.Equal(motif.Samples, restored.Samples);
        Assert.Equal(motif.Blocks, restored.Blocks);
    }
}
