using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tests.Maps;

public sealed class DummyTrackBuilderTests
{
    [Fact]
    public void Build_LaysStartStraightFinishAlongTheDirection()
    {
        var blocks = DummyTrackBuilder.Build(originX: 10, originZ: 20, straightCount: 1, direction: "North");

        Assert.Collection(
            blocks,
            block =>
            {
                Assert.Equal(DummyTrackBuilder.StartBlock, block.Name);
                Assert.Equal(10, block.X);
                Assert.Equal(20, block.Z);
            },
            block =>
            {
                Assert.Equal(DummyTrackBuilder.StraightBlock, block.Name);
                Assert.Equal(10, block.X);
                Assert.Equal(21, block.Z);
            },
            block =>
            {
                Assert.Equal(DummyTrackBuilder.FinishBlock, block.Name);
                Assert.Equal(10, block.X);
                Assert.Equal(22, block.Z);
            });
    }

    [Fact]
    public void Build_LeavesHeightToTheBridge()
    {
        var blocks = DummyTrackBuilder.Build();

        Assert.All(blocks, block => Assert.Equal(-1, block.Y));
    }

    [Fact]
    public void Build_CarriesTheDirectionOntoEveryBlock()
    {
        var blocks = DummyTrackBuilder.Build(direction: "east");

        Assert.All(blocks, block => Assert.Equal("east", block.Dir));
        Assert.Equal(DummyTrackBuilder.DefaultOriginX - 2, blocks[^1].X);
        Assert.Equal(DummyTrackBuilder.DefaultOriginZ, blocks[^1].Z);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(5, 7)]
    public void Build_AlwaysBookendsTheStraightsWithStartAndFinish(int straightCount, int expectedCount)
    {
        var blocks = DummyTrackBuilder.Build(straightCount: straightCount);

        Assert.Equal(expectedCount, blocks.Count);
        Assert.Equal(DummyTrackBuilder.StartBlock, blocks[0].Name);
        Assert.Equal(DummyTrackBuilder.FinishBlock, blocks[^1].Name);
    }

    [Fact]
    public void Build_RejectsANegativeStraightCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DummyTrackBuilder.Build(straightCount: -1));
    }

    [Fact]
    public void Build_RejectsAnUnknownDirection()
    {
        Assert.Throws<ArgumentException>(() => DummyTrackBuilder.Build(direction: "up"));
    }

    [Theory]
    [InlineData("North", true)]
    [InlineData(" west ", true)]
    [InlineData("northeast", false)]
    [InlineData("", false)]
    public void IsKnownDirection_AcceptsTheFourCardinalDirections(string direction, bool expected)
    {
        Assert.Equal(expected, DummyTrackBuilder.IsKnownDirection(direction));
    }
}
