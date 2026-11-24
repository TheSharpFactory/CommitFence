using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CF.App.Shell;

internal sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
    }

    /// <summary>
    /// The window is up: the updates learn how to restart the app and open a page, then look for a newer version.
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        if(Model == null)
            return;

        Model.Updates.StartAndExit = StartAndExit;
        Model.Updates.OpenUri = OpenUri;
        Model.Updates.Start();
    }

    private bool StartAndExit(ProcessStartInfo startInfo)
    {
        try
        {
            using(Process.Start(startInfo))
            {
            }
        }
        catch(Exception exception) when(exception is Win32Exception || exception is InvalidOperationException)
        {
            return false;
        }

        Close();
        return true;
    }

    private async Task OpenUri(Uri uri)
    {
        await Launcher.LaunchUriAsync(uri);
    }

    private MainWindowViewModel? Model
    {
        get
        {
            return DataContext as MainWindowViewModel;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if(Model == null || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        switch(e.Key)
        {
            case Key.D1:
                Model.ShowRules();
                e.Handled = true;
                break;
            case Key.D2:
                Model.ShowReview();
                e.Handled = true;
                break;
            case Key.N:
                Model.ShowRules();
                Model.Rules.NewRule();
                e.Handled = true;
                break;
            case Key.S:
                Model.Rules.SaveOpenRule();
                e.Handled = true;
                break;
            case Key.U:
                Model.Updates.CheckNowCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
                Model.ShowRules();
                RulesScreen.FocusSearch();
                e.Handled = true;
                break;
        }
    }
}
