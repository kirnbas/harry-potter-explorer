using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("sorting-hat")]
public class SortingHatController(ISortingHatService hat) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(new SortingHatViewModel(hat.Questions));
}
