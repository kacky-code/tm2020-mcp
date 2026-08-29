using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class BlockConnectionModelTests
{
    [Fact]
    public void Learn_GivesAStartBlockExactlyOneExit()
    {
        var maps = Repeat(20, i =>
        [
            new MapBlock("RoadTechStart", 0, 9, i * 4, "North"),
            new MapBlock("RoadTechStraight", 0, 9, (i * 4) + 1, "North")
        ]);

        var model = BlockConnectionModel.Learn(maps, minimumSamples: 20);

        Assert.Equal([GridOffset.PlusZ], model.Connections("RoadTechStart", "North"));
    }

    [Fact]
    public void Learn_GivesACurveTwoPerpendicularExits()
    {
        var maps = Repeat(20, i =>
        [
            new MapBlock("RoadTechCurve1", 0, 9, i * 4, "North"),
            new MapBlock("RoadTechStraight", -1, 9, i * 4, "East"),
            new MapBlock("RoadTechStraight", 0, 9, (i * 4) + 1, "North")
        ]);

        var model = BlockConnectionModel.Learn(maps, minimumSamples: 20);
        var curve = model.Connections("RoadTechCurve1", "North");

        Assert.Equal(2, curve.Count);
        Assert.Contains(GridOffset.MinusX, curve);
        Assert.Contains(GridOffset.PlusZ, curve);
    }

    [Fact]
    public void Learn_IgnoresOtherVariantsWhenAskedForOne()
    {
        // A curve's variant changes its shape, and the bridge only ever places variant 0.
        var maps = Repeat(20, i =>
        [
            new MapBlock("RoadTechCurve1", 0, 9, i * 4, "North", Variant: 1),
            new MapBlock("RoadTechStraight", 0, 9, (i * 4) + 1, "North", Variant: 1)
        ]);

        var model = BlockConnectionModel.Learn(maps, minimumSamples: 20, variant: 0);

        Assert.Equal(0, model.EntryCount);
        Assert.False(model.Knows("RoadTechCurve1", "North"));
    }

    [Fact]
    public void Learn_DropsOffsetsSeenTooRarelyToBeStructural()
    {
        // One map in twenty has an unrelated block alongside; that is a neighbour, not a
        // connection.
        var maps = Repeat(20, i =>
        {
            var blocks = new List<MapBlock>
            {
                new("RoadTechStraight", 0, 9, i * 4, "North"),
                new("RoadTechStraight", 0, 9, (i * 4) + 1, "North")
            };

            if (i == 0)
                blocks.Add(new MapBlock("RoadTechStraight", 1, 9, 0, "East"));

            return blocks;
        });

        var connections = BlockConnectionModel.Learn(maps, minimumSamples: 20)
            .Connections("RoadTechStraight", "North");

        Assert.DoesNotContain(GridOffset.PlusX, connections);
    }

    [Fact]
    public void JsonRoundTripsTheModel()
    {
        var maps = Repeat(20, i =>
        [
            new MapBlock("RoadTechStart", 0, 9, i * 4, "North"),
            new MapBlock("RoadTechStraight", 0, 9, (i * 4) + 1, "North")
        ]);

        var model = BlockConnectionModel.Learn(maps, minimumSamples: 20);
        var restored = BlockConnectionModel.FromJson(model.ToJson());

        Assert.Equal(model.EntryCount, restored.EntryCount);
        Assert.Equal(
            model.Connections("RoadTechStart", "North"),
            restored.Connections("RoadTechStart", "North"));
    }

    [Fact]
    public void LoadBundled_ShipsTheRoadFamilyMeasuredFromRealMaps()
    {
        var model = BlockConnectionModel.LoadBundled();

        Assert.True(model.EntryCount > 0);
        Assert.Equal([GridOffset.PlusZ], model.Connections("RoadTechStart", "North"));
        Assert.Equal([GridOffset.MinusX], model.Connections("RoadTechStart", "East"));
        Assert.Equal([GridOffset.MinusZ], model.Connections("RoadTechFinish", "North"));

        var straight = model.Connections("RoadTechStraight", "North");
        Assert.Contains(GridOffset.PlusZ, straight);
        Assert.Contains(GridOffset.MinusZ, straight);
    }

    private static List<IReadOnlyList<MapBlock>> Repeat(int count, Func<int, IReadOnlyList<MapBlock>> factory) =>
        Enumerable.Range(0, count).Select(factory).ToList();
}
