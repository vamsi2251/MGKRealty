using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Data;
using LayoutManagementAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public AgentsController(RealEstateDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<AgentDto>>> GetAll()
    {
        var name = Caller();
        var agents = await _db.Agents
            .Where(agent => agent.CreatedBy.ToLower() == name)
            .OrderBy(agent => agent.Name)
            .ToListAsync();
        return agents.Select(agent => agent.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDto>> Get(int id)
    {
        var agent = await OwnedAgentAsync(id);
        return agent is null ? NotFound() : agent.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<AgentDto>> Create(AgentRequest request)
    {
        var agent = new Agent { CreatedBy = User.Identity?.Name ?? "" };
        agent.Apply(request);
        _db.Agents.Add(agent);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = agent.Id }, agent.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AgentDto>> Update(int id, AgentRequest request)
    {
        var agent = await OwnedAgentAsync(id);
        if (agent is null)
        {
            return NotFound();
        }

        agent.Apply(request);
        await _db.SaveChangesAsync();
        return agent.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var agent = await OwnedAgentAsync(id);
        if (agent is null)
        {
            return NotFound();
        }

        if (await _db.Properties.AnyAsync(p => p.AgentId == id))
        {
            return Conflict("Reassign this agent's listings before deleting the agent.");
        }

        if (await _db.Plots.AnyAsync(plot => plot.AgentId == id))
        {
            return Conflict("Remove this agent from plots before deleting the agent.");
        }

        _db.Agents.Remove(agent);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private string Caller() => (User.Identity?.Name ?? "").ToLower();

    private async Task<Agent?> OwnedAgentAsync(int id)
    {
        var name = Caller();
        return await _db.Agents.FirstOrDefaultAsync(agent => agent.Id == id && agent.CreatedBy.ToLower() == name);
    }
}
