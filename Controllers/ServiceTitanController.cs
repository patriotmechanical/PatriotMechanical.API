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

        // ─── MANUAL REFRESH (dashboard button) ───────────────────────
        // Uses the list endpoint with modifiedOnOrAfter for instant results.
        // One click catches all changes from the last 24 hours.
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRecent()
        {
            // Sync customers with full export (they change less often)
            await _syncEngine.SyncCustomersAsync(fullSync: true);

            // Refresh jobs using list endpoint — catches all changes in last 24h immediately
            var jobsUpdated = await _syncEngine.RefreshRecentJobsAsync(lookbackHours: 24);

            // Sync invoices normally
            await _syncEngine.SyncInvoicesAsync();

            return Ok(new
            {
                message = "Dashboard refreshed",
                jobsUpdated
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