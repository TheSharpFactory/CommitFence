using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CF.App.Parts;

namespace CF.App.Architecture;

/// <summary>
/// Every direction written out: one group per layer, the top first, and in it one row per module with the modules it may
/// use as tokens. A use that breaks a direction is a red token, first in its row. The list does not edit; a row chooses
/// its module, and the list has one tab stop (the chosen module's row) that the arrow keys move.
/// Choosing a module only restyles the old and the new row (and the tokens that name them). The rows are made again only
/// when the structure changes, and each layer and each row is made when it comes near the visible part of the screen: until
/// then a place of about its height stands there, so a graph of hundreds of modules costs only what is looked at.
/// </summary>
internal sealed class DirectionList : UserControl
{
    private const double _near = 800;
    private const double _rowHeight = 36;

    private readonly StackPanel _stack = new StackPanel();
    private readonly Dictionary<string, ListRow> _rows = new Dictionary<string, ListRow>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Border>> _tokensOf = new Dictionary<string, List<Border>>(StringComparer.Ordinal);
    private readonly List<Place> _layers = new List<Place>();
    private ArchitectureViewModel? _model;
    private object? _shownReport;
    private object? _shownLayers;
    private object? _shownBreaches;
    private string _selected = string.Empty;
    private bool _onePane;

    public DirectionList()
    {
        Content = _stack;
    }

    /// <summary>A part of the list that is made when it comes near: until then it is an empty place of its estimated height.</summary>
    private sealed class Place : Border
    {
        private Func<Control>? _make;
        private bool _queued;

        public Place(Func<Control> make, double height)
        {
            _make = make;
            Height = height;
            EffectiveViewportChanged += (_, args) => OnViewport(args.EffectiveViewport);
        }

        public bool IsMade
        {
            get { return _make == null; }
        }

        public void Make()
        {
            if(_make == null)
                return;

            var make = _make;
            _make = null;
            ClearValue(HeightProperty);
            Child = make();
        }

        private void OnViewport(Rect viewport)
        {
            if(_make == null || _queued || viewport.Width <= 0 || viewport.Height <= 0 || viewport.Bottom < -_near || viewport.Top > Bounds.Height + _near)
                return;

            _queued = true;
            Dispatcher.UIThread.Post(Make, DispatcherPriority.Loaded);
        }
    }

    /// <summary>One module's row: what it holds, and its parts once it is made, so that choosing a module can restyle them in place.</summary>
    private sealed class ListRow
    {
        public ListRow(string name, int layer, List<string> uses, List<string> broken)
        {
            Name = name;
            Layer = layer;
            Uses = uses;
            Broken = broken;
        }

        public string Name { get; }

        public int Layer { get; }

        public List<string> Uses { get; }

        public List<string> Broken { get; }

        public Place Place { get; set; } = null!;

        public Button? Button { get; set; }

        public Border Shell { get; set; } = new Border();

        public Border Bar { get; set; } = new Border();

        public IDisposable? Shadow { get; set; }
    }

    public void Show(ArchitectureViewModel? model, bool onePane)
    {
        var same = model != null && IsVisible && _rows.Count > 0 && ReferenceEquals(_model, model) && _onePane == onePane &&
            ReferenceEquals(_shownReport, model.Report) && ReferenceEquals(_shownLayers, model.Layers) && ReferenceEquals(_shownBreaches, model.ActiveBreaches);
        _model = model;
        _onePane = onePane;
        if(same)
        {
            Choose(model!.SelectedName);
            return;
        }

        var hadFocus = _rows.Values.Any(row => row.Button?.IsFocused == true);
        _stack.Children.Clear();
        _rows.Clear();
        _tokensOf.Clear();
        _layers.Clear();
        _shownReport = null;
        if(model == null || !IsVisible)
            return;

        _shownReport = model.Report;
        _shownLayers = model.Layers;
        _shownBreaches = model.ActiveBreaches;
        _selected = model.SelectedName;
        Lay(model);
        if(_rows.TryGetValue(_selected, out var chosen))
        {
            Make(chosen);
            if(hadFocus)
                FocusRow(chosen);
        }
    }

    // A place for each layer, each holding a place for each of its rows; nothing is drawn yet.
    private void Lay(ArchitectureViewModel model)
    {
        var rank = model.Layers.Order.Select((name, index) => (name, index)).ToDictionary(pair => pair.name, pair => pair.index, StringComparer.Ordinal);
        var uses = model.Directions.ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
        var breaks = Breaches(model);
        for(var layer = 0; layer < model.Layers.Rows.Count; layer++)
        {
            var rows = model.Layers.Rows[layer].Select(name => new ListRow(name, layer, uses[name].OrderBy(use => rank.GetValueOrDefault(use)).ToList(), breaks[name].ToList())).ToList();
            foreach(var row in rows)
            {
                row.Place = new Place(() => Row(model, row), Estimate(row.Uses.Concat(row.Broken).ToList()));
                _rows.Add(row.Name, row);
            }

            var number = layer;
            var place = new Place(() => Group(number, rows), rows.Sum(row => row.Place.Height + 2) + 22);
            _layers.Add(place);
            _stack.Children.Add(place);
        }
    }

