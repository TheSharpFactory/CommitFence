using Avalonia;

namespace CF.App.Parts;

/// <summary>
/// The measures of the page every screen stands in: when it has two columns, how wide a frame may be, and where the panel
/// that stays in view belongs while the sheet scrolls. Review and the five other screens read them from here.
/// </summary>
internal static class PageLayout
{
    public const double RailWidth = 88;
    public const double TwoPanesFrom = 1304;
    public const double TwoColumnsMost = 1292;
    public const double OneColumnMost = 760;
    public const double PanelWidth = 340;
    public const double ColumnGap = 32;
    public const double SheetTop = 24;
    public const double SheetBottom = 32;
    public const double ScrollBarWidth = 12;
    public const double WheelStep = 50;

    private const double _blockMiddleAt = 0.4;
    private const double _blockTopLeast = 32;

    /// <summary>
    /// Whether a window of this width shows two columns: from 1304 wide, counting the rail.
    /// </summary>
    public static bool IsTwoPanes(double windowWidth)
    {
        return windowWidth >= TwoPanesFrom;
    }

    /// <summary>
    /// The width of the page, the part of the window right of the rail, turned into the window's width.
    /// </summary>
    public static double WindowWidthOf(double pageWidth)
    {
        return pageWidth + RailWidth;
    }

    /// <summary>
    /// The room at each side of the sheet and of everything that stands in its frame.
    /// </summary>
    public static double Side(bool twoPanes)
    {
        return twoPanes ? 32 : 24;
    }

    /// <summary>
    /// The widest the frame may be: the two columns, or the one.
    /// </summary>
    public static double FrameMost(bool twoPanes)
    {
        return twoPanes ? TwoColumnsMost : OneColumnMost;
    }

    /// <summary>
    /// How far a panel that stays in view is moved up while the sheet is scrolled to <paramref name="offset"/>: not at all until
    /// the end of the sheet comes up under it, and then with the sheet (so a panel higher than its room is seen to its end).
    /// </summary>
    public static double PanelShift(double offset, double extentHeight, double panelHeight)
    {
        var most = Math.Max(0, extentHeight - SheetTop - SheetBottom - panelHeight);
        return -Math.Max(0, offset - most);
    }

    /// <summary>
    /// Where the block of a screen with nothing to show starts: its middle at 40 percent of the room's height, its top at
    /// least 32 below the room's top.
    /// </summary>
    public static double EmptyBlockTop(double roomHeight, double blockHeight)
    {
        return Math.Max(_blockTopLeast, (roomHeight * _blockMiddleAt) - (blockHeight / 2));
    }

    /// <summary>
    /// The room at each side of a sheet's row once the scroll bar has taken its part of the right side.
    /// </summary>
    public static Thickness SheetPadding(bool twoPanes)
    {
        var side = Side(twoPanes);
        return new Thickness(side, 0, side - ScrollBarWidth, 0);
    }
}
