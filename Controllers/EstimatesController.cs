using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers;

[Authorize]
[ApiController]
[Route("estimates")]
public class EstimatesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ServiceTitanSyncEngine _syncEngine;

    public EstimatesController(AppDbContext context, ServiceTitanSyncEngine syncEngine)
    {
        _context = context;
        _syncEngine = syncEngine;
    }

    // GET /estimates — list all open estimates with follow-up data
    [HttpGet]
    public async Task<IActionResult> GetEstimates([FromQuery] string? outcome = null)
    {
        var query = _context.Estimates
            .Include(e => e.Customer)
            .Include(e => e.FollowUp)
                .ThenInclude(f => f == null ? null : f.Notes)
            .Where(e => e.IsActive)
            .AsQueryable();

        // Filter by outcome if provided (Pending, Won, Lost)
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            if (outcome == "Pending")
                query = query.Where(e => e.FollowUp == null || e.FollowUp.Outcome == "Pending");
            else
                query = query.Where(e => e.FollowUp != null && e.FollowUp.Outcome == outcome);
        }

        var estimates = await query
            .OrderByDescending(e => e.CreatedOn)
            .Select(e => new
            {
                e.Id,
                e.ServiceTitanEstimateId,
                e.JobNumber,
                e.EstimateName,
                e.Status,
                e.ReviewStatus,
                e.Summary,
                e.BusinessUnitName,
                e.Subtotal,
                e.Tax,
                Total = e.Subtotal + e.Tax,
                e.CreatedOn,
                e.ModifiedOn,
                CustomerName = e.Customer != null ? e.Customer.Name : $"Customer #{e.ServiceTitanCustomerId}",
                FollowUp = e.FollowUp == null ? null : new
                {
                    e.FollowUp.Id,
                    e.FollowUp.FollowUpDate,
                    e.FollowUp.AssignedTo,
                    e.FollowUp.Outcome,
                    e.FollowUp.UpdatedAt,
                    Notes = e.FollowUp.Notes
                        .OrderByDescending(n => n.CreatedAt)
                        .Select(n => new { n.Id, n.Text, n.Author, n.CreatedAt })
                        .ToList()
                }
            })
            .ToListAsync();

        return Ok(estimates);
    }

    // POST /estimates/sync — manual sync trigger
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        await _syncEngine.SyncEstimatesAsync();
        return Ok(new { message = "Estimates sync complete" });
    }

    // POST /estimates/{id}/followup — create or update follow-up record
    [HttpPost("{id}/followup")]
    public async Task<IActionResult> UpsertFollowUp(Guid id, [FromBody] FollowUpRequest req)
    {
        var estimate = await _context.Estimates
            .Include(e => e.FollowUp)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estimate == null) return NotFound();

        if (estimate.FollowUp == null)
        {
            estimate.FollowUp = new EstimateFollowUp
            {
                Id = Guid.NewGuid(),
                EstimateId = id,
                FollowUpDate = req.FollowUpDate,
                AssignedTo = req.AssignedTo ?? "",
                Outcome = req.Outcome ?? "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.EstimateFollowUps.Add(estimate.FollowUp);
        }
        else
        {
            if (req.FollowUpDate.HasValue) estimate.FollowUp.FollowUpDate = req.FollowUpDate;
            if (req.AssignedTo != null)    estimate.FollowUp.AssignedTo   = req.AssignedTo;
            if (req.Outcome != null)       estimate.FollowUp.Outcome      = req.Outcome;
            estimate.FollowUp.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(estimate.FollowUp);
    }

    // POST /estimates/{id}/followup/notes — add a call attempt note
    [HttpPost("{id}/followup/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] NoteRequest req)
    {
        var followUp = await _context.EstimateFollowUps
            .FirstOrDefaultAsync(f => f.EstimateId == id);

        // Auto-create follow-up record if one doesn't exist yet
        if (followUp == null)
        {
            followUp = new EstimateFollowUp
            {
                Id = Guid.NewGuid(),
                EstimateId = id,
                Outcome = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.EstimateFollowUps.Add(followUp);
            await _context.SaveChangesAsync();
        }

        var note = new EstimateFollowUpNote
        {
            Id = Guid.NewGuid(),
            FollowUpId = followUp.Id,
            Text = req.Text ?? "",
            Author = req.Author ?? "",
            CreatedAt = DateTime.UtcNow
        };

        _context.EstimateFollowUpNotes.Add(note);
        followUp.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(note);
    }

    // DELETE /estimates/{id}/followup/notes/{noteId}
    [HttpDelete("{id}/followup/notes/{noteId}")]
    public async Task<IActionResult> DeleteNote(Guid id, Guid noteId)
    {
        var note = await _context.EstimateFollowUpNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.FollowUp.EstimateId == id);

        if (note == null) return NotFound();

        _context.EstimateFollowUpNotes.Remove(note);
        await _context.SaveChangesAsync();
        return Ok(new { deleted = true });
    }
}

public class FollowUpRequest
{
    public DateTime? FollowUpDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? Outcome { get; set; }
}

public class NoteRequest
{
    public string? Text { get; set; }
    public string? Author { get; set; }
}