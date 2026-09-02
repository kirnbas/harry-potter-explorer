using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers.Api;

public sealed record ToggleCollectionRequest(bool Collected);

[ApiController]
[Route("api/collection")]
[Produces("application/json")]
public class CollectionApiController(ICollectionService collection) : ControllerBase
{
    private const string VisitorCookie = "hpx_visitor";

    [HttpPost("{characterId}")]
    public async Task<ActionResult<CollectionResult>> Toggle(
        string characterId, [FromBody] ToggleCollectionRequest request, CancellationToken ct)
    {
        var visitorId = ResolveVisitorId();
        var result = await collection.ToggleAsync(characterId, visitorId, request.Collected, ct);

        return result is null
            ? NotFound(new { message = "No such character." })
            : Ok(result);
    }

    /// <summary>
    /// An anonymous, first-party id used only to stop one browser inflating the public
    /// tally by toggling the same card repeatedly. It identifies a browser, not a person:
    /// no name, no email, nothing to correlate against.
    /// </summary>
    private string ResolveVisitorId()
    {
        if (Request.Cookies.TryGetValue(VisitorCookie, out var existing) &&
            Guid.TryParse(existing, out _))
        {
            return existing;
        }

        var visitorId = Guid.NewGuid().ToString();

        Response.Cookies.Append(VisitorCookie, visitorId, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        return visitorId;
    }
}
