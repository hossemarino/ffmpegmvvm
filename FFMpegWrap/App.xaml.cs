using System.Windows;
using FFMpegWrap.Services;
using FFMpegWrap.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FFMpegWrap
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            _serviceProvider.GetRequiredService<IThemeService>().Initialize(this);

            MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IToolPathService, ToolPathService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<IMediaToolService, MediaToolService>();
            services.AddSingleton<IYtDlpService, YtDlpService>();
            services.AddSingleton<IHelpPageService, HelpPageService>();

            services.AddSingleton<LocalMediaViewModel>();
            services.AddSingleton<OnlineMediaViewModel>();
            services.AddSingleton<CommandExplorerViewModel>();
            services.AddSingleton<ToolPathsViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            services.AddSingleton<MainWindow>();
        }
    }

}
