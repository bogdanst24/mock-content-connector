using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ---------------------------------------------------------------------------
// Sample data — picsum.photos serves stable, freely-usable images by ID.
// ---------------------------------------------------------------------------
var assets = new Asset[]
{
    new("1",  "Mountain Landscape",   "image/jpeg", "https://picsum.photos/id/10/300/200",  "https://picsum.photos/id/10/1600/1067",  ["nature", "landscape", "mountain"]),
    new("2",  "City at Night",        "image/jpeg", "https://picsum.photos/id/20/300/200",  "https://picsum.photos/id/20/1600/1067",  ["city", "urban", "night"]),
    new("3",  "Forest Path",          "image/jpeg", "https://picsum.photos/id/30/300/200",  "https://picsum.photos/id/30/1600/1067",  ["nature", "forest", "trees"]),
    new("4",  "Ocean Waves",          "image/jpeg", "https://picsum.photos/id/40/300/200",  "https://picsum.photos/id/40/1600/1067",  ["ocean", "waves", "nature"]),
    new("5",  "Desert Dunes",         "image/jpeg", "https://picsum.photos/id/50/300/200",  "https://picsum.photos/id/50/1600/1067",  ["desert", "sand", "landscape"]),
    new("6",  "Snowy Peaks",          "image/jpeg", "https://picsum.photos/id/60/300/200",  "https://picsum.photos/id/60/1600/1067",  ["snow", "mountain", "winter"]),
    new("7",  "Autumn Colours",       "image/jpeg", "https://picsum.photos/id/70/300/200",  "https://picsum.photos/id/70/1600/1067",  ["autumn", "leaves", "nature"]),
    new("8",  "Tropical Beach",       "image/jpeg", "https://picsum.photos/id/80/300/200",  "https://picsum.photos/id/80/1600/1067",  ["beach", "tropical", "ocean"]),
    new("9",  "Abstract Architecture","image/jpeg", "https://picsum.photos/id/90/300/200",  "https://picsum.photos/id/90/1600/1067",  ["architecture", "abstract", "building"]),
    new("10", "Rustic Farm",          "image/jpeg", "https://picsum.photos/id/100/300/200", "https://picsum.photos/id/100/1600/1067", ["farm", "rural", "nature"]),
    new("11", "Waterfall",            "image/jpeg", "https://picsum.photos/id/110/300/200", "https://picsum.photos/id/110/1600/1067", ["waterfall", "nature", "water"]),
    new("12", "Vintage Street",       "image/jpeg", "https://picsum.photos/id/120/300/200", "https://picsum.photos/id/120/1600/1067", ["street", "vintage", "city"]),
};

const string MockToken = "mock-access-token";
const string AuthorizationCodeValue = "mock-auth-code";

// ---------------------------------------------------------------------------
// POST /oauth/token
// ---------------------------------------------------------------------------
app.MapPost("/oauth/token", ([FromForm] OAuthTokenRequest request) =>
{
    if (request.grant_type == "client_credentials")
    {
        return Results.Ok(new OAuthTokenResponse(MockToken, "Bearer", 3600));
    }

    if (request.grant_type == "authorization_code" && request.code == AuthorizationCodeValue)
    {
        return Results.Ok(new OAuthTokenResponse(MockToken, "Bearer", 3600));
    }

    return Results.BadRequest(new { error = "unsupported_grant_type" });
})
.DisableAntiforgery();

// ---------------------------------------------------------------------------
// GET /oauth/authorize  (Authorization Code flow — presents a login page)
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
    var html = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Mock Content Connector — Sign In</title>
          <style>
            body {{ font-family: system-ui, sans-serif; display: flex; align-items: center;
                    justify-content: center; min-height: 100vh; margin: 0;
                    background: #f5f5f5; }}
            .card {{ background: white; padding: 2rem; border-radius: 8px;
                     box-shadow: 0 2px 8px rgba(0,0,0,.12); width: 320px; }}
            h1 {{ font-size: 1.25rem; margin: 0 0 1.5rem; color: #111; }}
            label {{ display: block; font-size: .875rem; color: #555; margin-bottom: .25rem; }}
            input {{ width: 100%; box-sizing: border-box; padding: .5rem .75rem;
                     border: 1px solid #ccc; border-radius: 4px; font-size: 1rem;
                     margin-bottom: 1rem; }}
            button {{ width: 100%; padding: .625rem; background: #1a56db; color: white;
                      border: none; border-radius: 4px; font-size: 1rem; cursor: pointer; }}
            button:hover {{ background: #1648c0; }}
            .hint {{ font-size: .75rem; color: #888; margin-top: 1rem; text-align: center; }}
          </style>
        </head>
        <body>
          <div class="card">
            <h1>Mock Content Connector</h1>
            <form method="post" action="/oauth/authorize/callback">
              <input type="hidden" name="redirect_uri" value="{HtmlEncode(redirect_uri ?? "")}">
              <input type="hidden" name="state"        value="{HtmlEncode(state ?? "")}">
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
// POST /oauth/authorize/callback  (accepts the login form, redirects back)
// ---------------------------------------------------------------------------
app.MapPost("/oauth/authorize/callback", ([FromForm] AuthorizeCallbackRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.redirect_uri))
        return Results.BadRequest("redirect_uri is required");

    var separator = request.redirect_uri.Contains('?') ? '&' : '?';
    var location = $"{request.redirect_uri}{separator}code={AuthorizationCodeValue}&state={Uri.EscapeDataString(request.state ?? "")}";
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
    if (!IsAuthorized(ctx))
        return Results.Unauthorized();

    // This mock only serves images; other contentTypes return an empty list.
    if (contentType != null && contentType != "image")
        return Results.Ok(new ContentResponse([], 0, skip));

    var filtered = assets.AsEnumerable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.ToLowerInvariant();
        filtered = filtered.Where(a =>
            a.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    var total   = filtered.Count();
    var page    = filtered.Skip(skip).Take(limit).Select(a => a.ToContentItem()).ToArray();

    return Results.Ok(new ContentResponse(page, total, skip));
});

// ---------------------------------------------------------------------------
// GET /content/{assetId}/download-url
// ---------------------------------------------------------------------------
app.MapGet("/content/{assetId}/download-url", (HttpContext ctx, string assetId) =>
{
    if (!IsAuthorized(ctx))
        return Results.Unauthorized();

    var asset = assets.FirstOrDefault(a => a.Id == assetId);
    if (asset is null)
        return Results.NotFound();

    return Results.Ok(new DownloadUrlResponse(asset.DownloadUrl));
});

app.Run();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static bool IsAuthorized(HttpContext ctx)
{
    var header = ctx.Request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
}

static string HtmlEncode(string value) =>
    System.Net.WebUtility.HtmlEncode(value);

// ---------------------------------------------------------------------------
// Records / DTOs
// ---------------------------------------------------------------------------
record Asset(
    string Id,
    string Name,
    string MimeType,
    string PreviewUrl,
    string DownloadUrl,
    string[] Tags)
{
    public ContentItem ToContentItem() =>
        new(Id, Name, MimeType, PreviewUrl, string.Join(",", Tags));
}

record ContentItem(string id, string name, string mimeType, string previewUrl, string tags);
record ContentResponse(ContentItem[] content, int contentCount, int offset);
record DownloadUrlResponse(string downloadUrl);
record OAuthTokenResponse(string access_token, string token_type, int expires_in);

record OAuthTokenRequest(
    string? grant_type,
    string? client_id,
    string? client_secret,
    string? code,
    string? redirect_uri,
    string? code_verifier);

record AuthorizeCallbackRequest(string? redirect_uri, string? state);
