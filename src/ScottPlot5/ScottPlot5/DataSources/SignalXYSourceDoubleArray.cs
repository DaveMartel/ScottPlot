﻿namespace ScottPlot.DataSources;

public class SignalXYSourceDoubleArray : ISignalXYSource
{
    readonly double[] Xs;
    readonly double[] Ys;
    public bool Rotated { get; set; } = false;

    public double XOffset { get; set; } = 0;
    public double YOffset { get; set; } = 0;
    public int MinimumIndex { get; set; } = 0;
    public int MaximumIndex { get; set; }

    public SignalXYSourceDoubleArray(double[] xs, double[] ys)
    {
        if (xs.Length != ys.Length)
        {
            throw new ArgumentException($"{nameof(xs)} and {nameof(ys)} must have equal length");
        }

        Xs = xs;
        Ys = ys;
        MaximumIndex = xs.Length - 1;
    }

    public AxisLimits GetAxisLimits()
    {
        double xMin = Xs[MinimumIndex] + XOffset;
        double xMax = Xs[MaximumIndex] + XOffset;

        CoordinateRange xRange = new(xMin, xMax);
        CoordinateRange yRange = GetRangeY(MinimumIndex, MaximumIndex);
        return Rotated
            ? new AxisLimits(yRange, xRange)
            : new AxisLimits(xRange, yRange);
    }

    public Pixel[] GetPixelsToDraw(RenderPack rp, IAxes axes)
    {
        return Rotated
            ? GetPixelsToDrawVertically(rp, axes)
            : GetPixelsToDrawHorizontally(rp, axes);
    }

    private Pixel[] GetPixelsToDrawHorizontally(RenderPack rp, IAxes axes)
    {
        // determine the range of data in view
        (Pixel[] PointBefore, int dataIndexFirst) = GetFirstPointX(axes);
        (Pixel[] PointAfter, int dataIndexLast) = GetLastPointX(axes);
        IndexRange visibileRange = new(dataIndexFirst, dataIndexLast);

        // get all points in view
        IEnumerable<Pixel> VisiblePoints = Enumerable.Range(0, (int)Math.Ceiling(rp.DataRect.Width))
            .Select(pxColumn => GetColumnPixelsX(pxColumn, visibileRange, rp, axes))
            .SelectMany(x => x);

        // combine with one extra point before and after
        Pixel[] points = [.. PointBefore, .. VisiblePoints, .. PointAfter];

        // use interpolation at the edges to prevent points from going way off the screen
        if (PointBefore.Length > 0)
            SignalInterpolation.InterpolateBeforeX(rp, points);

        if (PointAfter.Length > 0)
            SignalInterpolation.InterpolateAfterX(rp, points);

        return points;
    }

    private Pixel[] GetPixelsToDrawVertically(RenderPack rp, IAxes axes)
    {
        // determine the range of data in view
        (Pixel[] PointBefore, int dataIndexFirst) = GetFirstPointY(axes);
        (Pixel[] PointAfter, int dataIndexLast) = GetLastPointY(axes);
        IndexRange visibleRange = new(dataIndexFirst, dataIndexLast);

        // get all points in view
        IEnumerable<Pixel> VisiblePoints = Enumerable.Range(0, (int)Math.Ceiling(rp.DataRect.Height))
            .Select(pxRow => GetColumnPixelsY(pxRow, visibleRange, rp, axes))
            .SelectMany(x => x);

        // combine with one extra point before and after
        Pixel[] points = [.. PointBefore, .. VisiblePoints, .. PointAfter];

        // use interpolation at the edges to prevent points from going way off the screen
        if (PointBefore.Length > 0)
            SignalInterpolation.InterpolateBeforeY(rp, points);

        if (PointAfter.Length > 0)
            SignalInterpolation.InterpolateAfterY(rp, points);

        return points;
    }

