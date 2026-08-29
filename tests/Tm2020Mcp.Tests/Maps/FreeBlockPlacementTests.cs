using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class FreeBlockPlacementTests
{
    [Fact]
    public void AtCell_ConvertsCellsToWorldUnits()
    {
        // A cell is 32 wide and 8 tall, so ground level y=9 is 72 world units up.
        var placement = FreeBlockPlacement.AtCell("PlatformIceWallStraight", 24, 9, 27);

        Assert.Equal(768, placement.X);
        Assert.Equal(72, placement.Y);
        Assert.Equal(864, placement.Z);
    }

    [Fact]
    public void AtCell_KeepsFractionalCellsOffTheGrid()
    {
        // Placing between cells is the entire point of a free block.
        var placement = FreeBlockPlacement.AtCell("A", 24.5f, 9, 24);

        Assert.Equal(784, placement.X);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(180, 180)]
    [InlineData(360, 0)]
    [InlineData(405, 45)]
    [InlineData(450, 90)]
    [InlineData(-90, -90)]
    [InlineData(-180, 180)]
    [InlineData(-405, -45)]
    public void Wrap_BringsAnglesIntoTheRangeRealMapsUse(float input, float expected)
    {
        Assert.Equal(expected, FreeBlockPlacement.Wrap(input), 3);
    }

    [Fact]
    public void RotationRadians_ConvertsAndWraps()
    {
        var placement = new FreeBlockPlacement("A", 0, 0, 0, YawDegrees: 405, RollDegrees: -90);

        Assert.Equal(MathF.PI / 4, placement.RotationRadians.X, 4);
        Assert.Equal(0, placement.RotationRadians.Y, 4);
        Assert.Equal(-MathF.PI / 2, placement.RotationRadians.Z, 4);
    }

    [Fact]
    public void Writer_RefusesToOverwriteTheSourceMap()
    {
        var writer = new MapGbxWriter();
        var path = Path.Combine(Path.GetTempPath(), "some.Map.Gbx");

        var ex = Assert.Throws<ArgumentException>(() =>
            writer.AddFreeBlocks(path, path, [new FreeBlockPlacement("A", 0, 0, 0)]));

        Assert.Contains("Refusing to overwrite", ex.Message);
    }

    [Fact]
    public void Writer_RefusesAnEmptyPlacementList()
    {
        var writer = new MapGbxWriter();

        Assert.Throws<ArgumentException>(() =>
            writer.AddFreeBlocks("in.Map.Gbx", "out.Map.Gbx", []));
    }

    [Fact]
    public void Writer_RefusesABlockWithNoName()
    {
        var writer = new MapGbxWriter();

        var ex = Assert.Throws<ArgumentException>(() =>
            writer.AddFreeBlocks("in.Map.Gbx", "out.Map.Gbx", [new FreeBlockPlacement(" ", 0, 0, 0)]));

        Assert.Contains("no name", ex.Message);
    }
}
