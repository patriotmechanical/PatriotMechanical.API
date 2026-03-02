using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatriotMechanical.API.Application.Services;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("servicetitan")]
    public class ServiceTitanController : ControllerBase
    {
        private readonly ServiceTitanSyncEngine _syncEngine;

        public ServiceTitanController(ServiceTitanSyncEngine syncEngine)
        {
            _syncEngine = syncEngine;
        }

        // Temporary diagnostic — remove after debugging
        [HttpGet("refresh/test")]
        public IActionResult RefreshTest()
        {
            return Ok(new { message = "Refresh endpoint is reachable", timestamp = DateTime.UtcNow });
        }

        // ─── MANUAL REFRESH (dashboard button) ───────────────────────
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRecent()
        {
            var errors = new List<string>();
            int jobsUpdated = 0;

            // Sync customers — don't let failure block the rest
            try
            {
                await _syncEngine.SyncCustomersAsync(fullSync: true);
            }
            catch (Exception ex)
            {
                errors.Add($"Customer sync failed: {ex.Message}");
            }

            // Refresh jobs — the main reason for this endpoint
            try
            {
                jobsUpdated = await _syncEngine.RefreshRecentJobsAsync(lookbackHours: 24);
            }
            catch (Exception ex)
            {
                errors.Add($"Job refresh failed: {ex.Message}");
            }

            // Sync invoices
            try
            {
                await _syncEngine.SyncInvoicesAsync();
            }
            catch (Exception ex)
            {
                errors.Add($"Invoice sync failed: {ex.Message}");
            }

            return Ok(new
            {
                message = errors.Count == 0 ? "Dashboard refreshed" : "Refresh completed with errors",
                jobsUpdated,
                errors
            });
        }

        // ─── FULL SYNC (background service / admin use) ─────────────
        [HttpPost("sync/customers")]
        public async Task<IActionResult> SyncCustomers()
        {
            await _syncEngine.SyncCustomersAsync(fullSync: true);
            return Ok(new { message = "Full customer sync complete" });
        }

        [HttpPost("sync/invoices")]
        public async Task<IActionResult> SyncInvoices()
        {
            await _syncEngine.SyncInvoicesAsync();
            return Ok(new { message = "Invoice sync complete" });
        }

        [HttpPost("sync/jobs")]
        public async Task<IActionResult> SyncJobs()
        {
            await _syncEngine.SyncJobsAsync(fullSync: true);
            return Ok(new { message = "Full job sync complete" });
        }
    }
}