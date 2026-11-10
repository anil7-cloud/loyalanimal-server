using System.Net.Http.Json;
using System.Text.Json;

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
        CancellationToken cancellationToken = default)
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
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = question }
                    }
                }
            },
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
}
