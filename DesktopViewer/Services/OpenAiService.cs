using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using DesktopViewer.Models;

namespace DesktopViewer.Services;

/// <summary>
/// Calls either standard OpenAI or Azure AI Services based on settings.
/// </summary>
public class OpenAiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    
    private const string SystemPrompt = """
        You are a screen analysis assistant. The user has captured a screenshot of their screen.
        You will receive:
        1. The screenshot image
        2. OCR-extracted text from the screenshot (may be imperfect)

        Your job:
        - If the screen shows a question (quiz, exam, test, form), identify the question and all options, then provide the correct answer with a brief explanation.
        - If the screen shows a Q&A structure, detect it and answer clearly.
        - If no question is found, summarize what is visible on the screen.

        Response format (always use this JSON structure):
        {
          "detected_question": "The question text if found, or null",
          "answer": "Your answer or option number",
          "confidence": "High/Medium/Low"
        }

        Always respond with valid JSON only. No markdown, no code fences.
        """;

    public OpenAiService(AppSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<AiResponse> AnalyzeAsync(byte[] screenshotPng, string ocrText)
    {
        string apiUrl;
        string model;

        try
        {
            if (_settings.Provider == "Azure")
            {
                apiUrl = $"{_settings.AzureEndpoint.TrimEnd('/')}/chat/completions";
                model = _settings.AzureDeploymentName;

                // Use the API key provided from the image as a Bearer token
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AzureApiKey);
            }
            else // Standard OpenAI
            {
                apiUrl = "https://api.openai.com/v1/chat/completions";
                model = _settings.Model;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.OpenAiApiKey);
            }
        }
        catch (Exception ex)
        {
            return new AiResponse(Answer: $"Authentication Error: {ex.Message}");
        }

        var base64Image = Convert.ToBase64String(screenshotPng);

        var requestBody = new
        {
            model = model,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = SystemPrompt
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = string.IsNullOrWhiteSpace(ocrText)
                                ? "Please analyze this screenshot."
                                : $"OCR extracted text:\n\n{ocrText}\n\nPlease analyze the screenshot and the OCR text above."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/png;base64,{base64Image}",
                                detail = "high"
                            }
                        }
                    }
                }
            },
            max_completion_tokens = 1024
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(apiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new AiResponse(
                Answer: $"API Error ({response.StatusCode}): {responseJson}");
        }

        return ParseResponse(responseJson);
    }

    private static AiResponse ParseResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            try
            {
                using var aiDoc = JsonDocument.Parse(messageContent);
                var root = aiDoc.RootElement;

                return new AiResponse(
                    Answer: root.TryGetProperty("answer", out var ans)
                        ? ans.GetString() ?? messageContent
                        : messageContent,
                    DetectedQuestion: root.TryGetProperty("detected_question", out var q)
                        ? q.ValueKind != JsonValueKind.Null ? q.GetString() : null
                        : null,
                    Confidence: root.TryGetProperty("confidence", out var c)
                        ? c.GetString()
                        : null
                );
            }
            catch
            {
                return new AiResponse(Answer: messageContent);
            }
        }
        catch (Exception ex)
        {
            return new AiResponse(Answer: $"Failed to parse response: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
