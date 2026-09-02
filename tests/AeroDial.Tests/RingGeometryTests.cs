using AeroDial.Core;

namespace AeroDial.Tests;

public class RingGeometryTests
{
    [Theory]
    [InlineData(0, -10, 0)]      // straight up
    [InlineData(10, 0, 2)]       // right (8 slices: 0 top, 2 right, 4 bottom, 6 left)
    [InlineData(0, 10, 4)]
    [InlineData(-10, 0, 6)]
    [InlineData(7, -7, 1)]       // up-right
    [InlineData(-7, -7, 7)]      // up-left
    public void SliceIndexAt_eight_slices_clockwise_from_top(float dx, float dy, int expected)
        => Assert.Equal(expected, RingGeometry.SliceIndexAt(dx, dy, 8));

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(12)]
    public void SliceIndexAt_covers_every_angle_and_every_slice(int n)
    {
        var seen = new HashSet<int>();
        for (float a = 0; a < 360f; a += 0.5f)
        {
            int idx = RingGeometry.SliceIndexAt(a, n);
            Assert.InRange(idx, 0, n - 1);
            seen.Add(idx);
        }
        Assert.Equal(n, seen.Count);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void SliceIndexAt_slice_boundaries_are_half_an_arc_from_the_slice_center(int n)
    {
        float arc = 360f / n;
        for (int i = 0; i < n; i++)
        {
            float mid = RingGeometry.SliceMidAngle(i, n);
            Assert.Equal(i, RingGeometry.SliceIndexAt(mid, n));
            Assert.Equal(i, RingGeometry.SliceIndexAt(mid - arc / 2f + 0.01f, n));
            Assert.Equal(i, RingGeometry.SliceIndexAt(mid + arc / 2f - 0.01f, n));
        }
    }

    [Fact]
    public void SliceIndexAt_rejects_zero_slices()
        => Assert.Equal(-1, RingGeometry.SliceIndexAt(0f, 0));

    [Fact]
    public void GetArcLayout_full_ring_spans_360_centered_on_top()
    {
        var (start, seg, total) = RingGeometry.GetArcLayout(6, -90f, partial: false);
        Assert.Equal(60f, seg);
        Assert.Equal(360f, total);
        Assert.Equal(-120f, start);
    }

    [Fact]
    public void GetArcLayout_partial_arc_is_centered_on_parent_angle()
    {
        var (start, seg, total) = RingGeometry.GetArcLayout(4, 45f, partial: true);
        Assert.Equal(45f, seg);              // 180/4 = 45, inside [28, 52]
        Assert.Equal(180f, total);
        // arc runs from start+seg/2 to start+seg/2+total; its middle is the parent angle
        Assert.Equal(45f, start + seg / 2f + total / 2f, 3);
    }

    [Fact]
    public void GetArcLayout_partial_falls_back_to_full_ring_when_too_many_items()
    {
        var (_, seg, total) = RingGeometry.GetArcLayout(16, 0f, partial: true);
        Assert.Equal(360f, total);
        Assert.Equal(22.5f, seg);
    }

    [Fact]
    public void HitTestArc_maps_inside_and_rejects_outside_partial_arc()
    {
        var (start, seg, total) = RingGeometry.GetArcLayout(3, -90f, partial: true);
        int first = RingGeometry.HitTestArc(start + seg / 2f, start, seg, 3, total);
        int last  = RingGeometry.HitTestArc(start + total - seg / 2f, start, seg, 3, total);
        int miss  = RingGeometry.HitTestArc(start + total + 20f, start, seg, 3, total);
        Assert.Equal(0, first);
        Assert.Equal(2, last);
        Assert.Equal(-1, miss);
    }

    [Fact]
    public void HitTestArc_full_ring_always_maps()
    {
        var (start, seg, total) = RingGeometry.GetArcLayout(5, 0f, partial: false);
        for (float a = 0; a < 360f; a += 1f)
            Assert.InRange(RingGeometry.HitTestArc(a, start, seg, 5, total), 0, 4);
    }

    [Theory]
    [InlineData("Settings", "Settings", null)]                    // fits on one line
    [InlineData("Volume Up Loud", "Volume", "Up Loud")]           // space nearest the midpoint wins
    [InlineData("Clipboard History", "Clipboard", "History")]
    [InlineData("Supercalifragilistic", "Supercalifr", "agilistic")] // no space: hard cut at 11
    public void SplitCenterLabel_splits_long_labels_at_word_boundaries(string label, string l1, string? l2)
    {
        var (a, b) = RingGeometry.SplitCenterLabel(label);
        Assert.Equal(l1, a);
        Assert.Equal(l2, b);
    }
}
