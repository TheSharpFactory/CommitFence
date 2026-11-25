using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CF.App.Parts;
using CF.Composition;

namespace CF.App.Shell;

internal sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = CompositionRoot.Create();
            Motion.ReducedOnThisMachine = services.Machine.ReduceMotion;
            var model = new MainWindowViewModel(services, FolderToWorkOn(desktop.Args), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            var window = new MainWindow
            {
                DataContext = model
            };
            window.Closed += (_, _) => model.Dispose();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The program works on the folder it is started in, or on the one folder given as its argument.
    /// </summary>
    private static string FolderToWorkOn(string[]? arguments)
    {
        if(arguments != null && arguments.Length > 0)
            return arguments[0];

        return Environment.CurrentDirectory;
    }
}
