using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using DesktopViewer.Models;
using DesktopViewer.Services;

namespace DesktopViewer;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoadSettings();

        // Validate license key before allowing the app to run
        if (!await LicenseService.IsValidAsync(Settings.LicenseKey))
        {
            MessageBox.Show(
                "Invalid or missing License Key.\n\n" +
                "Please set a valid \"LicenseKey\" in appsettings.json\n" +
                "located next to DesktopViewer.exe and restart the app.",
                "License Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Shutdown();
            return;
        }
    }

    public static void LoadSettings()
    {
        try
        {
            var configPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            if (File.Exists(configPath))
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile(configPath, optional: true, reloadOnChange: false)
                    .Build();

                var defaultSettings = new AppSettings();

                Settings = new AppSettings
                {
                    Provider = config["Settings:Provider"] ?? defaultSettings.Provider,
                    OpenAiApiKey = config["Settings:OpenAiApiKey"] ?? config["OpenAI:ApiKey"] ?? defaultSettings.OpenAiApiKey,
                    Model = config["Settings:Model"] ?? config["OpenAI:Model"] ?? defaultSettings.Model,
                    AzureEndpoint = config["Settings:AzureEndpoint"] ?? defaultSettings.AzureEndpoint,
                    AzureDeploymentName = config["Settings:AzureDeploymentName"] ?? defaultSettings.AzureDeploymentName,
                    AzureApiKey = config["Settings:AzureApiKey"] ?? defaultSettings.AzureApiKey,
                    LicenseKey = config["Settings:LicenseKey"] ?? ""
                };
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }
}
