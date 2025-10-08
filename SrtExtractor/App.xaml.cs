using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SrtExtractor.Services.Implementations;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.ViewModels;
using SrtExtractor.Views;

namespace SrtExtractor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Create and show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
        // Register services
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IToolDetectionService, ToolDetectionService>();
        services.AddSingleton<IWingetService, WingetService>();
        services.AddSingleton<IAsyncFileService, AsyncFileService>();
        services.AddSingleton<IFileLockDetectionService, FileLockDetectionService>();
        services.AddSingleton<IMkvToolService, MkvToolService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<ISubtitleOcrService, SubtitleOcrService>();
        services.AddSingleton<ISrtCorrectionService, SrtCorrectionService>();
        services.AddSingleton<IMultiPassCorrectionService, MultiPassCorrectionService>();
            services.AddSingleton<INetworkDetectionService, NetworkDetectionService>();
            services.AddSingleton<IRecentFilesService, RecentFilesService>();
            services.AddSingleton<IWindowStateService, WindowStateService>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<BatchSrtCorrectionViewModel>();
            services.AddTransient<VobSubTrackAnalyzerViewModel>();

            // Register Views
            services.AddTransient<MainWindow>();
            services.AddTransient<BatchSrtCorrectionWindow>();
            services.AddTransient<VobSubTrackAnalyzerWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<SrtCorrectionWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Dispose of the main window's ViewModel if it exists
                if (Current.MainWindow?.DataContext is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't prevent shutdown
                System.Diagnostics.Debug.WriteLine($"Error disposing ViewModel: {ex.Message}");
            }
            finally
            {
                _serviceProvider?.Dispose();
                base.OnExit(e);
            }
        }
    }
}
