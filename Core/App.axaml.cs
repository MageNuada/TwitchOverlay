using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LogMagic;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ChatOverlay.Core
{
    public class App : Application
    {
        private const string _settingsFile = "settings.json";

        public static ILog Log { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Log = L.G("client");
            L.Config.WriteTo.File($".{Path.DirectorySeparatorChar}logs{Path.DirectorySeparatorChar}log.txt").EnrichWith.ThreadId();
            Log.Trace($"Client started at {DateTime.Now}");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var loadingWindow = new LoadingView();
                desktop.MainWindow = loadingWindow;
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                Task.Run(() =>
                {
                    MainWindowViewModel? viewModel = null;
                    try
                    {
                        if (File.Exists(_settingsFile))
                        {
                            Log.Trace($"trying to load viewmodel from {_settingsFile}");
                            var serialized = File.ReadAllText(_settingsFile);
                            viewModel = JsonConvert.DeserializeObject<MainWindowViewModel>(serialized);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Error while loading viewmodel: {e.Message}");
                    }
                    if (viewModel == null)
                        viewModel = new MainWindowViewModel();
                    desktop.Exit += (s, e) =>
                    {
                        if (desktop.MainWindow.DataContext == null) return;
                        var serialized = JsonConvert.SerializeObject(desktop.MainWindow.DataContext, Formatting.Indented);
                        try
                        {
                            Log.Trace($"trying to save viewmodel to {_settingsFile}");
                            if (File.Exists(_settingsFile))
                                File.Delete(_settingsFile);
                            File.WriteAllText(_settingsFile, serialized);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error while saving viewmodel: {ex.Message}");
                        }
                    };
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await viewModel.Init();
                        //Log.Trace($"Setting shutdown mode ¹1");
                        //desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                        Log.Trace($"Creating main view");
                        desktop.MainWindow = new MainWindow() { DataContext = viewModel };
                        Log.Trace($"Show main window");
                        desktop.MainWindow.Show();
                        Log.Trace($"Closing loading window");
                        loadingWindow.Close();
                        //Log.Trace($"Setting shutdown mode ¹2");
                        //desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                    });
                });
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
