using System.Collections.Concurrent;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

// ---------------------------------------------------------------------------
// In-memory state
// ---------------------------------------------------------------------------

// Pending authorization codes: code -> PendingAuth
var pendingCodes = new ConcurrentDictionary<string, PendingAuth>();

// Valid issued access tokens
var issuedTokens = new ConcurrentDictionary<string, bool>();

// ---------------------------------------------------------------------------
// Image assets — picsum.photos serves stable, freely-usable images by ID.
// ---------------------------------------------------------------------------
var imageAssets = new Asset[]
{
    new("img-1",  "Mountain Landscape",    "image/jpeg", "https://picsum.photos/id/10/300/200",  "https://picsum.photos/id/10/1600/1067",  ["nature", "landscape", "mountain"]),
    new("img-2",  "City at Night",         "image/jpeg", "https://picsum.photos/id/20/300/200",  "https://picsum.photos/id/20/1600/1067",  ["city", "urban", "night"]),
    new("img-3",  "Forest Path",           "image/jpeg", "https://picsum.photos/id/30/300/200",  "https://picsum.photos/id/30/1600/1067",  ["nature", "forest", "trees"]),
    new("img-4",  "Ocean Waves",           "image/jpeg", "https://picsum.photos/id/40/300/200",  "https://picsum.photos/id/40/1600/1067",  ["ocean", "waves", "nature"]),
    new("img-5",  "Desert Dunes",          "image/jpeg", "https://picsum.photos/id/50/300/200",  "https://picsum.photos/id/50/1600/1067",  ["desert", "sand", "landscape"]),
    new("img-6",  "Snowy Peaks",           "image/jpeg", "https://picsum.photos/id/60/300/200",  "https://picsum.photos/id/60/1600/1067",  ["snow", "mountain", "winter"]),
    new("img-7",  "Autumn Colours",        "image/jpeg", "https://picsum.photos/id/70/300/200",  "https://picsum.photos/id/70/1600/1067",  ["autumn", "leaves", "nature"]),
    new("img-8",  "Tropical Beach",        "image/jpeg", "https://picsum.photos/id/80/300/200",  "https://picsum.photos/id/80/1600/1067",  ["beach", "tropical", "ocean"]),
    new("img-9",  "Abstract Architecture", "image/jpeg", "https://picsum.photos/id/90/300/200",  "https://picsum.photos/id/90/1600/1067",  ["architecture", "abstract", "building"]),
    new("img-10", "Rustic Farm",           "image/jpeg", "https://picsum.photos/id/100/300/200", "https://picsum.photos/id/100/1600/1067", ["farm", "rural", "nature"]),
    new("img-11", "Waterfall",             "image/jpeg", "https://picsum.photos/id/110/300/200", "https://picsum.photos/id/110/1600/1067", ["waterfall", "nature", "water"]),
    new("img-12", "Vintage Street",        "image/jpeg", "https://picsum.photos/id/120/300/200", "https://picsum.photos/id/120/1600/1067", ["street", "vintage", "city"]),
};

