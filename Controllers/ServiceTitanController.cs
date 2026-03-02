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

        [HttpPost("sync/customers")]
        public async Task<IActionResult> SyncCustomers()
        {
            await _syncEngine.SyncCustomersAsync(fullSync: true);
            return Ok(new { message = "Full customer sync complete" });
        }

        [HttpPost("sync/jobs")]
        public async Task<IActionResult> SyncJobs()
        {
            await _syncEngine.SyncJobsAsync(fullSync: true);
            return Ok(new { message = "Full job sync complete" });
        }

        [HttpPost("sync/invoices")]
        public async Task<IActionResult> SyncInvoices()
        {
            await _syncEngine.SyncInvoicesAsync();
            return Ok(new { message = "Invoice sync complete" });
        }

        // Quick refresh - pulls recently modified jobs via list API
        // This catches status changes (cancelled, completed, etc.) that
        // the export continuation token may have already passed.
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRecent()
        {
            var updated = await _syncEngine.RefreshRecentJobsAsync(lookbackHours: 72);

            // Appointment sync for auto-board (non-fatal if it fails)
            string apptMessage = "";
            try
            {
                await _syncEngine.SyncAppointmentsAndAutoBoardAsync();
                apptMessage = "Appointment sync complete.";
            }
            catch (Exception ex)
            {
                apptMessage = $"Appointment sync skipped: {ex.Message}";
                Console.WriteLine($"[Appointment Sync] {apptMessage}");
            }

            return Ok(new { message = $"Refreshed {updated} jobs. {apptMessage}" });
        }
    }
}