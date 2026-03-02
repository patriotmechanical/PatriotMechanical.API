using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public class ServiceTitanSyncEngine
    {
        private readonly AppDbContext _context;
        private readonly ServiceTitanService _service;

        public ServiceTitanSyncEngine(AppDbContext context, ServiceTitanService service)
        {
            _context = context;
            _service = service;
        }

        // ═══════════════════════════════════════════════════════════════
        // CUSTOMERS (export endpoint - continuation token based)
        // ═══════════════════════════════════════════════════════════════

        public async Task SyncCustomersAsync(bool fullSync = false)
        {
            var syncState = await _context.ServiceTitanSyncStates
                .FirstOrDefaultAsync(s => s.EntityName == "Customers");

            var continuationToken = syncState?.ContinuationToken;
            bool hasMore;

            do
            {
                var raw = await _service.ExportCustomersAsync(continuationToken);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                continuationToken = parsed.GetProperty("continueFrom").GetString();
                var customers = parsed.GetProperty("data");

                foreach (var cust in customers.EnumerateArray())
                {
                    var stId = cust.GetProperty("id").GetInt64();
                    var name = cust.GetProperty("name").GetString();
                    var modifiedOn = cust.GetProperty("modifiedOn").GetDateTime();

                    var existing = await _context.Customers
                        .FirstOrDefaultAsync(c => c.ServiceTitanCustomerId == stId);

                    if (existing == null)
                    {
                        _context.Customers.Add(new Customer
                        {
                            ServiceTitanCustomerId = stId,
                            Name = name ?? "Unknown",
                            ServiceTitanModifiedOn = modifiedOn,
                            LastSyncedFromServiceTitan = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.Name = name ?? existing.Name;
                        existing.ServiceTitanModifiedOn = modifiedOn;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                    }
                }

                if (syncState == null)
                {
                    syncState = new ServiceTitanSyncState { EntityName = "Customers" };
                    _context.ServiceTitanSyncStates.Add(syncState);
                }

                syncState.ContinuationToken = continuationToken;
                syncState.LastSynced = DateTime.UtcNow;

                await _context.SaveChangesAsync();

            } while (fullSync && hasMore);
        }

        // ═══════════════════════════════════════════════════════════════
        // JOBS - Export (background incremental sync - continuation token)
        // ═══════════════════════════════════════════════════════════════

        public async Task SyncJobsAsync(bool fullSync = false)
        {
            // Build a jobTypeId -> name lookup first
            var jobTypeMap = await _service.GetJobTypeMapAsync();

            var syncState = await _context.ServiceTitanSyncStates
                .FirstOrDefaultAsync(s => s.EntityName == "Jobs");

            var continuationToken = syncState?.ContinuationToken;
            bool hasMore;

            do
            {
                var raw = await _service.ExportJobsAsync(continuationToken);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                continuationToken = parsed.GetProperty("continueFrom").GetString();
                var jobs = parsed.GetProperty("data");

                foreach (var job in jobs.EnumerateArray())
                {
                    var jobId = job.GetProperty("id").GetInt64();
                    var modifiedOn = job.GetProperty("modifiedOn").GetDateTime();
                    var jobNumber = job.GetProperty("jobNumber").GetString();
                    var total = job.GetProperty("total").GetDecimal();
                    var status = job.GetProperty("jobStatus").GetString();
                    var customerId = job.GetProperty("customerId").GetInt64();

                    // Map jobTypeId to name using lookup
                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jobTypeIdProp) &&
                        jobTypeIdProp.ValueKind == JsonValueKind.Number)
                    {
                        var jobTypeId = jobTypeIdProp.GetInt64();
                        jobTypeMap.TryGetValue(jobTypeId, out jobTypeName);
                    }

                    // Pull completedOn safely
                    DateTime? completedAt = null;
                    if (job.TryGetProperty("completedOn", out var completedProp) &&
                        completedProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(completedProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedCompleted))
                    {
                        completedAt = DateTime.SpecifyKind(parsedCompleted, DateTimeKind.Utc);
                    }

                    // Pull createdOn safely
                    DateTime createdAt = DateTime.UtcNow;
                    if (job.TryGetProperty("createdOn", out var createdProp) &&
                        createdProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(createdProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedCreated))
                    {
                        createdAt = DateTime.SpecifyKind(parsedCreated, DateTimeKind.Utc);
                    }

                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.ServiceTitanCustomerId == customerId);

                    if (customer == null) continue;

                    var existing = await _context.WorkOrders
                        .FirstOrDefaultAsync(w => w.ServiceTitanJobId == jobId);

                    if (existing == null)
                    {
                        _context.WorkOrders.Add(new WorkOrder
                        {
                            ServiceTitanJobId = jobId,
                            ServiceTitanModifiedOn = modifiedOn,
                            LastSyncedFromServiceTitan = DateTime.UtcNow,
                            JobNumber = jobNumber!,
                            Status = status!,
                            TotalAmount = total,
                            TotalRevenueCalculated = total,
                            CustomerId = customer.Id,
                            JobTypeName = jobTypeName,
                            CompletedAt = completedAt,
                            CreatedAt = createdAt
                        });
                    }
                    else
                    {
                        existing.JobNumber = jobNumber!;
                        existing.Status = status!;
                        existing.TotalAmount = total;
                        existing.TotalRevenueCalculated = total;
                        existing.ServiceTitanModifiedOn = modifiedOn;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                        existing.JobTypeName = jobTypeName;
                        if (completedAt.HasValue) existing.CompletedAt = completedAt;
                        existing.CreatedAt = createdAt;
                    }
                }

                if (syncState == null)
                {
                    syncState = new ServiceTitanSyncState { EntityName = "Jobs" };
                    _context.ServiceTitanSyncStates.Add(syncState);
                }

                syncState.ContinuationToken = continuationToken;
                syncState.LastSynced = DateTime.UtcNow;

                await _context.SaveChangesAsync();

            } while (fullSync && hasMore);
        }

        // ═══════════════════════════════════════════════════════════════
        // JOBS - Manual Refresh (list endpoint - modifiedOnOrAfter)
        // Bypasses continuation tokens for instant status updates
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Manual refresh: Uses the Jobs list endpoint with modifiedOnOrAfter
        /// to immediately fetch all recently changed jobs without relying on
        /// continuation tokens. This is what the dashboard "Sync Data" button calls.
        /// </summary>
        public async Task<int> RefreshRecentJobsAsync(int lookbackHours = 24)
        {
            var jobTypeMap = await _service.GetJobTypeMapAsync();
            var modifiedSince = DateTime.UtcNow.AddHours(-lookbackHours);
            int totalUpdated = 0;
            int page = 1;
            bool hasMore;

            do
            {
                var raw = await _service.GetRecentJobsPageAsync(modifiedSince, page, 200);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                var jobs = parsed.GetProperty("data");

                foreach (var job in jobs.EnumerateArray())
                {
                    var jobId = job.GetProperty("id").GetInt64();
                    var jobNumber = job.GetProperty("jobNumber").GetString();
                    var total = job.GetProperty("total").GetDecimal();
                    var status = job.GetProperty("jobStatus").GetString();
                    var customerId = job.GetProperty("customerId").GetInt64();

                    // Parse modifiedOn from list endpoint (string format)
                    DateTime? modifiedOn = null;
                    if (job.TryGetProperty("modifiedOn", out var modProp) &&
                        modProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(modProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedMod))
                    {
                        modifiedOn = DateTime.SpecifyKind(parsedMod, DateTimeKind.Utc);
                    }

                    // Map jobTypeId to name
                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jobTypeIdProp) &&
                        jobTypeIdProp.ValueKind == JsonValueKind.Number)
                    {
                        var jobTypeId = jobTypeIdProp.GetInt64();
                        jobTypeMap.TryGetValue(jobTypeId, out jobTypeName);
                    }

                    // Pull completedOn
                    DateTime? completedAt = null;
                    if (job.TryGetProperty("completedOn", out var completedProp) &&
                        completedProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(completedProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedCompleted))
                    {
                        completedAt = DateTime.SpecifyKind(parsedCompleted, DateTimeKind.Utc);
                    }

                    // Pull createdOn
                    DateTime createdAt = DateTime.UtcNow;
                    if (job.TryGetProperty("createdOn", out var createdProp) &&
                        createdProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(createdProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedCreated))
                    {
                        createdAt = DateTime.SpecifyKind(parsedCreated, DateTimeKind.Utc);
                    }

                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.ServiceTitanCustomerId == customerId);

                    if (customer == null) continue;

                    var existing = await _context.WorkOrders
                        .FirstOrDefaultAsync(w => w.ServiceTitanJobId == jobId);

                    if (existing == null)
                    {
                        _context.WorkOrders.Add(new WorkOrder
                        {
                            ServiceTitanJobId = jobId,
                            ServiceTitanModifiedOn = modifiedOn,
                            LastSyncedFromServiceTitan = DateTime.UtcNow,
                            JobNumber = jobNumber!,
                            Status = status!,
                            TotalAmount = total,
                            TotalRevenueCalculated = total,
                            CustomerId = customer.Id,
                            JobTypeName = jobTypeName,
                            CompletedAt = completedAt,
                            CreatedAt = createdAt
                        });
                    }
                    else
                    {
                        existing.JobNumber = jobNumber!;
                        existing.Status = status!;
                        existing.TotalAmount = total;
                        existing.TotalRevenueCalculated = total;
                        existing.ServiceTitanModifiedOn = modifiedOn;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                        existing.JobTypeName = jobTypeName;
                        if (completedAt.HasValue) existing.CompletedAt = completedAt;
                        existing.CreatedAt = createdAt;
                    }

                    totalUpdated++;
                }

                await _context.SaveChangesAsync();
                page++;

            } while (hasMore);

            return totalUpdated;
        }

        // ═══════════════════════════════════════════════════════════════
        // INVOICES
        // ═══════════════════════════════════════════════════════════════

        public async Task SyncInvoicesAsync()
        {
            int page = 1;
            bool hasMore;

            do
            {
                var raw = await _service.GetInvoicesPageAsync(page);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                var invoices = parsed.GetProperty("data");

                foreach (var inv in invoices.EnumerateArray())
                {
                    var invoiceId = inv.GetProperty("id").GetInt64();

                    string invoiceNumber =
                        inv.TryGetProperty("referenceNumber", out var refProp) &&
                        refProp.ValueKind == JsonValueKind.String
                            ? refProp.GetString() ?? $"INV-{invoiceId}"
                            : $"INV-{invoiceId}";

                    long customerId = inv.GetProperty("customer").GetProperty("id").GetInt64();

                    long jobId = 0;
                    if (inv.TryGetProperty("job", out var jobProp) && jobProp.ValueKind != JsonValueKind.Null)
                        jobId = jobProp.GetProperty("id").GetInt64();

                    decimal total = decimal.Parse(inv.GetProperty("total").GetString() ?? "0");
                    decimal balance = decimal.Parse(inv.GetProperty("balance").GetString() ?? "0");

                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.ServiceTitanCustomerId == customerId);
                    if (customer == null) continue;

                    WorkOrder? workOrder = null;
                    if (jobId > 0)
                        workOrder = await _context.WorkOrders
                            .FirstOrDefaultAsync(w => w.ServiceTitanJobId == jobId);

                    var existing = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.ServiceTitanInvoiceId == invoiceId);

                    if (existing == null)
                    {
                        _context.Invoices.Add(new Invoice
                        {
                            ServiceTitanInvoiceId = invoiceId,
                            InvoiceNumber = invoiceNumber,
                            CustomerId = customer.Id,
                            WorkOrderId = workOrder?.Id,
                            TotalAmount = total,
                            BalanceRemaining = balance,
                            LastSyncedFromServiceTitan = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.TotalAmount = total;
                        existing.BalanceRemaining = balance;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                page++;

            } while (hasMore);
        }
    }
}