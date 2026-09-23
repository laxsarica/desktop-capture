namespace DesktopViewer.Models;

public record AiResponse(
    string Answer,
    string? DetectedQuestion = null,
    string? Confidence = null);