    // The chosen module changes: the old and the new row, and the tokens that name either, are restyled where they stand.
    private void Choose(string name)
    {
        if(name == _selected)
            return;

        var hadFocus = _rows.Values.Any(row => row.Button?.IsFocused == true);
        var old = _selected;
        _selected = name;
        foreach(var (module, chosen) in new List<(string, bool)> { (old, false), (name, true) })
        {
            if(_rows.TryGetValue(module, out var row) && row.Button != null)
                Decorate(row, chosen);

            foreach(var token in _tokensOf.GetValueOrDefault(module) ?? [])
                StyleToken(token, chosen);
        }

        if(_rows.TryGetValue(name, out var now))
        {
            Make(now);
            if(hadFocus)
                FocusRow(now);
        }
    }

    // The row, and the layer that holds it, are made now (the chosen module must be there to take the keyboard).
    private void Make(ListRow row)
    {
        _layers[row.Layer].Make();
        row.Place.Make();
    }

    private static void FocusRow(ListRow row)
    {
        Dispatcher.UIThread.Post(() =>
        {
            row.Button?.Focus(NavigationMethod.Directional);
            row.Button?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The distinct uses that break a direction, by the module that holds them.</summary>
    private static ILookup<string, string> Breaches(ArchitectureViewModel model)
    {
        return model.ActiveBreaches.Select(breach => (breach.FromModule, breach.ToModule)).Distinct()
            .ToLookup(pair => pair.FromModule, pair => pair.ToModule, StringComparer.Ordinal);
    }

    private static Border Hairline()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 4) };
        line.Bind(Border.BackgroundProperty, line.GetResourceObservable("line"));
        return line;
    }

    private Control Group(int layer, List<ListRow> members)
    {
        var label = ArchitectureVisuals.Text($"Layer {layer + 1}", "type-meta", "ink-2");
        label.VerticalAlignment = VerticalAlignment.Top;
        var rows = new StackPanel { Spacing = 2 };
        foreach(var row in members)
            rows.Children.Add(row.Place);

        Control group;
        if(_onePane)
        {
            label.Margin = new Thickness(14, 8, 0, 2);
            group = new StackPanel { Children = { label, rows } };
        }
        else
        {
            label.Margin = new Thickness(2, 13, 0, 0);
            rows.Margin = new Thickness(0, 4);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*"), ColumnSpacing = 8 };
            Grid.SetColumn(rows, 1);
            grid.Children.Add(label);
            grid.Children.Add(rows);
            group = grid;
        }

        return layer == 0 ? group : new StackPanel { Children = { Hairline(), group } };
    }

    private Panel Row(ArchitectureViewModel model, ListRow row)
    {
        var name = row.Name;
        var button = new Button
        {
            Height = double.NaN,
            MinHeight = _rowHeight,
            Padding = new Thickness(14, 5, 8, 5),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            IsTabStop = false,
            Tag = name,
            Classes = { "cf", "quiet", "arch-row" },
            Content = RowContent(row)
        };
        AutomationProperties.SetName(button, Sentence(name, row.Uses, row.Broken));
        button.Click += (_, _) => model.Choose(name);
        button.KeyDown += OnRowKey;
        var shell = new Border { CornerRadius = new CornerRadius(10), Child = button };
        var bar = ArchitectureVisuals.Fill("here", 4, double.NaN, 2);
        bar.HorizontalAlignment = HorizontalAlignment.Left;
        bar.Margin = new Thickness(4, 8, 0, 8);
        bar.IsHitTestVisible = false;
        bar.IsVisible = false;
        row.Button = button;
        row.Shell = shell;
        row.Bar = bar;
        Decorate(row, name == _selected);
        return new Panel { Children = { shell, bar } };
    }

    // The height a row will have, as near as can be told before it is made: how many lines its tokens take.
    private double Estimate(List<string> names)
    {
        if(names.Count == 0)
            return _rowHeight;

        var room = Math.Max(200, (Bounds.Width > 0 ? Bounds.Width : 700) - (_onePane ? 190 : 360));
        var average = (names.Sum(name => (name.Length * 7.5) + 24) / names.Count) + 6;
        var lines = Math.Ceiling(names.Count / Math.Max(1, Math.Floor(room / average)));
        return Math.Max(_rowHeight, (lines * 32) + 10);
    }

    private static void Decorate(ListRow row, bool chosen)
    {
        row.Button!.IsTabStop = chosen;
        row.Button.Classes.Set("chosen", chosen);
        row.Bar.IsVisible = chosen;
        row.Shadow?.Dispose();
        row.Shadow = null;
        if(chosen)
            row.Shadow = row.Shell.Bind(Border.BoxShadowProperty, row.Shell.GetResourceObservable("shadow-raised"));
        else
            row.Shell.ClearValue(Border.BoxShadowProperty);
    }

