using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using DesktopViewer.Models;

namespace DesktopViewer;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoadSettings();
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

                // Check for new schema (Settings:...) or fallback to old schema (OpenAI:ApiKey)
                Settings = new AppSettings
                {
                    Provider = config["Settings:Provider"] ?? "OpenAI",
                    OpenAiApiKey = config["Settings:OpenAiApiKey"] ?? config["OpenAI:ApiKey"] ?? "",
                    Model = config["Settings:Model"] ?? config["OpenAI:Model"] ?? "gpt-4o",
                    AzureEndpoint = config["Settings:AzureEndpoint"] ?? "https://viveksingh-claude-resource.services.ai.azure.com/openai/v1",
                    AzureDeploymentName = config["Settings:AzureDeploymentName"] ?? "gpt-5.4-mini",
                    AzureApiKey = config["Settings:AzureApiKey"] ?? ""
                };
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }
}
