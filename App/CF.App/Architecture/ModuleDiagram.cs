using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CF.App.Parts;

namespace CF.App.Architecture;

/// <summary>The diagram gives the selected module the keyboard and draws the layout's paths underneath the buttons.</summary>
internal sealed class ModuleDiagram : UserControl
{
    // The rows' numbers stand in a strip 16 wide at the left, 12 before the canvas; the strip does not scroll with the canvas.
    private const double _stripWidth = 16;
    private const double _stripGap = 12;
    private const double _numberDrop = 9;

    private readonly Canvas _canvas = new Canvas();
    private readonly Canvas _numbers = new Canvas { Width = _stripWidth };
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
    private ArchitectureViewModel? _model;
    private DiagramLayout? _layout;

    public ModuleDiagram()
    {
        _scroll = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            AllowAutoHide = false
        };
        var frame = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(_stripWidth, GridUnitType.Pixel), new ColumnDefinition(_stripGap, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star) } };
        Grid.SetColumn(_scroll, 2);
        frame.Children.Add(_numbers);
        frame.Children.Add(_scroll);
        Content = frame;
        SizeChanged += (_, args) =>
        {
            if(args.WidthChanged)
                Draw();
        };
    }

    public event Action<bool>? WidthNeeded;

    public void Show(ArchitectureViewModel? model)
    {
        _model = model;
        if(IsVisible)
            Draw();
    }

    private double Viewport
    {
        get { return Bounds.Width - _stripWidth - _stripGap; }
    }

    private void Draw()
    {
        if(_model?.Selected is not { } selected || Viewport <= 0)
            return;

        var hadFocus = _buttons.Values.Any(button => button.IsFocused);
        _canvas.Children.Clear();
        _buttons.Clear();
        var breach = _model.ActiveBreaches.FirstOrDefault(item => item.FromModule == selected.Name || item.ToModule == selected.Name) ?? _model.DrawnBreach;
        _layout = new DiagramLayout(_model.Layers, selected.Name, _model.Directions, breach, Viewport);
        _canvas.Width = _layout.Width;
        _canvas.Height = _layout.Height;
        AddNumbers(_layout);
        foreach(var line in _layout.Lines)
            AddLine(line);

        var linked = _layout.Lines.SelectMany(line => new List<string> { line.Direction.From, line.Direction.To }).ToHashSet(StringComparer.Ordinal);
        foreach(var box in _layout.Boxes.Values)
            AddModule(box, box.Name == selected.Name, linked.Contains(box.Name), breach?.FromModule == box.Name);

        WidthNeeded?.Invoke(_layout.Width > Viewport + .5);
        var chosen = _buttons[selected.Name];
        if(hadFocus)
            chosen.Focus(NavigationMethod.Directional);

        if(_layout.Width > Viewport + .5)
            Dispatcher.UIThread.Post(() => Centre(chosen), DispatcherPriority.Loaded);
    }

    private void AddNumbers(DiagramLayout layout)
    {
        _numbers.Children.Clear();
        _numbers.Height = layout.Height;
        foreach(var layer in layout.Layers)
        {
            var number = ArchitectureVisuals.Text(layer.Number.ToString(CultureInfo.InvariantCulture), "type-meta", layer.HoldsChosen ? "ink" : "ink-2");
            number.FontFeatures = FontFeatureCollection.Parse("tnum");
            number.FontWeight = layer.HoldsChosen ? FontWeight.SemiBold : FontWeight.Normal;
            number.IsHitTestVisible = false;
            AutomationProperties.SetAccessibilityView(number, AccessibilityView.Raw);
            Canvas.SetLeft(number, 0);
            Canvas.SetTop(number, layer.Top + _numberDrop);
            _numbers.Children.Add(number);
        }
    }

    private void Centre(Button button)
    {
        if(button.Parent != _canvas)
            return;

        var offset = Canvas.GetLeft(button) + (button.Width / 2) - (_scroll.Viewport.Width / 2);
        _scroll.Offset = new Vector(Math.Clamp(offset, 0, Math.Max(0, _scroll.Extent.Width - _scroll.Viewport.Width)), 0);
        button.BringIntoView();
    }

    private void AddModule(ModuleBox box, bool current, bool linked, bool breaks)
    {
        var button = new Button { Width = box.Width, Height = DiagramLayout.BoxHeight, IsTabStop = current, Tag = box.Name, Padding = new Thickness(10, 0) };
        button.Classes.Add("cf");
        button.Classes.Add("arch-module");
        button.Classes.Set("current", current);
        button.Classes.Set("unlinked", !current && !linked);
        AutomationProperties.SetName(button, $"{box.Name}, layer {box.Row + 1}");
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        if(breaks)
            content.Children.Add(new EffectMark { Width = 14, Height = 14, Kind = EffectKind.Block, Classes = { "block" } });

        content.Children.Add(ArchitectureVisuals.Text(box.Name, current || linked ? "code-strong" : "code", "ink"));
        button.Content = content;
        Canvas.SetLeft(button, box.X);
        Canvas.SetTop(button, box.Y);
        button.Click += (_, _) => _model?.Choose(box.Name);
        button.KeyDown += OnModuleKey;
        _buttons.Add(box.Name, button);
        _canvas.Children.Add(button);
    }

    private void OnModuleKey(object? sender, KeyEventArgs e)
    {
        var horizontal = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        var vertical = e.Key == Key.Up ? -1 : e.Key == Key.Down ? 1 : 0;
        if(horizontal == 0 && vertical == 0)
            return;

        if(_model != null && _layout?.Nearest(_model.SelectedName, horizontal, vertical) is { } nearest)
            _model.Choose(nearest);

        e.Handled = true;
    }

    private void AddLine(DiagramLine line)
    {
        var breach = line.Kind == DiagramLineKind.Breach;
        var brush = breach ? "block" : line.Kind == DiagramLineKind.Incoming ? "ink-2" : "ink";
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = PathOf(line),
            StrokeThickness = breach ? 3 : line.Kind == DiagramLineKind.Incoming ? 1.75 : 2,
            StrokeJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        path.Bind(Shape.StrokeProperty, path.GetResourceObservable(brush));
        if(line.Kind == DiagramLineKind.Incoming)
            path.StrokeDashArray = new AvaloniaList<double> { 6 / 1.75, 3 / 1.75 };

        _canvas.Children.Add(path);
        AddArrow(line, brush);
        if(line.Word is { } word)
            AddBreachLabel(word);
    }

    /// <summary>The line's corners, each rounded by the line's radius (less where a run is too short for it).</summary>
    private static StreamGeometry PathOf(DiagramLine line)
    {
        var geometry = new StreamGeometry();
        using var drawn = geometry.Open();
        var points = line.Points;
        drawn.BeginFigure(new Point(points[0].X, points[0].Y), false);
        for(var i = 1; i < points.Count - 1; i++)
        {
            var corner = new Point(points[i].X, points[i].Y);
            var before = new Vector(points[i - 1].X - corner.X, points[i - 1].Y - corner.Y);
            var after = new Vector(points[i + 1].X - corner.X, points[i + 1].Y - corner.Y);
            if(before.Length == 0 || after.Length == 0)
                continue;

            var radius = Math.Min(line.Radius, Math.Min(before.Length, after.Length) / 2);
            drawn.LineTo(corner + (before * (radius / before.Length)));
            drawn.QuadraticBezierTo(corner, corner + (after * (radius / after.Length)));
        }

        drawn.LineTo(new Point(points[^1].X, points[^1].Y));
        drawn.EndFigure(false);
        return geometry;
    }

    private void AddArrow(DiagramLine line, string brush)
    {
        var tip = line.Arrow.Tip;
        var breach = line.Kind == DiagramLineKind.Breach;
        var half = breach ? 6 : 4.5;
        var length = breach ? 10 : 8;
        var arrow = new Polygon { IsHitTestVisible = false };
        arrow.Bind(Shape.FillProperty, arrow.GetResourceObservable(brush));
        arrow.Points = line.Arrow.Pointing switch
        {
            ArrowPointing.Left => new List<Point> { new Point(tip.X + length, tip.Y - half), new Point(tip.X + length, tip.Y + half), new Point(tip.X, tip.Y) },
            ArrowPointing.Right => new List<Point> { new Point(tip.X - length, tip.Y - half), new Point(tip.X - length, tip.Y + half), new Point(tip.X, tip.Y) },
            ArrowPointing.Up => new List<Point> { new Point(tip.X - half, tip.Y + length), new Point(tip.X + half, tip.Y + length), new Point(tip.X, tip.Y) },
            ArrowPointing.Down => new List<Point> { new Point(tip.X - half, tip.Y - length), new Point(tip.X + half, tip.Y - length), new Point(tip.X, tip.Y) },
            _ => throw new InvalidOperationException("An arrow points left, right, up or down.")
        };
        _canvas.Children.Add(arrow);
    }

    /// <summary>The word "Breaks" in a 54 by 22 shape, set in an edge of the panel's colour that parts it from any line beneath.</summary>
    private void AddBreachLabel(DiagramPoint centre)
    {
        var edge = ArchitectureVisuals.Fill("side", 60, 28, 9);
        edge.IsHitTestVisible = false;
        Canvas.SetLeft(edge, centre.X - 30);
        Canvas.SetTop(edge, centre.Y - 14);
        _canvas.Children.Add(edge);
        var label = ArchitectureVisuals.Fill("block", 54, 22, 6);
        var words = ArchitectureVisuals.Text("Breaks", "type-meta-strong", "on-block");
        words.FontSize = 12;
        words.HorizontalAlignment = HorizontalAlignment.Center;
        label.Child = words;
        label.IsHitTestVisible = false;
        Canvas.SetLeft(label, centre.X - 27);
        Canvas.SetTop(label, centre.Y - 11);
        _canvas.Children.Add(label);
    }
}
