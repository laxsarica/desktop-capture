using System.IO;

namespace DesktopViewer.Models;

public class AppSettings
{
    public string Provider { get; set; } = "OpenAI"; // "OpenAI" or "Azure"
    
    public string OpenAiApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o";

    public string AzureEndpoint { get; set; } = "https://viveksingh-claude-resource.services.ai.azure.com/openai/v1";
    public string AzureDeploymentName { get; set; } = "gpt-5.4-mini";
    public string AzureApiKey { get; set; } = "";

    private static readonly string SettingsFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public void Save()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new { 
                Settings = new { 
                    Provider, 
                    OpenAiApiKey, 
                    Model,
                    AzureEndpoint,
                    AzureDeploymentName,
                    AzureApiKey
                } 
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(SettingsFilePath, json);
    }
}