// ---------------------------------------------------------------------------
// Text element assets — download URL is served by /text-files/{id} below.
// previewUrl uses placehold.co so Templafy can render a thumbnail.
// ---------------------------------------------------------------------------
var textContents = new Dictionary<string, string>
{
    ["txt-1"] = "Acme Corp is a global leader in innovative business solutions, serving over 10,000 customers across 50 countries. Our mission is to empower organizations with tools that streamline their workflows and drive measurable results.",
    ["txt-2"] = "CONFIDENTIALITY NOTICE: This document and any attachments are confidential and intended solely for the use of the named recipient(s). If you have received this in error, please notify the sender immediately and destroy all copies.",
    ["txt-3"] = "At Acme Corp, we are committed to protecting your privacy. We collect only the data necessary to provide our services and never sell your personal information to third parties. For full details, please review our Privacy Policy at acme.com/privacy.",
    ["txt-4"] = "By using our services, you agree to these Terms of Service. We reserve the right to update these terms at any time. Continued use of the service following any changes constitutes your acceptance of the revised terms.",
    ["txt-5"] = "Innovate. Inspire. Deliver.\n\nAcme Corp — Your partner in transformation.",
    ["txt-6"] = "Our flagship product, AcmeSuite, combines cutting-edge AI with an intuitive interface to automate repetitive tasks, surface actionable insights, and help teams focus on what matters most.",
    ["txt-7"] = "For general inquiries: contact@acme.com\nFor support: support@acme.com | +1 (800) 555-0100\nHeadquarters: 123 Innovation Drive, San Francisco, CA 94105",
    ["txt-8"] = "This proposal is submitted in strict confidence. The information contained herein is proprietary to Acme Corp and may not be reproduced or disclosed to any third party without prior written consent.",
    ["txt-9"] = "Q3 Results Highlights:\n• Revenue: $42.3M (+18% YoY)\n• New customers: 320\n• Net Promoter Score: 72\n• Employee headcount: 480 (+12% YoY)",
    ["txt-10"] = "Dear [Customer Name],\n\nThank you for choosing Acme Corp. We value your partnership and are committed to ensuring your success with our platform.\n\nWarm regards,\nThe Acme Corp Team",
};

var textAssets = new Asset[]
{
    new("txt-1",  "Company Overview",         "text/plain", "https://placehold.co/300x200/e8f4fd/1a56db?text=Company+Overview",     string.Empty, ["company", "overview", "about"]),
    new("txt-2",  "Confidentiality Notice",   "text/plain", "https://placehold.co/300x200/fdf8e8/b45309?text=Confidentiality",      string.Empty, ["legal", "confidential", "notice"]),
    new("txt-3",  "Privacy Policy Intro",     "text/plain", "https://placehold.co/300x200/f0fdf4/166534?text=Privacy+Policy",       string.Empty, ["legal", "privacy", "gdpr"]),
    new("txt-4",  "Terms of Service",         "text/plain", "https://placehold.co/300x200/fdf4ff/6b21a8?text=Terms+of+Service",     string.Empty, ["legal", "terms", "agreement"]),
    new("txt-5",  "Marketing Tagline",        "text/plain", "https://placehold.co/300x200/fff7ed/c2410c?text=Tagline",              string.Empty, ["marketing", "brand", "tagline"]),
    new("txt-6",  "Product Description",      "text/plain", "https://placehold.co/300x200/eff6ff/1d4ed8?text=Product+Description",  string.Empty, ["product", "description", "marketing"]),
    new("txt-7",  "Contact Information",      "text/plain", "https://placehold.co/300x200/f0fdf4/14532d?text=Contact+Info",        string.Empty, ["contact", "info", "support"]),
    new("txt-8",  "Proposal Disclaimer",      "text/plain", "https://placehold.co/300x200/fff1f2/9f1239?text=Disclaimer",           string.Empty, ["legal", "proposal", "disclaimer"]),
    new("txt-9",  "Q3 Results Summary",       "text/plain", "https://placehold.co/300x200/f0fdf4/166534?text=Q3+Results",          string.Empty, ["financial", "results", "quarterly"]),
    new("txt-10", "Customer Email Template",  "text/plain", "https://placehold.co/300x200/fef9c3/854d0e?text=Email+Template",       string.Empty, ["email", "template", "customer"]),
};

// ---------------------------------------------------------------------------
// POST /oauth/token
// ---------------------------------------------------------------------------
app.MapPost("/oauth/token", async (HttpRequest httpReq) =>
{
    IFormCollection form;
    try { form = await httpReq.ReadFormAsync(); }
    catch { return Results.BadRequest(new { error = "invalid_request", error_description = "Expected application/x-www-form-urlencoded body." }); }

    var grantType   = form["grant_type"].ToString();
    var code        = form["code"].ToString();
    var codeVerifier = form["code_verifier"].ToString();

    if (grantType == "client_credentials")
        return Results.Ok(IssueToken());

    if (grantType == "authorization_code")
    {
        if (string.IsNullOrEmpty(code) || !pendingCodes.TryRemove(code, out var pending))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Unknown or expired authorization code." });

        if (pending.CodeChallenge is not null)
        {
            if (string.IsNullOrEmpty(codeVerifier))
                return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier is required for PKCE." });

            var computed = ComputeS256Challenge(codeVerifier);
            if (!string.Equals(computed, pending.CodeChallenge, StringComparison.Ordinal))
                return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier does not match code_challenge." });
        }

        return Results.Ok(IssueToken());
    }

    return Results.BadRequest(new { error = "unsupported_grant_type" });
})
.DisableAntiforgery();

