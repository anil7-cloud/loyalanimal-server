using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LoyalAnimal.Server.Services;

public sealed class VerificationEmailService
{
    private readonly HttpClient _http;

    public VerificationEmailService(HttpClient http)
    {
        _http = http;
    }

    public async Task SendAsync(
        string recipientEmail,
        string verificationUrl,
        CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
        var fromEmail = Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL");

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException(
                "Resend e-posta gönderim ayarları eksik.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        request.Content = JsonContent.Create(new
        {
            from = fromEmail,
            to = new[] { recipientEmail },
            subject = "LoyalAnimal - E-posta adresini doğrula",
            text =
                "LoyalAnimal hesabını doğrulamak için aşağıdaki bağlantıyı aç:\n\n" +
                verificationUrl +
                "\n\nBu bağlantı 24 saat geçerlidir. " +
                "Bu hesabı sen oluşturmadıysan e-postayı dikkate alma."
        });

        using var response = await _http.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorType = "unknown";
            try
            {
                using var errorJson = System.Text.Json.JsonDocument.Parse(errorBody);
                if (errorJson.RootElement.TryGetProperty("name", out var name))
                    errorType = name.GetString() ?? "unknown";
            }
            catch (System.Text.Json.JsonException) { }

            Console.Error.WriteLine(
                $"Resend HTTP {(int)response.StatusCode}; hata türü: {errorType}");
            throw new HttpRequestException(
                $"Resend HTTP {(int)response.StatusCode}; hata türü: {errorType}");
        }
    }
}
