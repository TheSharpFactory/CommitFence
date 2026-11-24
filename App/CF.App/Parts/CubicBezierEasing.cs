using Avalonia.Animation.Easings;

namespace CF.App.Parts;

/// <summary>
/// An easing given as a CSS-style cubic Bezier curve. The spring has a control point above 1, which the
/// framework's spline easing refuses, so the same cubic is evaluated here.
/// </summary>
internal sealed class CubicBezierEasing : Easing
{
    private const int _iterations = 24;

    private readonly double _x1;
    private readonly double _y1;
    private readonly double _x2;
    private readonly double _y2;

    public CubicBezierEasing(double x1, double y1, double x2, double y2)
    {
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    /// <summary>
    /// The <c>ease</c> curve: cubic-bezier(0.2, 0, 0, 1).
    /// </summary>
    public static CubicBezierEasing Standard { get; } = new CubicBezierEasing(0.2, 0, 0, 1);

    /// <summary>
    /// The <c>spring</c> curve: cubic-bezier(0.34, 1.4, 0.64, 1).
    /// </summary>
    public static CubicBezierEasing Spring { get; } = new CubicBezierEasing(0.34, 1.4, 0.64, 1);

    public override double Ease(double progress)
    {
        if(progress <= 0)
            return 0;

        if(progress >= 1)
            return 1;

        return Sample(_y1, _y2, ParameterAt(progress));
    }

    private double ParameterAt(double x)
    {
        var low = 0.0;
        var high = 1.0;
        for(var index = 0; index < _iterations; index++)
        {
            var middle = (low + high) / 2;
            if(Sample(_x1, _x2, middle) < x)
                low = middle;
            else
                high = middle;
        }

        return (low + high) / 2;
    }

    private static double Sample(double first, double second, double t)
    {
        var inverse = 1 - t;
        return (3 * inverse * inverse * t * first) + (3 * inverse * t * t * second) + (t * t * t);
    }
}