// ---------------------------------------------------------------------------
// GET /oauth/authorize  (Authorization Code + PKCE flows)
// ---------------------------------------------------------------------------
app.MapGet("/oauth/authorize", (
    [FromQuery] string? response_type,
    [FromQuery] string? client_id,
    [FromQuery] string? state,
    [FromQuery] string? redirect_uri,
    [FromQuery] string? scope,
    [FromQuery] string? code_challenge_method,
    [FromQuery] string? code_challenge) =>
{
    var html = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Mock Content Connector — Sign In</title>
          <style>
            body { font-family: system-ui, sans-serif; display: flex; align-items: center;
                    justify-content: center; min-height: 100vh; margin: 0; background: #f5f5f5; }
            .card { background: white; padding: 2rem; border-radius: 8px;
                    box-shadow: 0 2px 8px rgba(0,0,0,.12); width: 320px; }
            h1 { font-size: 1.25rem; margin: 0 0 1.5rem; color: #111; }
            label { display: block; font-size: .875rem; color: #555; margin-bottom: .25rem; }
            input { width: 100%; box-sizing: border-box; padding: .5rem .75rem;
                    border: 1px solid #ccc; border-radius: 4px; font-size: 1rem; margin-bottom: 1rem; }
            button { width: 100%; padding: .625rem; background: #1a56db; color: white;
                      border: none; border-radius: 4px; font-size: 1rem; cursor: pointer; }
            button:hover { background: #1648c0; }
            .hint { font-size: .75rem; color: #888; margin-top: 1rem; text-align: center; }
          </style>
        </head>
        <body>
          <div class="card">
            <h1>Mock Content Connector</h1>
            <form method="post" action="/oauth/authorize/callback">
              <input type="hidden" name="redirect_uri"          value="{{HtmlEncode(redirect_uri ?? "")}}">
              <input type="hidden" name="state"                 value="{{HtmlEncode(state ?? "")}}">
              <input type="hidden" name="code_challenge"        value="{{HtmlEncode(code_challenge ?? "")}}">
              <input type="hidden" name="code_challenge_method" value="{{HtmlEncode(code_challenge_method ?? "")}}">
              <label for="username">Username</label>
              <input id="username" name="username" type="text" placeholder="any value" required>
              <label for="password">Password</label>
              <input id="password" name="password" type="password" placeholder="any value" required>
              <button type="submit">Sign in</button>
            </form>
            <p class="hint">Any credentials are accepted in this mock.</p>
          </div>
        </body>
        </html>
        """;

    return Results.Content(html, MediaTypeNames.Text.Html, Encoding.UTF8);
});

// ---------------------------------------------------------------------------
// POST /oauth/authorize/callback
// ---------------------------------------------------------------------------
app.MapPost("/oauth/authorize/callback", async (HttpRequest httpReq) =>
{
    var form = await httpReq.ReadFormAsync();
    var redirectUri        = form["redirect_uri"].ToString();
    var state              = form["state"].ToString();
    var codeChallenge      = form["code_challenge"].ToString();
    var codeChallengeMethod = form["code_challenge_method"].ToString();

    if (string.IsNullOrWhiteSpace(redirectUri))
        return Results.BadRequest("redirect_uri is required");

    var code = GenerateCode();
    pendingCodes[code] = new PendingAuth(
        string.IsNullOrEmpty(codeChallenge) ? null : codeChallenge,
        codeChallengeMethod);

    var sep = redirectUri.Contains('?') ? '&' : '?';
    var location = $"{redirectUri}{sep}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";
    return Results.Redirect(location);
})
.DisableAntiforgery();

// ---------------------------------------------------------------------------
// GET /content
// ---------------------------------------------------------------------------
app.MapGet("/content", (
    HttpContext ctx,
    [FromQuery] string? contentType,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 20,
    [FromQuery] string? search = null,
    [FromQuery] string? parentId = null) =>
{
    if (!IsAuthorized(ctx, issuedTokens))
        return Results.Unauthorized();

    IEnumerable<Asset> source = contentType switch
    {
        "image"       => imageAssets,
        "textElement" => textAssets,
        null          => [.. imageAssets, .. textAssets],
        _             => [],
    };

    if (!string.IsNullOrWhiteSpace(search))
    {
        source = source.Where(a =>
            a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    var total = source.Count();
    var page  = source.Skip(skip).Take(limit)
                      .Select(a => a.ToContentItem(BaseUrl(ctx)))
                      .ToArray();

    return Results.Ok(new ContentResponse(page, total, skip));
});

// ---------------------------------------------------------------------------
// GET /content/{assetId}/download-url
// ---------------------------------------------------------------------------
app.MapGet("/content/{assetId}/download-url", (HttpContext ctx, string assetId) =>
{
    if (!IsAuthorized(ctx, issuedTokens))
        return Results.Unauthorized();

    Asset? asset = imageAssets.FirstOrDefault(a => a.Id == assetId)
                ?? textAssets.FirstOrDefault(a => a.Id == assetId);

    if (asset is null)
        return Results.NotFound();

    var url = asset.MimeType == "text/plain"
        ? $"{BaseUrl(ctx)}/text-files/{asset.Id}"
        : asset.DownloadUrl;

    return Results.Ok(new DownloadUrlResponse(url));
});

// ---------------------------------------------------------------------------
// GET /text-files/{id}  — serves raw text content (no auth, it's a download URL)
// ---------------------------------------------------------------------------
app.MapGet("/text-files/{id}", (string id) =>
{
    if (!textContents.TryGetValue(id, out var content))
        return Results.NotFound();

    return Results.Text(content, "text/plain", Encoding.UTF8);
});

app.Run();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static bool IsAuthorized(HttpContext ctx, ConcurrentDictionary<string, bool> tokens)
{
    var header = ctx.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return false;
    var token = header["Bearer ".Length..].Trim();
    return tokens.ContainsKey(token);
}

OAuthTokenResponse IssueToken()
{
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    issuedTokens[token] = true;
    return new OAuthTokenResponse(token, "Bearer", 3600);
}

static string GenerateCode() =>
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
           .Replace('+', '-').Replace('/', '_').TrimEnd('=');

static string ComputeS256Challenge(string codeVerifier)
{
    var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

static string BaseUrl(HttpContext ctx) =>
    $"{ctx.Request.Scheme}://{ctx.Request.Host}";

static string HtmlEncode(string value) =>
    System.Net.WebUtility.HtmlEncode(value);

// ---------------------------------------------------------------------------
// Records / DTOs
// ---------------------------------------------------------------------------
record PendingAuth(string? CodeChallenge, string? CodeChallengeMethod);

record Asset(
    string Id,
    string Name,
    string MimeType,
    string PreviewUrl,
    string DownloadUrl,
    string[] Tags)
{
    public ContentItem ToContentItem(string baseUrl)
    {
        var preview = MimeType == "text/plain"
            ? PreviewUrl
            : PreviewUrl;
        return new ContentItem(Id, Name, MimeType, preview, string.Join(",", Tags));
    }
}

record ContentItem(string id, string name, string mimeType, string previewUrl, string tags);
record ContentResponse(ContentItem[] content, int contentCount, int offset);
record DownloadUrlResponse(string downloadUrl);
record OAuthTokenResponse(string access_token, string token_type, int expires_in);

