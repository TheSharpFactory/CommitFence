using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CF.App.Parts;
using CF.App.Rules.Page;
using CF.Core.Services;

namespace CF.App.Architecture;

/// <summary>Connects the structure editor, module picker, diagram and direction table to the screen frame.</summary>
internal sealed partial class ArchitectureView : UserControl
{
    private ArchitectureViewModel? _model;
    private readonly EmptyBlock _empty;
    private string? _partKind;
    private string? _partModule;
    private bool _editing;
    private bool _diagramWider;

    public ArchitectureView()
    {
        InitializeComponent();
        _empty = MakeEmpty();
        DataContextChanged += (_, _) => FollowModel();
        Frame.SizeChanged += (_, _) =>
        {
            if(!Frame.IsOnePane && ReferenceEquals(PickerPopup.PlacementTarget, SwitchModule))
                PickerPopup.IsOpen = false;

            Fit();
        };
        PickerPopup.Closed += (_, _) => SwitchModule.Classes.Remove("open");
        ModulePanel.SizeChanged += (_, _) => FitPanel();
        Diagram.WidthNeeded += wider =>
        {
            _diagramWider = wider;
            ScrollCue.IsVisible = Diagram.IsVisible && wider;
        };
        AddHandler(KeyDownEvent, OnScreenKey, RoutingStrategies.Tunnel);
    }

    public void OpenModuleSwitcher()
    {
        OnSwitchModule(SwitchModule, new RoutedEventArgs());
    }

    private void FollowModel()
    {
        if(_model != null)
        {
            _model.Changed -= Update;
            _model.OpenFileRequested -= OnOpenFile;
        }

        _model = DataContext as ArchitectureViewModel;
        if(_model == null)
            return;

        _model.Changed += Update;
        _model.OpenFileRequested += OnOpenFile;
        _empty.ButtonCommand = _model.NewModuleCommand;
        Update();
    }

    private void Update()
    {
        if(_model == null)
            return;

        PickerPopup.IsOpen = false;
        NewButton.IsVisible = _model.HasModules;
        Frame.Empty = _model.HasModules || _model.HasBanner || _model.HasUndo ? null : _empty;
        Frame.Panel = _model.HasModules ? ModulePanel : null;
        if(!_model.CanEdit || _partModule != _model.SelectedName)
            ClosePart();

        WriteCount(_model);
        Fit();
        if(!_editing && _model.EditingName)
        {
            NameField.Text = _model.SelectedName;
            NameField.CaretIndex = NameField.Text.Length;
            Dispatcher.UIThread.Post(() => NameField.Focus(), DispatcherPriority.Loaded);
        }

        if(_editing && !_model.EditingName)
            Dispatcher.UIThread.Post(() => More.Focus(NavigationMethod.Directional), DispatcherPriority.Loaded);

        _editing = _model.EditingName;
    }

