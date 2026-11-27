using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// A path on one line in the code face: the folders in one brush, the file name in bold in another. It is cut at folder
/// boundaries from the left to the width it is given, so the name is always whole (see <see cref="PathCut"/>).
/// </summary>
internal sealed class PathLine : Control
{
    private const double _lineHeight = 20;

    public static readonly StyledProperty<string> PathProperty = AvaloniaProperty.Register<PathLine, string>(nameof(Path), string.Empty);

    public static readonly StyledProperty<IBrush?> FolderBrushProperty = AvaloniaProperty.Register<PathLine, IBrush?>(nameof(FolderBrush));

    public static readonly StyledProperty<IBrush?> NameBrushProperty = AvaloniaProperty.Register<PathLine, IBrush?>(nameof(NameBrush));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<PathLine>();

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<PathLine>();

    static PathLine()
    {
        AffectsMeasure<PathLine>(PathProperty, FontFamilyProperty, FontSizeProperty);
        AffectsRender<PathLine>(PathProperty, FolderBrushProperty, NameBrushProperty, FontFamilyProperty, FontSizeProperty);
    }

    public string Path
    {
        get { return GetValue(PathProperty); }
        set { SetValue(PathProperty, value); }
    }

    public IBrush? FolderBrush
    {
        get { return GetValue(FolderBrushProperty); }
        set { SetValue(FolderBrushProperty, value); }
    }

    public IBrush? NameBrush
    {
        get { return GetValue(NameBrushProperty); }
        set { SetValue(NameBrushProperty, value); }
    }

    public FontFamily FontFamily
    {
        get { return GetValue(FontFamilyProperty); }
        set { SetValue(FontFamilyProperty, value); }
    }

    public double FontSize
    {
        get { return GetValue(FontSizeProperty); }
        set { SetValue(FontSizeProperty, value); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var whole = Measure(Path, false);
        var width = double.IsInfinity(availableSize.Width) ? whole : Math.Min(whole, availableSize.Width);
        return new Size(width, _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var parts = PathCut.Fit(Path, Bounds.Width, Measure);
        var top = (Bounds.Height - Text(parts.Name, true, Brushes.Black).Height) / 2;
        var folders = Text(parts.Folders, false, FolderBrush);
        context.DrawText(folders, new Point(0, top));
        context.DrawText(Text(parts.Name, true, NameBrush), new Point(folders.WidthIncludingTrailingWhitespace, top));
    }

    private double Measure(string text, bool bold)
    {
        return text.Length == 0 ? 0 : Text(text, bold, Brushes.Black).WidthIncludingTrailingWhitespace;
    }

    private FormattedText Text(string text, bool bold, IBrush? brush)
    {
        var typeface = new Typeface(FontFamily, FontStyle.Normal, bold ? FontWeight.SemiBold : FontWeight.Normal);
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, brush);
    }
}
