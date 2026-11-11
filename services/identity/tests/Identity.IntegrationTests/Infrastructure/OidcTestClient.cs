using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Identity.Infrastructure.Seeding;
using Microsoft.AspNetCore.WebUtilities;

namespace Identity.IntegrationTests.Infrastructure;

/// <summary>Plays the browser and the public client in the Authorization Code + PKCE flow.</summary>
public sealed partial class OidcTestClient(HttpClient browser)
{
    public string Verifier { get; } = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string Challenge => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    public string AuthorizeUrl(string clientId, string? extraQuery = null)
    {
        var url = $"/connect/authorize?client_id={clientId}&response_type=code&scope=openid%20profile" +
            $"&redirect_uri={Uri.EscapeDataString(SeedData.RedirectUri)}&state=test-state" +
            $"&code_challenge={Challenge}&code_challenge_method=S256";
        return extraQuery is null ? url : $"{url}&{extraQuery}";
    }

    public Task<HttpResponseMessage> StartAsync(string clientId, string? extraQuery = null) =>
        browser.GetAsync(AuthorizeUrl(clientId, extraQuery));

    /// <summary>Runs the flow up to the login form and submits the credentials.</summary>
    public async Task<HttpResponseMessage> SubmitCredentialsAsync(string loginUrl, string username, string password) =>
        await PostLoginAsync(await ReadLoginFormAsync(loginUrl), username, password);

    public async Task<string> ReadLoginFormAsync(string loginUrl)
    {
        var form = await browser.GetAsync(loginUrl);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        return await form.Content.ReadAsStringAsync();
    }

    public Task<HttpResponseMessage> PostLoginAsync(string formHtml, string username, string password) =>
        browser.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["returnUrl"] = HiddenField(formHtml, "returnUrl"),
            ["__RequestVerificationToken"] = HiddenField(formHtml, "__RequestVerificationToken"),
        }));

    /// <summary>Signs in from the start and returns the code sent to the client.</summary>
    public async Task<string> SignInForCodeAsync(string clientId, string username, string password)
    {
        var start = await StartAsync(clientId);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var login = await SubmitCredentialsAsync(LocationOf(start), username, password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorize = await browser.GetAsync(LocationOf(login));
        return CodeFrom(authorize);
    }

    public Task<HttpResponseMessage> ExchangeAsync(string clientId, string code, string? verifier = null) =>
        browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = SeedData.RedirectUri,
            ["code_verifier"] = verifier ?? Verifier,
        }));

    public async Task<string> SignInForAccessTokenAsync(string clientId, string username, string password)
    {
        var code = await SignInForCodeAsync(clientId, username, password);
        var response = await ExchangeAsync(clientId, code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    public Task<HttpResponseMessage> UserInfoAsync(string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }

        return browser.SendAsync(request);
    }

    public static string LocationOf(HttpResponseMessage response)
    {
        var location = response.Headers.Location ?? throw new InvalidOperationException($"No redirect: {response.StatusCode}");
        return location.IsAbsoluteUri && location.Host == "localhost" && location.Port == 80
            ? location.PathAndQuery
            : location.OriginalString;
    }

    public static string CodeFrom(HttpResponseMessage response)
    {
        var location = LocationOf(response);
        Assert.StartsWith(SeedData.RedirectUri, location);
        var code = HttpUtility.ParseQueryString(new Uri(location).Query)["code"];
        return code ?? throw new InvalidOperationException($"No code in {location}");
    }

    public static JsonElement ReadJwtPayload(string jwt)
    {
        var payload = WebEncoders.Base64UrlDecode(jwt.Split('.')[1]);
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.Clone();
    }

    private static string HiddenField(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"The form has no hidden field {name}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
