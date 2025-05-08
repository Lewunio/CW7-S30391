using CW7_S30391.Exceptions;
using CW7_S30391.Models.DTOs;
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
    
    //dodaj klienta do bazy
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] ClientCreateDTO body)
    {
        var client = await dbService.CreateClientAsync(body);
        return Created($"clients/{client.IdClient}", client);
    }

    //przypisuje wyczieczkie do klienta po idkach jesli oboje istnieja, dokladnie robi rekord w Client_trip
    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterTripToClient([FromRoute] int id, [FromRoute] int tripId)
    {
        try
        {
            await dbService.PutTripToClientAsync(id, tripId);
            return Ok("Zapisano wycieczke dla klienta");
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
    }

    //usun przypisanie klienta do wycieczki
    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> DeleteTripFromClientById([FromRoute] int id, [FromRoute] int tripId)
    {
        try
        {
            await dbService.RemoveTripFromClientAsync(id, tripId);   
            return NoContent();
        } catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
    
}