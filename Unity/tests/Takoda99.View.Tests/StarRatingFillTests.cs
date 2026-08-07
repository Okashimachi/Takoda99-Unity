using Takoda99.View.ValueObjects;
using Xunit;

namespace Takoda99.View.Tests;

public class StarRatingFillTests
{
    [Fact]
    public void 整数の評価は先頭から満タンになる()
    {
        Assert.Equal(new[] { 1f, 1f, 1f, 0f, 0f }, StarRatingFill.From(3d));
    }

    [Fact]
    public void 端数は境目の星だけを部分的に塗る()
    {
        Assert.Equal(new[] { 1f, 1f, 0.5f, 0f, 0f }, StarRatingFill.From(2.5d));
        Assert.Equal(new[] { 0.2f, 0f, 0f, 0f, 0f }, StarRatingFill.From(0.2d));
    }

    [Fact]
    public void ゼロと満点()
    {
        Assert.Equal(new[] { 0f, 0f, 0f, 0f, 0f }, StarRatingFill.From(0d));
        Assert.Equal(new[] { 1f, 1f, 1f, 1f, 1f }, StarRatingFill.From(5d));
    }

    [Fact]
    public void 範囲外はクランプする()
    {
        Assert.Equal(new[] { 0f, 0f, 0f, 0f, 0f }, StarRatingFill.From(-1.5d));
        Assert.Equal(new[] { 1f, 1f, 1f, 1f, 1f }, StarRatingFill.From(99d));
    }

    [Fact]
    public void 星の数を変えても割り方は変わらない()
    {
        Assert.Equal(new[] { 1f, 0.5f, 0f }, StarRatingFill.From(1.5d, 3));
        Assert.Empty(StarRatingFill.From(1.5d, 0));
    }
}
