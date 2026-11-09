using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LayoutManagementAPI.Contracts;
using Microsoft.Extensions.Options;

namespace LayoutManagementAPI.Data;

public interface ILayoutExtractionService
{
    Task<LayoutDocument> ExtractAsync(LayoutExtractionRequest request, CancellationToken cancellationToken);
}

public record LayoutExtractionRequest(byte[] Content, string ContentType, int Width, int Height, string SourceImage);

public class LayoutExtractionOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o";
    public int TimeoutSeconds { get; set; } = 120;
}

public class LayoutExtractionService : ILayoutExtractionService
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LayoutExtractionOptions _options;

    public LayoutExtractionService(IHttpClientFactory httpClientFactory, IOptions<LayoutExtractionOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<LayoutDocument> ExtractAsync(LayoutExtractionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var missing = EmptyDraft(request, "ChatGPT");
            missing.Extraction.Status = "failed";
            missing.Extraction.Warnings.Add("ChatGPT extraction is not configured. Set LayoutExtraction:ApiKey to an OpenAI API key.");
            return missing;
        }

        var document = await ReadChatGptAsync(request, cancellationToken, attempt: 0);
        if (!string.Equals(document.Extraction.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            PrintedLayoutSketch.FillAreaFromSides(document);
        }

        return document;
    }

    private async Task<LayoutDocument> ReadChatGptAsync(LayoutExtractionRequest request, CancellationToken cancellationToken, int attempt)
    {
        var draft = EmptyDraft(request, "ChatGPT");
        try
        {
            var client = _httpClientFactory.CreateClient("LayoutExtraction");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, 300));
            using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            message.Content = new StringContent(BuildRequest(request), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var (providerMessage, providerCode) = ReadProviderError(body);
                var rateLimited = (int)response.StatusCode == 429
                    && !string.Equals(providerCode, "insufficient_quota", StringComparison.OrdinalIgnoreCase)
                    && providerMessage?.Contains("quota", StringComparison.OrdinalIgnoreCase) != true;
                if (rateLimited && attempt < 1)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
                    if (delay < TimeSpan.FromSeconds(20))
                    {
                        await Task.Delay(delay, cancellationToken);
                        return await ReadChatGptAsync(request, cancellationToken, attempt + 1);
                    }
                }

                draft.Extraction.Status = "failed";
                draft.Extraction.Warnings.Add(ProviderFailure((int)response.StatusCode, providerMessage, providerCode));
                return draft;
            }

            var document = ReadProviderDocument(body);
            if (document is null)
            {
                draft.Extraction.Status = "failed";
                draft.Extraction.Warnings.Add("ChatGPT did not return a valid layout document. No plot geometry was kept.");
                return draft;
            }

            document.SchemaVersion = LayoutDocumentValidator.SchemaVersion;
            if (document.CanvasWidth <= 0)
            {
                document.CanvasWidth = request.Width;
            }

            if (document.CanvasHeight <= 0)
            {
                document.CanvasHeight = request.Height;
            }

            document.SourceImage = request.SourceImage;
            document.Coordinates.System = "normalized-image";
            document.Coordinates.DimensionUnit = "feet";
            document.Extraction.Provider = "ChatGPT";
            document.Extraction.Status = "needs-review";
            document.Extraction.Warnings.Add("ChatGPT sizes are unverified. Review plot numbers, side lengths, and vertices before use.");
            return document;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            draft.Extraction.Status = "failed";
            draft.Extraction.Warnings.Add("ChatGPT could not be reached or returned malformed JSON. No plot geometry was inferred.");
            return draft;
        }
    }

    public static LayoutDocument EmptyDraft(LayoutExtractionRequest request, string provider) => new()
    {
        Name = "Untitled layout",
        SourceImage = request.SourceImage,
        CanvasWidth = request.Width,
        CanvasHeight = request.Height,
        Extraction = new ExtractionInfo
        {
            Provider = provider,
            Status = "needs-review",
            Confidence = null
        }
    };

    private string BuildRequest(LayoutExtractionRequest request)
    {
        var prompt = """
            You are reading a residential plot-layout sheet. Return only JSON for schemaVersion "2.0".
            Drawing convention:
            - A green circled number is the plot number. Use that number. Do not renumber plots.
            - Red numbers printed on the edges are side lengths in feet. Read each of the four sides. Do not invent a length that is not printed.
            - Put those four readings on sides: top, right, bottom, left, as they appear on the image. Use null for a side you cannot read.
            - Leave length, breadth, and area null. The application calculates them.
            - Blue text such as "20 Feet Road" is a road. Put its width in feet on the road.
            - The compass may show west at the top of the page. Do not guess a facing that is not printed.
            Coordinates are normalized fractions of the image, origin top-left, x to the right, y down, each between 0 and 1.
            These are image coordinates, not survey coordinates. Use null when a printed value cannot be read.
            Include every plot you can see. A sheet like this can have about 54 plots, so include them all.
            Properties: schemaVersion, name, canvasWidth, canvasHeight, coordinates, orientation, boundary, plots, roads, annotations, extraction.
            coordinates.dimensionUnit is "feet". coordinates.system is "normalized-image".
            Each plot needs id, plotNumber, vertices [{x,y}] (at least 3), sides {top,right,bottom,left}, area, length, breadth, facing, status, confidence, warnings.
            Side values are feet. status is available, blocked, sold, reserved, or unknown.
            Each road needs id, name, width, widthUnit "feet", vertices, confidence, warnings.
            """;
        var dataUrl = $"data:{request.ContentType};base64,{Convert.ToBase64String(request.Content)}";
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o" : _options.Model,
            max_tokens = 16000,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = dataUrl, detail = "high" } }
                    }
                }
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static LayoutDocument? ReadProviderDocument(string body)
    {
        try
        {
            using var provider = JsonDocument.Parse(body);
            if (!provider.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LayoutDocument>(UnwrapJson(content), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string? Message, string? Code) ReadProviderError(string body)
    {
        try
        {
            using var provider = JsonDocument.Parse(body);
            if (!provider.RootElement.TryGetProperty("error", out var error))
            {
                return (null, null);
            }

            var message = error.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
            var code = error.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(code) && error.TryGetProperty("type", out var typeElement))
            {
                code = typeElement.GetString();
            }

            return (message, code);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string ProviderFailure(int status, string? message, string? code)
    {
        if (status == 429 && (string.Equals(code, "insufficient_quota", StringComparison.OrdinalIgnoreCase) || message?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true))
        {
            return "ChatGPT refused the sketch because the OpenAI account has no remaining quota. Add a payment method or credits at platform.openai.com, then upload again.";
        }

        if (status == 429)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "ChatGPT is rate limited right now. Wait a minute and upload again."
                : $"ChatGPT is rate limited right now. {TrimProviderMessage(message)}";
        }

        return string.IsNullOrWhiteSpace(message)
            ? $"ChatGPT returned {status}. No plot geometry was inferred."
            : $"ChatGPT returned {status}. {TrimProviderMessage(message)}";
    }

    private static string TrimProviderMessage(string message)
    {
        var text = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 240 ? text : text[..240];
    }

    public static string UnwrapJson(string content)
    {
        var text = content.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLine = text.IndexOf('\n');
        if (firstLine >= 0)
        {
            text = text[(firstLine + 1)..];
        }

        if (text.EndsWith("```", StringComparison.Ordinal))
        {
            text = text[..^3];
        }

        return text.Trim();
    }
}
