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
        services.AddSingleton<IMkvToolService, MkvToolService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<ISubtitleOcrService, SubtitleOcrService>();
        services.AddSingleton<ISrtCorrectionService, SrtCorrectionService>();
        services.AddSingleton<INetworkDetectionService, NetworkDetectionService>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();

            // Register Views
            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
