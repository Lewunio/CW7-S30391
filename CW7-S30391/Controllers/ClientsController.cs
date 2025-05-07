using CW7_S30391.Exceptions;
using CW7_S30391.Services;
using Microsoft.AspNetCore.Mvc;

namespace CW7_S30391.Controllers;

[ApiController]
[Route("[controller]")]
public class ClientsController(IDbService dbService) : ControllerBase
{
    //zwroc liste wycieczek danego poprzez id klienta
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetTripsOfClient([FromRoute] int id)
    {
        try
        {
            return Ok(await dbService.GetTripsDetailsOfClientAsync(id));
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}