    private void WriteCount(ArchitectureViewModel model)
    {
        var text = model.Count.Split(' ');
        CountText.Inlines!.Clear();
        for(var i = 0; i < text.Length; i++)
        {
            var run = new Run((i == 0 ? string.Empty : " ") + text[i]);
            if(i == 0 || i == 2)
            {
                run.FontWeight = FontWeight.SemiBold;
                run.Bind(TextElement.ForegroundProperty, run.GetResourceObservable("ink"));
            }

            CountText.Inlines.Add(run);
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if(_model != null)
        {
            PickerPopup.IsOpen = false;
            Fit();
        }
    }

    private void FitPanel()
    {
        MainColumn.MinHeight = Frame.IsOnePane ? 0 : ModulePanel.Bounds.Height + 20;
    }

    private void Fit()
    {
        if(_model == null)
            return;

        var onePane = Frame.IsOnePane;
        var showsTable = Mode.SelectedIndex == 1;
        DiagramLabel.Text = onePane ? "List" : "Diagram";
        SwitchModule.IsVisible = onePane && !showsTable;
        Table.IsVisible = showsTable;
        Diagram.IsVisible = !onePane && !showsTable;
        ScrollCue.IsVisible = Diagram.IsVisible && _diagramWider;
        Table.Show(_model);
        Diagram.Show(_model);
        WriteLegend(_model, showsTable, onePane);
        FitPanel();
    }

    private void WriteLegend(ArchitectureViewModel model, bool showsTable, bool onePane)
    {
        Legend.Children.Clear();
        var name = model.SelectedName;
        if(showsTable)
        {
            AddKey(ArchitectureVisuals.Fill("solid", 12, 12, 3), "Row may use column");
            if(model.DrawnBreach != null)
                AddKey(ArchitectureVisuals.Fill("block", 12, 12, 3), "Breaks");

            Foot.Text = $"Every direction: {model.Directions.Count}. A heavier line starts each layer; an allowed direction always points to a layer below, right of the dashes." +
                (model.DrawnBreach == null ? string.Empty : " The red mark is the use that breaks one.");
            return;
        }

        if(onePane)
        {
            Foot.Text = $"The diagram needs a wide window. Here {name} is shown as its list, below: choose another module above, or in Used by.";
            return;
        }

        AddKey(LineSample("ink", 2), name + " may use");
        AddKey(LineSample("ink-2", 1.75, true), "Uses " + name);
        if(model.DrawnBreach != null)
            AddKey(LineSample("block", 3), "Breaks");

        AddKey(ArchitectureVisuals.Fill("line", 22, 14, 4), "Not linked to " + name);
        Foot.Text = $"Each module stands above the ones it may use. The lines of {name} are drawn; choose a module to see its own, or the table to see every direction.";
    }

    private void AddKey(Control mark, string text)
    {
        var item = new StackPanel { Classes = { "arch-key" }, Orientation = Orientation.Horizontal, Spacing = 6 };
        mark.VerticalAlignment = VerticalAlignment.Center;
        item.Children.Add(mark);
        item.Children.Add(new TextBlock { Text = text });
        Legend.Children.Add(item);
    }

    private static Avalonia.Controls.Shapes.Path LineSample(string brush, double width, bool dashed = false)
    {
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M0 4h22"), Width = 22, Height = 8, StrokeThickness = width };
        path.Bind(Shape.StrokeProperty, path.GetResourceObservable(brush));
        if(dashed)
            path.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6 / width, 3 / width };

        return path;
    }

