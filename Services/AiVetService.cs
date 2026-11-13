using System.Net.Http.Json;
using System.Text.Json;
using LoyalAnimal.Server.Models;

namespace LoyalAnimal.Server.Services;

public sealed class AiVetService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    private const string GeminiUrl =
        "https://generativelanguage.googleapis.com/" +
        "v1beta/models/gemini-3.5-flash-lite:generateContent";

    public AiVetService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<string> AskAsync(
        string question,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiVetChatMessage>? history = null)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Gemini API anahtarı yapılandırılmadı.");

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = """
                            Sen evcil hayvan sağlığı hakkında Türkçe
                            bilgilendirme yapan bir yapay zekâ asistanısın.

                            Veteriner hekim olduğunu iddia etme.
                            Kesin teşhis koyma.
                            İlaç veya ilaç dozu reçete etme.
                            Belirsizlikleri açıkça belirt.
                            Tehlikeli ev tedavileri önerme.

                            Solunum güçlüğü, zehirlenme, nöbet,
                            ciddi kanama, bilinç kaybı veya
                            idrar yapamama durumlarında derhal
                            veterinere başvurulmasını söyle.

                            Kedilerde uzun süre yemek yememenin
                            ciddi olabileceğini belirt.
                            Riskli belirtilerde beklemeyi önerme.

                            Yanıtların kısa, anlaşılır ve
                            Türkçe olsun.
                            """
                    }
                }
            },
            contents = BuildContents(question, history),
            generationConfig = new
            {
                maxOutputTokens = 1000
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            GeminiUrl);

        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = json.RootElement;

        if (root.TryGetProperty("candidates", out var candidates))
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content))
                    continue;

                if (!content.TryGetProperty("parts", out var parts))
                    continue;

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        var answer = text.GetString();

                        if (!string.IsNullOrWhiteSpace(answer))
                            return answer;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "Gemini yanıtı alınamadı.");
    }

    private static object[] BuildContents(
        string question,
        IReadOnlyList<AiVetChatMessage>? history)
    {
        var contents = new List<object>();

        if (history is not null)
        {
            foreach (var item in history.TakeLast(20))
            {
                var role = item.Role?.Trim().ToLowerInvariant();
                var value = item.Text?.Trim();

                if (role is not ("user" or "model") ||
                    string.IsNullOrWhiteSpace(value) ||
                    value.Length > 2000)
                    continue;

                contents.Add(new
                {
                    role,
                    parts = new[] { new { text = value } }
                });
            }
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = question } }
        });

        return contents.ToArray();
    }

    public async Task<string> AnalyzeImageAsync(
        string prompt,
        byte[] imageBytes,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Gemini API anahtarı yapılandırılmadı.");

        if (imageBytes.Length == 0 || imageBytes.Length > 10 * 1024 * 1024)
            throw new ArgumentException("Fotoğraf boş veya 10 MB sınırını aşıyor.");

        if (mimeType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new ArgumentException("Desteklenmeyen fotoğraf formatı.");

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = """
                            Sen evcil hayvan sağlığı ve bakımı hakkında
                            Türkçe bilgilendirme yapan bir yapay zekâ asistanısın.
                            Fotoğrafta yalnızca gerçekten gözlemleyebildiğin
                            özellikleri açıkla. Kesin teşhis koyma.
                            Fotoğraftan hastalık kesin olarak belirlenemez.
                            İlaç veya ilaç dozu önerme.
                            Acil belirtiler varsa derhal veterinere yönlendir.
                            Yanıtlarını anlaşılır başlıklarla ve Türkçe ver.
                            """
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inlineData = new
                            {
                                mimeType,
                                data = Convert.ToBase64String(imageBytes)
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = 1200
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, GeminiUrl);

        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(
            request, cancellationToken);

        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (json.RootElement.TryGetProperty("candidates", out var candidates))
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content))
                    continue;

                if (!content.TryGetProperty("parts", out var parts))
                    continue;

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        var answer = text.GetString();
                        if (!string.IsNullOrWhiteSpace(answer))
                            return answer;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "Gemini fotoğraf analizi yanıtı alınamadı.");
    }

}
