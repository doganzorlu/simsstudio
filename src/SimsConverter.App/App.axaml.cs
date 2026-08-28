using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SimsConverter.App.Services;
using SimsConverter.App.ViewModels;
using SimsConverter.Application.Contracts;
using SimsConverter.Application.Services;
using SimsConverter.Package.Contracts;
using SimsConverter.Package.Services;
using SimsConverter.Textures.Contracts;
using SimsConverter.Textures.Services;

namespace SimsConverter.App;

public partial class App : Avalonia.Application
{
    public IServiceProvider? Services { get; private set; }

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
            var viewModel = Services.GetRequiredService<ResourceInspectorViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Func<Window?>>(() =>
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        });

        services.AddSingleton<ISims3PackDetector, Sims3PackDetector>();
        services.AddSingleton<ISims3PackXmlParser, Sims3PackXmlParser>();
        services.AddSingleton<ISims3PackPayloadCatalogScanner, Sims3PackPayloadCatalogScanner>();
        services.AddSingleton<ISims3PackPayloadExporter, Sims3PackPayloadExporter>();
        services.AddSingleton<ISims3PackInspectionService, Sims3PackInspectionService>();
        services.AddSingleton<IPackageDetector, PackageDetector>();
        services.AddSingleton<IDbpfPackageParser, DbpfPackageParser>();
        services.AddSingleton<IPackageResourceExporter, PackageResourceExporter>();
        services.AddSingleton<IPackageInspectionService, PackageInspectionService>();
        services.AddSingleton<IResourceExportService, ResourceExportService>();
        services.AddSingleton<ITextureResourceClassifier, TextureResourceClassifier>();
        services.AddSingleton<ITextureResourceExtractor, TextureResourceExtractor>();
        services.AddSingleton<IDdsHeaderParser, DdsHeaderParser>();
        services.AddSingleton<IDdsPayloadValidator, DdsPayloadValidator>();
        services.AddSingleton<ITextureInspectionService, TextureInspectionService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddTransient<ResourceInspectorViewModel>();
    }
}