    /// <summary>
    /// Return the vertical range covered by data between the given indices (inclusive)
    /// </summary>
    public CoordinateRange GetRangeY(int index1, int index2)
    {
        double min = Ys[index1];
        double max = Ys[index1];

        var minindex = Math.Min(index1, index2);
        var maxindex = Math.Max(index1, index2);

        for (int i = minindex; i <= maxindex; i++)
        {
            min = Math.Min(Ys[i], min);
            max = Math.Max(Ys[i], max);
        }

        return new CoordinateRange(min + YOffset, max + YOffset);
    }

    /// <summary>
    /// Get the index associated with the given X position
    /// </summary>
    public int GetIndex(double x)
    {
        IndexRange range = new(MinimumIndex, MaximumIndex);
        return GetIndex(x, range);
    }

    /// <summary>
    /// Get the index associated with the given X position limited to the given range
    /// </summary>
    public int GetIndex(double x, IndexRange indexRange)
    {
        int index = Array.BinarySearch(Xs, indexRange.Min, indexRange.Length, x - XOffset);

        // If x is not exactly matched to any value in Xs, BinarySearch returns a negative number. We can bitwise negation to obtain the position where x would be inserted (i.e., the next highest index).
        // If x is below the min Xs, BinarySearch returns -1. Here, bitwise negation returns 0 (i.e., x would be inserted at the first index of the array).
        // If x is above the max Xs, BinarySearch returns -maxIndex. Bitwise negation of this value returns maxIndex + 1 (i.e., the position after the last index). However, this index is beyond the array bounds, so we return the final index instead.
        if (index < 0)
        {
            index = index < -indexRange.Max ? indexRange.Max : ~index; // read BinarySearch() docs
        }

        return index;
    }

    /// <summary>
    /// Given a pixel column, return the pixels to render its line.
    /// If the column contains no data, no pixels are returned.
    /// If the column contains one point, return that one pixel.
    /// If the column contains multiple points, return 4 pixels: enter, min, max, and exit
    /// </summary>
    public IEnumerable<Pixel> GetColumnPixelsX(int pixelColumnIndex, IndexRange rng, RenderPack rp, IAxes axes)
    {
        float xPixel = pixelColumnIndex + rp.DataRect.Left;
        double unitsPerPixelX = axes.XAxis.Width / rp.DataRect.Width;
        double start = axes.XAxis.Min + unitsPerPixelX * pixelColumnIndex;
        double end = start + unitsPerPixelX;
        int startIndex = GetIndex(start, rng);
        int endIndex = GetIndex(end, rng);
        int pointsInRange = endIndex - startIndex;

        if (pointsInRange == 0)
        {
            yield break;
        }

        yield return new Pixel(xPixel, axes.GetPixelY(Ys[startIndex] + YOffset)); // enter

        if (pointsInRange > 1)
        {
            CoordinateRange yRange = GetRange(startIndex, endIndex - 1);
            CoordinateRange yRange = GetRangeY(startIndex, lastIndex); //YOffset is added in GetRangeY
            yield return new Pixel(xPixel, axes.GetPixelY(yRange.Min)); // min
            yield return new Pixel(xPixel, axes.GetPixelY(yRange.Max)); // max
            yield return new Pixel(xPixel, axes.GetPixelY(Ys[endIndex - 1] + YOffset)); // exit
        }
    }

    /// <summary>
    /// Given a pixel column, return the pixels to render its line.
    /// If the column contains no data, no pixels are returned.
    /// If the column contains one point, return that one pixel.
    /// If the column contains multiple points, return 4 pixels: enter, min, max, and exit
    /// </summary>
    public IEnumerable<Pixel> GetColumnPixelsY(int rowColumnIndex, IndexRange rng, RenderPack rp, IAxes axes)
    {
        float yPixel = rp.DataRect.Bottom - rowColumnIndex;
        double unitsPerPixelY = axes.YAxis.Height / rp.DataRect.Height;
        double start = axes.YAxis.Min + unitsPerPixelY * rowColumnIndex;
        double end = start + unitsPerPixelY;
        int startIndex = GetIndex(start, rng);
        int endIndex = GetIndex(end, rng);
        int pointsInRange = endIndex - startIndex;

        if (pointsInRange == 0)
        {
            yield break;
        }

        yield return new Pixel(axes.GetPixelX(Ys[startIndex] + XOffset), yPixel); // enter

        if (pointsInRange > 1)
        {
            CoordinateRange yRange = GetRange(startIndex, endIndex - 1);
            CoordinateRange yRange = GetRangeY(startIndex, lastIndex);
            yield return new Pixel(axes.GetPixelX(yRange.Min), yPixel); // min
            yield return new Pixel(axes.GetPixelX(yRange.Max), yPixel); // max
            yield return new Pixel(axes.GetPixelX(Ys[endIndex - 1] + XOffset), yPixel); // exit
        }
    }

