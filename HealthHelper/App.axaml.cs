using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HealthHelper.Configuration;
using HealthHelper.Navigation;
using HealthHelper.Services.Clients;
using HealthHelper.Services.Contracts;
using HealthHelper.Services.Implementations;
using HealthHelper.ViewModels;
using HealthHelper.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace HealthHelper;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.Configure<HealthInsightsOptions>(options =>
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HealthHelper");
            Directory.CreateDirectory(dataDir);
            options.DatabasePath = Path.Combine(dataDir, "healthhelper.db");
        });

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IRecommendationClient, LargeLanguageModelClient>();
        services.AddSingleton<IHealthInsightsService, HealthInsightsService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddTransient(sp =>
        {
            var healthInsightsService = sp.GetRequiredService<IHealthInsightsService>();
            var navigationService = sp.GetRequiredService<INavigationService>();
            var adviceViewModelFactory = sp.GetRequiredService<Func<AdviceViewModel>>();
            var historyViewModelFactory = sp.GetRequiredService<Func<HistoryViewModel>>();
            var welcomeViewModelFactory = sp.GetRequiredService<Func<WelcomeViewModel>>();
            return new InputViewModel(healthInsightsService, navigationService, adviceViewModelFactory, historyViewModelFactory, welcomeViewModelFactory);
        });
        services.AddTransient<AdviceViewModel>();
        services.AddTransient<Func<AdviceViewModel>>(sp => () => sp.GetRequiredService<AdviceViewModel>());
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<Func<HistoryViewModel>>(sp => () => sp.GetRequiredService<HistoryViewModel>());
        services.AddTransient<HistoryDetailViewModel>();
        services.AddTransient<Func<HistoryDetailViewModel>>(sp => () => sp.GetRequiredService<HistoryDetailViewModel>());
        services.AddTransient<Func<InputViewModel>>(sp => () => sp.GetRequiredService<InputViewModel>());
        services.AddTransient<Func<WelcomeViewModel>>(sp => () => sp.GetRequiredService<WelcomeViewModel>());

        services.AddTransient(sp =>
        {
            var navigationService = sp.GetRequiredService<INavigationService>();
            var inputViewModelFactory = sp.GetRequiredService<Func<InputViewModel>>();
            var historyViewModelFactory = sp.GetRequiredService<Func<HistoryViewModel>>();
            var healthTipsViewModelFactory = sp.GetRequiredService<Func<HealthTipsViewModel>>();
            var healthInsightsService = sp.GetRequiredService<IHealthInsightsService>();
            return new WelcomeViewModel(navigationService, inputViewModelFactory, historyViewModelFactory, healthTipsViewModelFactory, healthInsightsService);
        });

        // Health Tips
        services.AddTransient<HealthTipsViewModel>();
        services.AddTransient<Func<HealthTipsViewModel>>(sp => () => sp.GetRequiredService<HealthTipsViewModel>());
        services.AddTransient<TipsViewModel>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<InputView>();
        services.AddTransient<AdviceView>();
        services.AddTransient<HistoryView>();
        services.AddTransient<HistoryDetailView>();
        services.AddTransient<WelcomeView>();
        services.AddTransient<HealthTipsView>();
        services.AddTransient<TipsView>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}