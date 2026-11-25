using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CF.Core.Services;

namespace CF.App.Architecture;

/// <summary>A scrollable direction matrix. Only cells and headings in the viewport have controls.</summary>
internal sealed class DirectionTable : UserControl
{
    private const double _headHeight = 136;
    private const double _endSpace = 80;

    private readonly Canvas _headers = new Canvas();
    private readonly Canvas _matrix = new Canvas { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ScrollViewer _scroll;
    private readonly Dictionary<(int Row, int Column), Control> _tiles = new Dictionary<(int, int), Control>();
    private ArchitectureViewModel? _model;
    private IReadOnlyList<string> _names = [];
    private HashSet<StructureDirection> _allowed = new HashSet<StructureDirection>();
    private HashSet<StructureDirection> _broken = new HashSet<StructureDirection>();
    private double _size;
    private double _mark;
    private Rect _viewport;
    private (int Row, int Column) _cursor;

    public DirectionTable()
    {
        _scroll = new ScrollViewer
        {
            Content = _matrix,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            AllowAutoHide = false
        };
        var frame = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        frame.Children.Add(_headers);
        Grid.SetColumn(_scroll, 1);
        frame.Children.Add(_scroll);
        Content = frame;
        _matrix.EffectiveViewportChanged += (_, args) =>
        {
            _viewport = args.EffectiveViewport;
            Realize();
        };
    }

    public void Show(ArchitectureViewModel? model)
    {
        _model = model;
        if(model == null || !IsVisible)
            return;

        var focused = _tiles.TryGetValue(_cursor, out var current) && current.IsFocused;
        var from = _names.Count > _cursor.Row ? _names[_cursor.Row] : model.SelectedName;
        var to = _names.Count > _cursor.Column ? _names[_cursor.Column] : model.SelectedName;
        _names = model.Layers.Order;
        _allowed = model.Directions.ToHashSet();
        _broken = model.ActiveBreaches.Select(breach => new StructureDirection(breach.FromModule, breach.ToModule)).ToHashSet();
        _size = _names.Count > 40 ? 20 : _names.Count > 20 ? 26 : 32;
        _mark = _names.Count > 40 ? 8 : _names.Count > 20 ? 10 : 12;
        _headers.Width = Math.Min(280, (_names.Select(name => name.Length).DefaultIfEmpty(0).Max() * 8.5) + 26);
        _headers.Height = _matrix.Height = _headHeight + (_size * _names.Count);
        _matrix.Width = (_size * _names.Count) + _endSpace;
        _cursor = (IndexOf(from), IndexOf(to));
        _matrix.Children.Clear();
        _headers.Children.Clear();
        _tiles.Clear();
        Realize();
        if(focused && model.CanEdit)
            FocusCell();
    }

    private int IndexOf(string name)
    {
        for(var i = 0; i < _names.Count; i++)
        {
            if(_names[i] == name)
                return i;
        }

        return 0;
    }

    private void Realize()
    {
        if(_model == null || _names.Count == 0 || !IsVisible)
            return;

        var visible = new HashSet<(int, int)> { _cursor };
        var firstColumn = Math.Max(0, IndexAt(_viewport.Left) - 1);
        var lastColumn = IndexAt(_viewport.Right);
        var firstRow = Math.Max(0, IndexAt(_viewport.Top - _headHeight) - 1);
        var lastRow = IndexAt(_viewport.Bottom - _headHeight);
        for(var column = firstColumn; column <= lastColumn; column++)
            visible.Add((-1, column));

        for(var row = firstRow; row <= lastRow; row++)
        {
            visible.Add((row, -1));
            for(var column = firstColumn; column <= lastColumn; column++)
                visible.Add((row, column));
        }

        foreach(var key in _tiles.Keys.Where(key => !visible.Contains(key)).ToList())
        {
            (key.Column == -1 ? _headers : _matrix).Children.Remove(_tiles[key]);
            _tiles.Remove(key);
        }

        foreach(var key in visible.Where(key => !_tiles.ContainsKey(key)).OrderBy(key => key.Item1).ThenBy(key => key.Item2))
            AddTile(key);
    }

    private int IndexAt(double offset)
    {
        if(double.IsNaN(offset) || offset <= 0)
            return 0;

        return (int)Math.Min(_names.Count - 1, Math.Floor(offset / _size) + 1);
    }

    private void AddTile((int Row, int Column) key)
    {
        Control tile = key switch
        {
            (-1, _) => ColumnLabel(key.Column),
            (_, -1) => RowLabel(key.Row),
            _ => Cell(key)
        };
        var canvas = key.Column == -1 ? _headers : _matrix;
        Canvas.SetTop(tile, key.Row == -1 ? 0 : _headHeight + (key.Row * _size));
        Canvas.SetLeft(tile, key.Column == -1 ? 0 : key.Column * _size);
        _tiles.Add(key, tile);
        canvas.Children.Add(tile);
    }

    private TextBlock Label(string name)
    {
        var label = ArchitectureVisuals.Text(name, name == _model!.SelectedName ? "code-strong" : "code", "ink");
        label.FontSize = 14;
        return label;
    }

    private Canvas ColumnLabel(int column)
    {
        var label = Label(_names[column]);
        label.RenderTransform = new RotateTransform(-60);
        label.RenderTransformOrigin = new RelativePoint(0, 1, RelativeUnit.Relative);
        var canvas = new Canvas { Width = _size, Height = _headHeight, IsHitTestVisible = false };
        Canvas.SetTop(label, _headHeight - 28);
        Canvas.SetLeft(label, _size / 2);
        canvas.Children.Add(label);
        return canvas;
    }

    private Button RowLabel(int row)
    {
        var name = _names[row];
        var label = Label(name);
        label.Margin = new Thickness(12, 0, 14, 0);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        var panel = new Panel();
        panel.Children.Add(label);
        if(name == _model!.SelectedName)
        {
            var bar = ArchitectureVisuals.Fill("here", 4, _size - 12, 2);
            bar.HorizontalAlignment = HorizontalAlignment.Left;
            bar.Margin = new Thickness(2, 0, 0, 0);
            panel.Children.Add(bar);
        }

        var button = new Button
        {
            Content = panel,
            Classes = { "cf", "quiet" },
            Height = _size,
            Width = _headers.Width,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsTabStop = false
        };
        ToolTip.SetTip(button, name);
        AutomationProperties.SetName(button, "Choose " + name);
        button.Click += (_, _) => _model?.Choose(name);
        return button;
    }

    private Button Cell((int Row, int Column) key)
    {
        var from = _names[key.Row];
        var to = _names[key.Column];
        var direction = new StructureDirection(from, to);
        var allowed = _allowed.Contains(direction);
        var broken = _broken.Contains(direction);
        var model = _model!;
        var button = new Button
        {
            Width = _size,
            Height = _size,
            IsTabStop = key == _cursor,
            IsEnabled = model.CanEdit,
            Classes = { "cf", "arch-cell" },
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        button.Bind(TemplatedControl.BackgroundProperty, button.GetResourceObservable(from == model.SelectedName || to == model.SelectedName ? "hover" : "page"));
        button.Content = CellFace(key, Mark(from == to, allowed, broken));
        button.Tag = key;
        AutomationProperties.SetName(button, from == to ? from : $"{from} to {to}: {(broken ? "breaks" : allowed ? "allowed" : "not allowed")}");
        ToolTip.SetTip(button, model.DirectionRefusal(from, to) ?? (allowed ? $"Stop allowing {from} to use {to}" : $"Allow {from} to use {to}"));
        button.Click += (_, _) =>
        {
            _cursor = key;
            _model?.ToggleDirection(from, to);
        };
        button.KeyDown += OnCellKey;
        return button;
    }

    private Panel CellFace((int Row, int Column) key, Control? mark)
    {
        var face = new Panel { IsHitTestVisible = false };
        var layerColumn = IsLayerStart(key.Column);
        var layerRow = IsLayerStart(key.Row);
        var left = ArchitectureVisuals.Fill(layerColumn ? "ink-3" : "line", layerColumn ? 2 : 1, _size, 0);
        left.HorizontalAlignment = HorizontalAlignment.Left;
        var top = ArchitectureVisuals.Fill(layerRow ? "ink-3" : "line", _size, layerRow ? 2 : 1, 0);
        top.VerticalAlignment = VerticalAlignment.Top;
        face.Children.Add(left);
        face.Children.Add(top);
        if(key.Column == _names.Count - 1)
        {
            var right = ArchitectureVisuals.Fill("line", 1, _size, 0);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            face.Children.Add(right);
        }

        if(key.Row == _names.Count - 1)
        {
            var bottom = ArchitectureVisuals.Fill("line", _size, 1, 0);
            bottom.VerticalAlignment = VerticalAlignment.Bottom;
            face.Children.Add(bottom);
        }

        if(mark != null)
            face.Children.Add(mark);

        return face;
    }

    private bool IsLayerStart(int index)
    {
        return index == 0 || _model!.Layers.Level[_names[index]] != _model.Layers.Level[_names[index - 1]];
    }

    private Border? Mark(bool self, bool allowed, bool broken)
    {
        if(self)
            return ArchitectureVisuals.Fill("ink-3", 8, 1.5, 0);

        if(!broken)
            return allowed ? ArchitectureVisuals.Fill("solid", _mark, _mark, 3) : null;

        var mark = ArchitectureVisuals.Fill("block", _mark + 2, _mark + 2, 3);
        var bar = ArchitectureVisuals.Fill("on-block", _mark - 4, 2, 1);
        bar.HorizontalAlignment = HorizontalAlignment.Center;
        bar.VerticalAlignment = VerticalAlignment.Center;
        mark.Child = bar;
        return mark;
    }

    private void OnCellKey(object? sender, KeyEventArgs e)
    {
        var (row, column) = _cursor;
        switch(e.Key)
        {
            case Key.Left:
                column--;
                break;
            case Key.Right:
                column++;
                break;
            case Key.Up:
                row--;
                break;
            case Key.Down:
                row++;
                break;
            case Key.Home:
                column = 0;
                break;
            case Key.End:
                column = _names.Count - 1;
                break;
            default:
                return;
        }

        if(_tiles.TryGetValue(_cursor, out var old))
            old.IsTabStop = false;

        _cursor = (Math.Clamp(row, 0, _names.Count - 1), Math.Clamp(column, 0, _names.Count - 1));
        Realize();
        FocusCell();
        e.Handled = true;
    }

    private void FocusCell()
    {
        var tile = _tiles[_cursor];
        tile.IsTabStop = true;
        tile.Focus(NavigationMethod.Directional);
        tile.BringIntoView();
        Dispatcher.UIThread.Post(() =>
        {
            if(tile.Parent == _matrix)
                tile.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}
