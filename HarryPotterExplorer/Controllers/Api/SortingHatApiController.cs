using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers.Api;

[ApiController]
[Route("api/sorting-hat")]
[Produces("application/json")]
public class SortingHatApiController(ISortingHatService hat) : ControllerBase
{
    /// <summary>
    /// Scores a completed quiz. The answer key stays on the server, so the page cannot be
    /// read to work out which option leads where.
    /// </summary>
    [HttpPost("")]
    public ActionResult<SortingVerdict> Sort([FromBody] Dictionary<string, string> answers)
    {
        if (answers.Count == 0)
        {
            return BadRequest(new { message = "The Hat needs at least one answer." });
        }

        return Ok(hat.Sort(answers));
    }
}