    private static EmptyBlock MakeEmpty()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach(var name in new List<string> { "web", "app", "domain" })
        {
            if(stack.Children.Count != 0)
            {
                var arrow = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M5 0h2v13h4l-5 7-5-7h4z"), Width = 12, Height = 20, HorizontalAlignment = HorizontalAlignment.Center };
                arrow.Bind(Shape.FillProperty, arrow.GetResourceObservable("ink-3"));
                stack.Children.Add(arrow);
            }

            var box = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center };
            box.Bind(Border.BackgroundProperty, box.GetResourceObservable("page"));
            box.Bind(Border.BorderBrushProperty, box.GetResourceObservable("ink-3"));
            box.Child = ArchitectureVisuals.Text(name, "code-strong", "ink");
            stack.Children.Add(box);
        }

        var specimen = new Border { Child = stack, Padding = new Thickness(20, 16), CornerRadius = new CornerRadius(16) };
        specimen.Bind(Border.BackgroundProperty, specimen.GetResourceObservable("well"));
        var block = new EmptyBlock
        {
            Heading = "No structure rules",
            Emblem = specimen,
            ButtonText = "New module",
            Lead = "Name the parts of CommitFence as modules, then say which may use which. A push that makes a part use one it may not is refused."
        };
        block.Bind(EmptyBlock.ButtonIconProperty, block.GetResourceObservable("i-plus"));
        return block;
    }

    private void OnSwitchModule(object? sender, RoutedEventArgs e)
    {
        if(_model == null || sender is not Button button)
            return;

        var modules = _model.Report.Modules.ToDictionary(module => module.Name, StringComparer.Ordinal);
        var entries = _model.Layers.Order.Select(name =>
        {
            var module = modules[name];
            return Entry(name, name, $"uses {module.MayUse.Count}, used by {module.UsedBy.Count}", name == _model.SelectedName);
        }).ToList();
        OpenPicker(button, "Modules", entries, true, _model.Choose);
    }

    private void OnSwitchKey(object? sender, KeyEventArgs e)
    {
        if(_model?.Selected == null || (e.Key != Key.Up && e.Key != Key.Down))
            return;

        var order = _model.Layers.Order.ToList();
        var index = Math.Clamp(order.IndexOf(_model.SelectedName) + (e.Key == Key.Up ? -1 : 1), 0, order.Count - 1);
        _model.Choose(order[index]);
        e.Handled = true;
    }

    private void OnAddPart(object? sender, RoutedEventArgs e)
    {
        if(sender is not Button button || _model == null)
            return;

        OpenPicker(button, "Add a part", [Entry("namespace", "A namespace"), Entry("project", "A project"), Entry("folder", "A folder")],
            false, OpenPart);
    }

    private void OnAllowAnother(object? sender, RoutedEventArgs e)
    {
        if(sender is not Button button || _model is not { } model)
            return;

        var from = model.SelectedName;
        var entries = model.Layers.Order.Where(name => !model.IsAllowed(from, name) && model.DirectionRefusal(from, name) == null)
            .Select(name => Entry(name, name)).ToList();
        OpenPicker(button, "Allow another module", entries, false, to => model.SetDirection(from, to, true));
    }

    private static PickerEntryViewModel Entry(string key, string text, string note = "", bool current = false)
    {
        return new PickerEntryViewModel(key, text, note, string.Empty, current, false);
    }

    private void OpenPicker(Button button, string title, IReadOnlyList<PickerEntryViewModel> choices, bool find, Action<string> choose)
    {
        if(PickerPopup.IsOpen)
        {
            PickerPopup.IsOpen = false;
            return;
        }

        var picker = new PickerViewModel(title, 340, "Find a module", "No matching modules.") { AlwaysFind = find };
        PickerPopup.PlacementTarget = button;
        PickerPopup.Child = new PickerPanel { DataContext = picker, Classes = { "arch-picker" } };
        picker.PropertyChanged += (_, args) =>
        {
            if(args.PropertyName == nameof(PickerViewModel.IsOpen) && !picker.IsOpen)
                PickerPopup.IsOpen = false;
        };
        picker.Chosen += entry =>
        {
            button.Focus(NavigationMethod.Directional);
            choose(entry.Key);
        };
        picker.Open(choices, true);
        if(!choices.Any(choice => choice.IsCurrent))
            picker.MoveHighlight(1);

        SwitchModule.Classes.Set("open", button == SwitchModule);
        PickerPopup.IsOpen = true;
    }

    private void OpenPart(string kind)
    {
        _partKind = kind;
        _partModule = _model?.SelectedName;
        PartField.Text = string.Empty;
        PartField.PlaceholderText = kind;
        PartField.IsVisible = true;
        Dispatcher.UIThread.Post(() => PartField.Focus(), DispatcherPriority.Loaded);
    }

    private void ClosePart()
    {
        PartField.IsVisible = false;
        _partKind = null;
        _partModule = null;
    }

    private void OnPartKey(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Escape && e.Key != Key.Enter)
            return;

        if(e.Key == Key.Escape || (_partKind != null && _model?.SavePart(_partKind, PartField.Text ?? string.Empty) == true))
        {
            ClosePart();
            AddPart.Focus(NavigationMethod.Directional);
        }

        e.Handled = true;
    }

    private void OnScreenKey(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.F2 || _model?.CanEdit != true || !_model.HasModules)
            return;

        _model.StartRename();
        e.Handled = true;
    }

    private void OnNameKey(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Enter)
            _model?.FinishName(NameField.Text ?? string.Empty);
        else if(e.Key == Key.Escape)
            _model?.CancelName();
        else
            return;

        e.Handled = true;
    }

    private void OnRename(object? sender, RoutedEventArgs e)
    {
        _model?.StartRename();
    }

    private void OnDelete(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Delete || sender is not Button button || !button.IsEffectivelyEnabled)
            return;

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        e.Handled = true;
    }

    private void OnRemovePart(object? sender, RoutedEventArgs e)
    {
        if(sender is Button { DataContext: StructurePart part })
            _model?.RemovePart(part);
    }

    private void OnStopAllowing(object? sender, RoutedEventArgs e)
    {
        if(sender is Button { DataContext: string to } && _model is { } model)
            model.SetDirection(model.SelectedName, to, false);
    }

    private void OnChooseUser(object? sender, RoutedEventArgs e)
    {
        if(sender is Button { DataContext: string user })
            _model?.Choose(user);
    }

    private async void OnOpenFile(string path)
    {
        if(TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }
}
