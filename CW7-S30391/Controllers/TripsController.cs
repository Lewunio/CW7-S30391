using Microsoft.AspNetCore.Mvc;
using CW7_S30391.Services;

namespace CW7_S30391.Controllers;
[ApiController]
[Route("[controller]")]
public class TripsController(IDbService dbService) : ControllerBase
{
    //zwroc wszystkie wycieczki z krajami
    [HttpGet]
    public async Task<ActionResult> GetAllTrips()
    {
        return Ok(await dbService.GetTripsDetailsAsync());
    }
}