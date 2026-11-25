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
    private readonly Canvas _canvas = new Canvas();
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
        Content = _scroll;
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

    private void Draw()
    {
        if(_model?.Selected is not { } selected || Bounds.Width <= 0)
            return;

        var hadFocus = _buttons.Values.Any(button => button.IsFocused);
        _canvas.Children.Clear();
        _buttons.Clear();
        var breach = _model.DrawnBreach;
        _layout = new DiagramLayout(_model.Layers, selected.Name, _model.Directions, breach, Bounds.Width);
        _canvas.Width = _layout.Width;
        _canvas.Height = _layout.Height;
        foreach(var line in _layout.Lines)
            AddLine(line);

        var linked = _layout.Lines.SelectMany(line => new List<string> { line.Direction.From, line.Direction.To }).ToHashSet(StringComparer.Ordinal);
        foreach(var box in _layout.Boxes.Values)
            AddModule(box, box.Name == selected.Name, linked.Contains(box.Name), breach?.FromModule == box.Name);

        WidthNeeded?.Invoke(_layout.Width > Bounds.Width + .5);
        var chosen = _buttons[selected.Name];
        if(hadFocus)
            chosen.Focus(NavigationMethod.Directional);

        if(_layout.Width > Bounds.Width + .5)
            Dispatcher.UIThread.Post(() => Centre(chosen), DispatcherPriority.Loaded);
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
        var button = new Button { Width = box.Width, Height = DiagramLayout.BoxHeight, IsTabStop = current, Tag = box.Name };
        button.Classes.Add("cf");
        button.Classes.Add("arch-module");
        button.Classes.Set("current", current);
        button.Classes.Set("unlinked", !current && !linked);
        AutomationProperties.SetName(button, box.Name);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        if(breaks)
            content.Children.Add(new EffectMark { Width = 14, Height = 14, Kind = EffectKind.Block, Classes = { "block" } });

        content.Children.Add(ArchitectureVisuals.Text(box.Name, current || linked ? "code-strong" : "code", current || linked ? "ink" : "ink-2"));
        if(current)
            content.Margin = new Thickness(5, 0, 0, 0);

        button.Content = content;
        Canvas.SetLeft(button, box.X);
        Canvas.SetTop(button, box.Y);
        button.Click += (_, _) => _model?.Choose(box.Name);
        button.KeyDown += OnModuleKey;
        _buttons.Add(box.Name, button);
        _canvas.Children.Add(button);
        if(current)
        {
            var bar = ArchitectureVisuals.Fill("here", 4, 20, 2);
            bar.IsHitTestVisible = false;
            Canvas.SetLeft(bar, box.X + 5);
            Canvas.SetTop(bar, box.Y + 8);
            _canvas.Children.Add(bar);
        }
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
        var brush = line.Kind == DiagramLineKind.Breach ? "block" : line.Kind == DiagramLineKind.Incoming ? "ink-2" : "ink";
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = PathOf(line),
            StrokeThickness = line.Kind == DiagramLineKind.Breach ? 3 : line.Kind == DiagramLineKind.Incoming ? 1.75 : 2,
            StrokeJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        };
        path.Bind(Shape.StrokeProperty, path.GetResourceObservable(brush));
        if(line.Kind == DiagramLineKind.Incoming)
            path.StrokeDashArray = new AvaloniaList<double> { 6 / 1.75, 3 / 1.75 };

        _canvas.Children.Add(path);
        AddArrow(line, brush);
        if(line.Kind == DiagramLineKind.Breach)
            AddBreachLabel(line.Points);
    }

    private static StreamGeometry PathOf(DiagramLine line)
    {
        var geometry = new StreamGeometry();
        using var drawn = geometry.Open();
        var points = line.Points;
        drawn.BeginFigure(new Point(points[0].X, points[0].Y), false);
        if(line.Kind == DiagramLineKind.Breach)
        {
            RoundCorners(drawn, points);
            drawn.EndFigure(false);
            return geometry;
        }

        for(var i = 1; i < points.Count; i++)
        {
            var start = points[i - 1];
            var end = points[i];
            if(Math.Abs(start.X - end.X) < .5)
            {
                drawn.LineTo(new Point(end.X, end.Y));
                continue;
            }

            var middle = (start.Y + end.Y) / 2;
            drawn.CubicBezierTo(new Point(start.X, middle), new Point(end.X, middle), new Point(end.X, end.Y));
        }

        drawn.EndFigure(false);
        return geometry;
    }

    private static void RoundCorners(StreamGeometryContext drawn, IReadOnlyList<DiagramPoint> points)
    {
        for(var i = 1; i < points.Count - 1; i++)
        {
            var corner = new Point(points[i].X, points[i].Y);
            var before = new Vector(points[i - 1].X - corner.X, points[i - 1].Y - corner.Y);
            var after = new Vector(points[i + 1].X - corner.X, points[i + 1].Y - corner.Y);
            if(before.Length == 0 || after.Length == 0)
                continue;

            var radius = Math.Min(12, Math.Min(before.Length, after.Length) / 2);
            drawn.LineTo(corner + (before * (radius / before.Length)));
            drawn.QuadraticBezierTo(corner, corner + (after * (radius / after.Length)));
        }

        drawn.LineTo(new Point(points[^1].X, points[^1].Y));
    }

    private void AddArrow(DiagramLine line, string brush)
    {
        var last = line.Points[^1];
        var arrow = new Polygon { IsHitTestVisible = false };
        arrow.Bind(Shape.FillProperty, arrow.GetResourceObservable(brush));
        if(line.Kind == DiagramLineKind.Breach)
            arrow.Points = new List<Point> { new Point(last.X, last.Y - 6), new Point(last.X, last.Y + 6), new Point(last.X - 10, last.Y) };
        else
            arrow.Points = new List<Point> { new Point(last.X - 4.5, last.Y), new Point(last.X + 4.5, last.Y), new Point(last.X, last.Y + (line.ArrowUp ? -8 : 8)) };

        _canvas.Children.Add(arrow);
    }

    private void AddBreachLabel(IReadOnlyList<DiagramPoint> points)
    {
        var right = points.Max(point => point.X);
        var onLane = points.Where(point => point.X == right).ToList();
        var label = ArchitectureVisuals.Fill("block", 54, 22, 6);
        var words = ArchitectureVisuals.Text("Breaks", "type-meta-strong", "on-block");
        words.FontSize = 12;
        words.HorizontalAlignment = HorizontalAlignment.Center;
        label.Child = words;
        label.IsHitTestVisible = false;
        Canvas.SetLeft(label, right - 27);
        Canvas.SetTop(label, (onLane.Average(point => point.Y)) - 11);
        _canvas.Children.Add(label);
    }
}
