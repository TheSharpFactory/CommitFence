using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace CF.App.Parts;

/// <summary>
/// The lines of any file, each with its number: the line in force marked, lines that do not fit wrapping with their rest set in 4
/// characters. The file rule's page and the screens that quote a file use it; the box round it is theirs.
/// </summary>
internal sealed partial class FileLines : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<FileLines, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<double> NumberWidthProperty = AvaloniaProperty.Register<FileLines, double>(nameof(NumberWidth), 52);

    public static readonly StyledProperty<double> NumberGapProperty = AvaloniaProperty.Register<FileLines, double>(nameof(NumberGap), 16);

    public static readonly StyledProperty<bool> BandsCurrentProperty = AvaloniaProperty.Register<FileLines, bool>(nameof(BandsCurrent), true);

    public static readonly StyledProperty<Thickness> GapMarginProperty = AvaloniaProperty.Register<FileLines, Thickness>(nameof(GapMargin), new Thickness(16, 0, 0, 0));

    static FileLines()
    {
        ItemsSourceProperty.Changed.AddClassHandler<FileLines>((lines, _) => lines.Rows.ItemsSource = lines.ItemsSource);
        NumberGapProperty.Changed.AddClassHandler<FileLines>((lines, _) => lines.SetValue(GapMarginProperty, new Thickness(lines.NumberGap, 0, 0, 0)));
    }

    public FileLines()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The lines, as <see cref="FileLineViewModel"/>.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get
        {
            return GetValue(ItemsSourceProperty);
        }
        set
        {
            SetValue(ItemsSourceProperty, value);
        }
    }

    /// <summary>
    /// The width of the column of numbers: 52, and 44 where the box is narrower.
    /// </summary>
    public double NumberWidth
    {
        get
        {
            return GetValue(NumberWidthProperty);
        }
        set
        {
            SetValue(NumberWidthProperty, value);
        }
    }

    /// <summary>
    /// The room between the numbers and the text: 16, and 14 where the box is narrower.
    /// </summary>
    public double NumberGap
    {
        get
        {
            return GetValue(NumberGapProperty);
        }
        set
        {
            SetValue(NumberGapProperty, value);
        }
    }

    /// <summary>
    /// Whether the line in force takes the background of hover over its whole row, wrapped rows too. The bar and the weight mark it
    /// either way.
    /// </summary>
    public bool BandsCurrent
    {
        get
        {
            return GetValue(BandsCurrentProperty);
        }
        set
        {
            SetValue(BandsCurrentProperty, value);
        }
    }

    /// <summary>
    /// The space the text keeps to the left, which is the room between the numbers and it.
    /// </summary>
    public Thickness GapMargin
    {
        get
        {
            return GetValue(GapMarginProperty);
        }
    }
}
