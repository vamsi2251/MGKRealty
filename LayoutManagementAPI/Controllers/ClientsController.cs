using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public ClientsController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<ClientDto>>> GetAll()
    {
        var name = Caller();
        var clients = await _db.Clients
            .Where(client => client.CreatedBy.ToLower() == name)
            .OrderBy(client => client.Name)
            .ToListAsync();
        return clients.Select(client => client.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id)
    {
        var client = await OwnedClientAsync(id);
        return client is null ? NotFound() : client.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientRequest request)
    {
        var client = new Client { CreatedBy = User.Identity?.Name ?? "" };
        client.Apply(request);
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClientDto>> Update(int id, ClientRequest request)
    {
        var client = await OwnedClientAsync(id);
        if (client is null)
        {
            return NotFound();
        }

        client.Apply(request);
        await _db.SaveChangesAsync();
        return client.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await OwnedClientAsync(id);
        if (client is null)
        {
            return NotFound();
        }

        if (await _db.Plots.AnyAsync(plot => plot.ClientId == id))
        {
            return Conflict("Remove this client from plots before deleting the client.");
        }

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private string Caller() => (User.Identity?.Name ?? "").ToLower();

    private async Task<Client?> OwnedClientAsync(int id)
    {
        var name = Caller();
        return await _db.Clients.FirstOrDefaultAsync(client => client.Id == id && client.CreatedBy.ToLower() == name);
    }
}
