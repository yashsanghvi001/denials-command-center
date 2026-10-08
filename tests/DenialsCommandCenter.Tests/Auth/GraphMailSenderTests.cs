using System.Net;
using System.Text;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Tests.Auth;

public class GraphMailSenderTests
{
    [Fact]
    public async Task Token_and_send_requests_go_to_the_configured_endpoints()
    {
        var handler = new RecordingHandler();
        var options = new GraphMailOptions
        {
            TenantId = "tenant-1", ClientId = "client", ClientSecret = "secret", SenderAddress = "denials@example.test",
            AuthorityUrl = new Uri("https://login.microsoftonline.com/"),
            ApiBaseUrl = new Uri("https://graph.microsoft.com/v1.0/"),
            Scope = "https://graph.microsoft.com/.default",
            TimeoutSeconds = 30,
        };

        await new GraphMailSender(new HttpClient(handler), options).SendAsync("anjali@example.test", "Subject", "Body", CancellationToken.None);

        Assert.Equal(
            ["https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token", "https://graph.microsoft.com/v1.0/users/denials%40example.test/sendMail"],
            handler.Requests.Select(request => request.Uri.AbsoluteUri));
        Assert.Contains("scope=https%3A%2F%2Fgraph.microsoft.com%2F.default", handler.Requests[0].Body);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token"}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
