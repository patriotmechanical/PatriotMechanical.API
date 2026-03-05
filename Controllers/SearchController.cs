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

        [HttpPost("sync/invoices")]
        public async Task<IActionResult> SyncInvoices()
        {
            try
            {
                await _syncEngine.SyncInvoicesAsync();
                return Ok(new { message = "Invoice sync complete" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Invoice Sync Error] {ex.Message}");
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpPost("sync/jobs")]
        public async Task<IActionResult> SyncJobs()
        {
            await _syncEngine.SyncJobsAsync(fullSync: true);
            return Ok(new { message = "Full job sync complete" });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRecent()
        {
            var updated = await _syncEngine.RefreshRecentJobsAsync(lookbackHours: 72);

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