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