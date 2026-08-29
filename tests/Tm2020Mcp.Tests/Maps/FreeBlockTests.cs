using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class FreeBlockTests
{
    [Fact]
    public void GridCell_DerivesTheCellFromTheWorldPosition()
    {
        // One cell is 32 world units across and 8 tall.
        var block = new MapBlock("PlatformIceWallStraight", -1, 0, -1, "", IsFree: true,
            Position: new Vector3(832, 64, 928));

        Assert.Equal((26, 8, 29), block.GridCell);
    }

    [Fact]
    public void GridCell_IsNullForAGridBlockThatCarriesNoPosition()
    {
        Assert.Null(new MapBlock("RoadTechStraight", 24, 9, 24, "North").GridCell);
    }

    [Fact]
    public void IsTilted_IgnoresYawAndCatchesPitchAndRoll()
    {
        // Yaw alone is a flat turn, which the grid can express; pitch and roll are not.
        var yawed = new MapBlock("A", -1, 0, -1, "", IsFree: true,
            Rotation: new Vector3(MathF.PI / 2, 0, 0));
        var rolled = new MapBlock("A", -1, 0, -1, "", IsFree: true,
            Rotation: new Vector3(0, 0, -MathF.PI / 2));
        var pitched = new MapBlock("A", -1, 0, -1, "", IsFree: true,
            Rotation: new Vector3(0, 0.26f, 0));

        Assert.False(yawed.IsTilted);
        Assert.True(rolled.IsTilted);
        Assert.True(pitched.IsTilted);
    }

    [Fact]
    public void IsTilted_IsFalseWhenThereIsNoRotationAtAll()
    {
        Assert.False(new MapBlock("RoadTechStraight", 1, 9, 1, "North").IsTilted);
    }

    [Fact]
    public void TiltedBlockCount_CountsAcrossTheMap()
    {
        var map = new MapGbxFile("m.Map.Gbx", "m", "48x48Day", "<48, 255, 48>",
        [
            new MapBlock("A", -1, 0, -1, "", IsFree: true, Rotation: new Vector3(0, 0, -1.57f)),
            new MapBlock("B", -1, 0, -1, "", IsFree: true, Rotation: new Vector3(1.57f, 0, 0)),
            new MapBlock("C", 1, 9, 1, "North")
        ]);

        Assert.Equal(2, map.FreeBlockCount);
        Assert.Equal(1, map.TiltedBlockCount);
    }
}
