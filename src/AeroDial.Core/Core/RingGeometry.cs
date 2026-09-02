// AeroDial — RingGeometry.cs
// Angle and radius math shared by the overlay renderer and the settings ring editor,
// so a slice hit in the editor is exactly the slice the dial would pick.
// Angles are degrees, 0 = +X (right), increasing clockwise (screen coordinates);
// slice 0 is centered at -90 (top).

namespace AeroDial.Core;

public static class RingGeometry
{
    /// <summary>Screen-space angle in [0, 360) of the vector (dx, dy).</summary>
    public static float AngleOf(float dx, float dy)
    {
        float a = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        return a < 0 ? a + 360f : a;
    }

    /// <summary>Main-ring slice index for an angle: slice 0 is centered on top and
    /// slices run clockwise. Always returns a valid index for sliceCount &gt; 0.</summary>
    public static int SliceIndexAt(float angleDeg, int sliceCount)
    {
        if (sliceCount <= 0) return -1;
        float arc      = 360f / sliceCount;
        float topAlign = ((angleDeg + 90f + arc / 2f) % 360f + 360f) % 360f;
        return (int)(topAlign / arc) % sliceCount;
    }

    /// <summary>Convenience: slice index for a cursor offset from the ring center.</summary>
    public static int SliceIndexAt(float dx, float dy, int sliceCount)
        => SliceIndexAt(AngleOf(dx, dy), sliceCount);

    /// <summary>Center angle (degrees) of main-ring slice i.</summary>
    public static float SliceMidAngle(int index, int sliceCount)
        => -90f + index * (360f / sliceCount);

    /// <summary>Returns (startOff, segAngle, totalArc) for a child ring layout.
    /// Full arc: items distributed over 360. Partial arc: items fanned around parentAngleDeg.</summary>
    public static (float startOff, float segAngle, float totalArc) GetArcLayout(
        int count, float parentAngleDeg, bool partial)
    {
        if (count <= 0) return (-90f, 360f, 360f);
        if (!partial)
        {
            float seg = 360f / count;
            return (-90f - seg / 2f, seg, 360f);
        }
        // Partial arc: fan out centered on parentAngleDeg.
        // Arc per item clamped to [28, 52] degrees; fall back to a full circle when too many items.
        float arcPerItem = Math.Clamp(180f / count, 28f, 52f);
        float total      = arcPerItem * count;
        if (total > 355f)
        {
            float seg = 360f / count;
            return (-90f - seg / 2f, seg, 360f);
        }
        float start = parentAngleDeg - total / 2f - arcPerItem / 2f;
        return (start, arcPerItem, total);
    }

    /// <summary>Maps an angle to a child-ring item index given arc layout parameters.
    /// Returns -1 if the angle falls outside a partial arc (always maps for full arcs).</summary>
    public static int HitTestArc(float angleDeg, float startOff, float segAngle, int count, float totalArc)
    {
        if (count <= 0 || segAngle <= 0f) return -1;
        float rel = ((angleDeg - startOff) % 360f + 360f) % 360f;
        if (rel > totalArc) return -1;
        int idx = (int)(rel / segAngle);
        return Math.Clamp(idx, 0, count - 1);
    }

    /// <summary>Icon size multiplier for a ring with <paramref name="count"/> slices: 1 up to
    /// 8 slices, then shrinking so icons stay inside their slice, never below 64 %.
    /// Shared by the overlay and the settings ring preview so they match.</summary>
    public static float IconSizeMul(int count) => Math.Clamp(8f / Math.Max(count, 1), 0.64f, 1f);

    /// <summary>Splits a center label into at most two lines at a word boundary near the middle.</summary>
    public static (string line1, string? line2) SplitCenterLabel(string label, int maxLine = 11)
    {
        if (label.Length <= maxLine) return (label, null);

        int mid = label.Length / 2;
        int splitAt = -1;
        for (int i = 0; i <= mid; i++)
        {
            if (mid - i > 0 && label[mid - i] == ' ') { splitAt = mid - i; break; }
            if (mid + i < label.Length && label[mid + i] == ' ') { splitAt = mid + i; break; }
        }

        if (splitAt > 0)
        {
            string l1 = label[..splitAt];
            string l2 = label[(splitAt + 1)..];
            if (l1.Length > maxLine) l1 = l1[..maxLine];
            if (l2.Length > maxLine) l2 = l2[..maxLine];
            return (l1, l2);
        }

        return (label[..maxLine], label[maxLine..Math.Min(maxLine * 2, label.Length)]);
    }
}