    private static string Sentence(string name, List<string> uses, List<string> broken)
    {
        var text = name + (uses.Count == 0 ? " may use no module" : " may use " + string.Join(", ", uses));
        return broken.Count == 0 ? text : text + "; it uses " + string.Join(", ", broken) + ", which breaks a direction";
    }

    private Grid RowContent(ListRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(_onePane ? "144,*" : "168,*"), ColumnSpacing = 12 };
        grid.Children.Add(NameOf(row.Name, row.Broken.Count > 0));
        var tokens = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        Grid.SetColumn(tokens, 1);
        foreach(var other in row.Broken)
            tokens.Children.Add(BreakToken(other));

        foreach(var use in row.Uses)
        {
            var token = Token(use, use == _selected);
            tokens.Children.Add(token);
            if(!_tokensOf.TryGetValue(use, out var named))
            {
                named = new List<Border>();
                _tokensOf.Add(use, named);
            }

            named.Add(token);
        }

        if(tokens.Children.Count == 0)
        {
            var none = ArchitectureVisuals.Text("Uses no module", "type-meta", "ink-2");
            none.Margin = new Thickness(0, 4, 0, 0);
            tokens.Children.Add(none);
        }

        grid.Children.Add(tokens);
        return grid;
    }

    private static Grid NameOf(string name, bool holdsBreach)
    {
        var text = ArchitectureVisuals.Text(name, "code-strong", "ink");
        text.TextWrapping = TextWrapping.Wrap;
        var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, MinHeight = 26, VerticalAlignment = VerticalAlignment.Top };
        if(holdsBreach)
        {
            var mark = new EffectMark { Width = 14, Height = 14, Kind = EffectKind.Block, VerticalAlignment = VerticalAlignment.Center, Classes = { "block" } };
            panel.Children.Add(mark);
        }

        Grid.SetColumn(text, 1);
        panel.Children.Add(text);
        if(!holdsBreach)
            panel.ColumnSpacing = 0;

        return panel;
    }

    /// <summary>A module a row may use: a small white token with the quiet edge; the chosen module's, edged in ink.</summary>
    private static Border Token(string name, bool ofChosen)
    {
        var token = new Border { Height = 26, CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 0) };
        token.Bind(Border.BackgroundProperty, token.GetResourceObservable("page"));
        token.Child = ArchitectureVisuals.Text(name, "code", "ink");
        StyleToken(token, ofChosen);
        return token;
    }

    // The token's edge and its type, in the chosen module's style or the plain one; the token itself stays.
    private static void StyleToken(Border token, bool ofChosen)
    {
        token.BorderThickness = new Thickness(ofChosen ? 1.5 : 1);
        token.Bind(Border.BorderBrushProperty, token.GetResourceObservable(ofChosen ? "ink" : "line-strong"));
        if(token.Child is TextBlock text)
            text.Bind(StyledElement.ThemeProperty, text.GetResourceObservable(ofChosen ? "code-strong" : "code"));
    }

    /// <summary>A use that breaks a direction: red, with the stop sign, the name of the module it may not use, a rule and "Breaks".</summary>
    private static Border BreakToken(string name)
    {
        var token = new Border { Height = 26, CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 0, 10, 0) };
        token.Bind(Border.BackgroundProperty, token.GetResourceObservable("block"));
        var mark = new EffectMark { Width = 14, Height = 14, Kind = EffectKind.Block, VerticalAlignment = VerticalAlignment.Center };
        mark.Bind(EffectMark.ShapeProperty, mark.GetResourceObservable("on-block"));
        mark.Bind(EffectMark.SignProperty, mark.GetResourceObservable("block"));
        var rule = ArchitectureVisuals.Fill("on-block", 1, 16, 0);
        rule.Margin = new Thickness(2, 0, 0, 0);
        rule.VerticalAlignment = VerticalAlignment.Center;
        var word = ArchitectureVisuals.Text("Breaks", "type-label", "on-block");
        token.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { mark, ArchitectureVisuals.Text(name, "code-strong", "on-block"), rule, word }
        };
        return token;
    }

    private void OnRowKey(object? sender, KeyEventArgs e)
    {
        if(_model == null)
            return;

        var order = _model.Layers.Order.ToList();
        var at = order.IndexOf(_model.SelectedName);
        int? target = null;
        if(e.Key == Key.Up)
            target = at - 1;
        else if(e.Key == Key.Down)
            target = at + 1;
        else if(e.Key == Key.Home)
            target = 0;
        else if(e.Key == Key.End)
            target = order.Count - 1;

        if(target == null)
            return;

        e.Handled = true;
        if(target >= 0 && target < order.Count)
            _model.Choose(order[target.Value]);
    }
}