    /// <summary>
    /// If data is off to the screen to the left, 
    /// returns information about the closest point off the screen
    /// </summary>
    private (Pixel[] pointsBefore, int firstIndex) GetFirstPointX(IAxes axes)
    {
        int firstPointIndex = GetIndex(axes.XAxis.Range.Span > 0 ? axes.XAxis.Min : axes.XAxis.Max); // if axis is reversed first index will on the right limit of the plot

        if (firstPointIndex > MinimumIndex)
        {
            float beforeX = axes.GetPixelX(Xs[firstPointIndex - 1] + XOffset);
            float beforeY = axes.GetPixelY(Ys[firstPointIndex - 1] + YOffset);
            Pixel beforePoint = new(beforeX, beforeY);
            return ([beforePoint], firstPointIndex);
        }
        else
        {
            return ([], MinimumIndex);
        }
    }

    /// <summary>
    /// If data is off to the screen to the bottom, 
    /// returns information about the closest point off the screen
    /// </summary>
    private (Pixel[] pointsBefore, int firstIndex) GetFirstPointY(IAxes axes)
    {
        int firstPointIndex = GetIndex(axes.YAxis.Range.Span > 0? axes.YAxis.Min: axes.YAxis.Max); // if axis is reversed first index will on the top limit of the plot

        if (firstPointIndex > MinimumIndex)
        {
            float beforeX = axes.GetPixelX(Ys[firstPointIndex - 1] + XOffset);
            float beforeY = axes.GetPixelY(Xs[firstPointIndex - 1] + YOffset);
            Pixel beforePoint = new(beforeX, beforeY);
            return ([beforePoint], firstPointIndex);
        }
        else
        {
            return ([], MinimumIndex);
        }
    }

    /// <summary>
    /// If data is off to the screen to the right, 
    /// returns information about the closest point off the screen
    /// </summary>
    private (Pixel[] pointsBefore, int lastIndex) GetLastPointX(IAxes axes)
    {
        int lastPointIndex = GetIndex(axes.XAxis.Range.Span > 0 ? axes.XAxis.Max : axes.XAxis.Min); // if axis is reversed last index will on the left limit of the plot

        if (lastPointIndex < MaximumIndex)
        {
            float afterX = axes.GetPixelX(Xs[lastPointIndex+1] + XOffset);
            float afterY = axes.GetPixelY(Ys[lastPointIndex+1] + YOffset);
            Pixel afterPoint = new(afterX, afterY);
            return ([afterPoint], lastPointIndex);
        }
        else
        {
            return ([], MaximumIndex);
        }
    }

    /// <summary>
    /// If data is off to the screen to the top, 
    /// returns information about the closest point off the screen
    /// </summary>
    private (Pixel[] pointsBefore, int lastIndex) GetLastPointY(IAxes axes)
    {
        int lastPointIndex = GetIndex(axes.YAxis.Range.Span > 0 ? axes.YAxis.Max : axes.YAxis.Min); // if axis is reversed last index will on the bottom limit of the plot

        if (lastPointIndex < MaximumIndex)
        {
            float afterX = axes.GetPixelX(Ys[lastPointIndex+1] + XOffset);
            float afterY = axes.GetPixelY(Xs[lastPointIndex+1] + YOffset);
            Pixel afterPoint = new(afterX, afterY);
            return ([afterPoint], lastPointIndex);
        }
        else
        {
            return ([], MaximumIndex);
        }
    }
}
