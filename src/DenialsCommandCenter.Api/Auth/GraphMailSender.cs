using System.Net.Http.Headers;
using System.Text.Json;
using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Api.Auth;

public interface IMailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

// Sends mail as SenderAddress through Microsoft Graph using an app registration with the Mail.Send application permission.
public sealed class GraphMailSender(HttpClient http, GraphMailOptions options) : IMailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var accessToken = await AccessTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(options.ApiBaseUrl, $"users/{Uri.EscapeDataString(options.SenderAddress!)}/sendMail"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                subject,
                body = new { contentType = "Text", content = body },
                toRecipients = new[] { new { emailAddress = new { address = to } } },
            },
            saveToSentItems = false,
        });
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync(
            new Uri(options.AuthorityUrl, $"{Uri.EscapeDataString(options.TenantId!)}/oauth2/v2.0/token"),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!,
                ["scope"] = options.Scope,
                ["grant_type"] = "client_credentials",
            }), ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new HttpRequestException("Microsoft identity platform returned no access token.");
    }
}
