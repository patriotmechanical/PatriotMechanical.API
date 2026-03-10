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

                    long locationId = 0;
                    if (job.TryGetProperty("locationId", out var locIdProp) && locIdProp.ValueKind == JsonValueKind.Number)
                        locationId = locIdProp.GetInt64();

                    long? holdReasonId = null;
                    if (job.TryGetProperty("holdReasonId", out var hrProp) && hrProp.ValueKind == JsonValueKind.Number)
                        holdReasonId = hrProp.GetInt64();

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
                            ServiceTitanLocationId = locationId,
                            JobTypeName = jobTypeName,
                            CompletedAt = completedAt,
                            CreatedAt = createdAt,
                            HoldReasonId = holdReasonId
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
                        existing.ServiceTitanLocationId = locationId;
                        if (completedAt.HasValue) existing.CompletedAt = completedAt;
                        existing.CreatedAt = createdAt;
                        existing.HoldReasonId = holdReasonId; // always update (clears when hold removed)
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
        // Bypasses continuation tokens for instant status updates.
        //
        // NOTE: The list endpoint returns a DIFFERENT shape than the
        // export endpoint. Key differences:
        //   - No "total" field (dollar amount) on list response
        //   - "modifiedOn" is a string date, not a DateTime
        //   - Some fields may be absent depending on job state
        // We use TryGetProperty for everything to handle both safely.
        // ═══════════════════════════════════════════════════════════════

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
                    var customerId = job.GetProperty("customerId").GetInt64();

                    // jobNumber - required field on both endpoints
                    var jobNumber = job.TryGetProperty("jobNumber", out var jnProp)
                        ? jnProp.GetString() : null;
                    if (string.IsNullOrEmpty(jobNumber)) continue;

                    // jobStatus - required field on both endpoints
                    var status = job.TryGetProperty("jobStatus", out var statusProp)
                        ? statusProp.GetString() : null;
                    if (string.IsNullOrEmpty(status)) continue;

                    // total - EXISTS on export endpoint but NOT on list endpoint
                    // Don't crash if it's missing; keep existing value
                    decimal? total = null;
                    if (job.TryGetProperty("total", out var totalProp))
                    {
                        if (totalProp.ValueKind == JsonValueKind.Number)
                            total = totalProp.GetDecimal();
                        else if (totalProp.ValueKind == JsonValueKind.String &&
                                 decimal.TryParse(totalProp.GetString(), out var parsedTotal))
                            total = parsedTotal;
                    }

                    // modifiedOn - string on list endpoint
                    DateTime? modifiedOn = null;
                    if (job.TryGetProperty("modifiedOn", out var modProp))
                    {
                        if (modProp.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(modProp.GetString(), null,
                                System.Globalization.DateTimeStyles.AssumeUniversal |
                                System.Globalization.DateTimeStyles.AdjustToUniversal,
                                out var parsedMod))
                        {
                            modifiedOn = DateTime.SpecifyKind(parsedMod, DateTimeKind.Utc);
                        }
                    }

                    // jobTypeId -> name
                    string? jobTypeName = null;
                    if (job.TryGetProperty("jobTypeId", out var jobTypeIdProp) &&
                        jobTypeIdProp.ValueKind == JsonValueKind.Number)
                    {
                        var jobTypeId = jobTypeIdProp.GetInt64();
                        jobTypeMap.TryGetValue(jobTypeId, out jobTypeName);
                    }

                    // completedOn
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

                    // createdOn
                    DateTime? createdAt = null;
                    if (job.TryGetProperty("createdOn", out var createdProp) &&
                        createdProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(createdProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedCreated))
                    {
                        createdAt = DateTime.SpecifyKind(parsedCreated, DateTimeKind.Utc);
                    }

                    // locationId
                    long locationId = 0;
                    if (job.TryGetProperty("locationId", out var locIdProp) && locIdProp.ValueKind == JsonValueKind.Number)
                        locationId = locIdProp.GetInt64();

                    // holdReasonId - null if not on hold, populated if job is on hold
                    long? holdReasonId = null;
                    if (job.TryGetProperty("holdReasonId", out var hrProp) && hrProp.ValueKind == JsonValueKind.Number)
                        holdReasonId = hrProp.GetInt64();

                    // Match to our customer
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.ServiceTitanCustomerId == customerId);
                    if (customer == null) continue;

                    // Match to existing work order
                    var existing = await _context.WorkOrders
                        .FirstOrDefaultAsync(w => w.ServiceTitanJobId == jobId);

                    if (existing == null)
                    {
                        // New job we haven't seen before
                        _context.WorkOrders.Add(new WorkOrder
                        {
                            ServiceTitanJobId = jobId,
                            ServiceTitanModifiedOn = modifiedOn,
                            LastSyncedFromServiceTitan = DateTime.UtcNow,
                            JobNumber = jobNumber,
                            Status = status,
                            TotalAmount = total ?? 0m,
                            TotalRevenueCalculated = total ?? 0m,
                            CustomerId = customer.Id,
                            ServiceTitanLocationId = locationId,
                            JobTypeName = jobTypeName,
                            CompletedAt = completedAt,
                            CreatedAt = createdAt ?? DateTime.UtcNow,
                            HoldReasonId = holdReasonId
                        });
                    }
                    else
                    {
                        // Update existing — always update status (the main reason for this method)
                        existing.JobNumber = jobNumber;
                        existing.Status = status;
                        existing.ServiceTitanModifiedOn = modifiedOn;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                        existing.JobTypeName = jobTypeName;
                        existing.HoldReasonId = holdReasonId; // always update (clears when hold removed)
                        if (locationId > 0) existing.ServiceTitanLocationId = locationId;

                        // Only overwrite total if the list endpoint actually returned it
                        if (total.HasValue)
                        {
                            existing.TotalAmount = total.Value;
                            existing.TotalRevenueCalculated = total.Value;
                        }

                        if (completedAt.HasValue)
                            existing.CompletedAt = completedAt;

                        if (createdAt.HasValue)
                            existing.CreatedAt = createdAt.Value;
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

                    decimal invoiceTotal = decimal.Parse(inv.GetProperty("total").GetString() ?? "0");
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

                    // Parse invoice date — ST returns "date" field (e.g. "2025-11-15T00:00:00Z")
                    DateTime invoiceDate = DateTime.MinValue;
                    if (inv.TryGetProperty("date", out var dateProp) &&
                        dateProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(dateProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedDate))
                    {
                        invoiceDate = parsedDate;
                    }

                    if (existing == null)
                    {
                        _context.Invoices.Add(new Invoice
                        {
                            ServiceTitanInvoiceId = invoiceId,
                            InvoiceNumber = invoiceNumber,
                            CustomerId = customer.Id,
                            WorkOrderId = workOrder?.Id,
                            TotalAmount = invoiceTotal,
                            BalanceRemaining = balance,
                            IssueDate = invoiceDate,
                            InvoiceDate = invoiceDate,
                            LastSyncedFromServiceTitan = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existing.TotalAmount = invoiceTotal;
                        existing.BalanceRemaining = balance;
                        existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                        // Always update IssueDate if it was missing (MinValue)
                        if (invoiceDate != DateTime.MinValue && existing.IssueDate == DateTime.MinValue)
                        {
                            existing.IssueDate = invoiceDate;
                            existing.InvoiceDate = invoiceDate;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                page++;

            } while (hasMore);
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENTS + AUTO-BOARD
        // Jobs with appointments on Hold or multiple active appointments
        // auto-add to "Need to Return" board column.
        // ═══════════════════════════════════════════════════════════════

        public async Task SyncAppointmentsAndAutoBoardAsync()
        {
            var syncState = await _context.ServiceTitanSyncStates
                .FirstOrDefaultAsync(s => s.EntityName == "Appointments");

            var continuationToken = syncState?.ContinuationToken;
            bool hasMore;
            var jobAppointments = new Dictionary<long, List<ApptInfo>>();

            do
            {
                var raw = await _service.ExportAppointmentsAsync(continuationToken);
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                hasMore = parsed.GetProperty("hasMore").GetBoolean();
                continuationToken = parsed.GetProperty("continueFrom").GetString();
                var appointments = parsed.GetProperty("data");

                foreach (var appt in appointments.EnumerateArray())
                {
                    var jobId = appt.GetProperty("jobId").GetInt64();
                    var status = appt.GetProperty("status").GetString() ?? "";
                    var active = appt.GetProperty("active").GetBoolean();
                    var unused = appt.GetProperty("unused").GetBoolean();

                    long? holdReasonId = null;
                    if (appt.TryGetProperty("holdReasonId", out var hrProp) && hrProp.ValueKind == JsonValueKind.Number)
                        holdReasonId = hrProp.GetInt64();

                    if (!jobAppointments.ContainsKey(jobId))
                        jobAppointments[jobId] = new List<ApptInfo>();

                    jobAppointments[jobId].Add(new ApptInfo { Status = status, Active = active, Unused = unused, HasTechnician = false, HoldReasonId = holdReasonId });
                }

                if (syncState == null)
                {
                    syncState = new ServiceTitanSyncState { EntityName = "Appointments" };
                    _context.ServiceTitanSyncStates.Add(syncState);
                }
                syncState.ContinuationToken = continuationToken;
                syncState.LastSynced = DateTime.UtcNow;
                await _context.SaveChangesAsync();

            } while (hasMore);

            // Pull appointment assignments to know which jobs have technicians assigned
            var jobsWithTechAssigned = new HashSet<long>();
            try
            {
                var assignSyncState = await _context.ServiceTitanSyncStates
                    .FirstOrDefaultAsync(s => s.EntityName == "AppointmentAssignments");
                var assignToken = assignSyncState?.ContinuationToken;

                do
                {
                    var raw = await _service.ExportAppointmentAssignmentsAsync(assignToken);
                    var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                    hasMore = parsed.GetProperty("hasMore").GetBoolean();
                    assignToken = parsed.GetProperty("continueFrom").GetString();
                    var assignments = parsed.GetProperty("data");

                    foreach (var assignment in assignments.EnumerateArray())
                    {
                        var active = assignment.GetProperty("active").GetBoolean();
                        if (!active) continue;

                        var jobId = assignment.GetProperty("jobId").GetInt64();
                        jobsWithTechAssigned.Add(jobId);
                    }

                    if (assignSyncState == null)
                    {
                        assignSyncState = new ServiceTitanSyncState { EntityName = "AppointmentAssignments" };
                        _context.ServiceTitanSyncStates.Add(assignSyncState);
                    }
                    assignSyncState.ContinuationToken = assignToken;
                    assignSyncState.LastSynced = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                } while (hasMore);

                Console.WriteLine($"[AutoBoard] Found {jobsWithTechAssigned.Count} jobs with active tech assignments.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoBoard] Assignment export failed (non-fatal): {ex.Message}");
            }

            // ─── Sync hold reasons → board columns (runs every sync) ───
            await SyncHoldReasonsToColumnsAsync();

            await AutoBoardFromAppointmentsAsync(jobAppointments, jobsWithTechAssigned);
        }

        // ═══════════════════════════════════════════════════════════════
        // SYNC HOLD REASONS → BOARD COLUMNS
        // Pulls active hold reasons from ST and creates a board column
        // for each one that doesn't already exist (by ST hold reason ID).
        // Stores ServiceTitanHoldReasonId on each column for reliable matching.
        // Runs on every sync — safe to call repeatedly, never deletes columns.
        // ═══════════════════════════════════════════════════════════════
        public async Task SyncHoldReasonsToColumnsAsync()
        {
            try
            {
                var raw = await _service.GetHoldReasonsAsync();
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                if (!parsed.TryGetProperty("data", out var data)) return;

                var existingColumns = await _context.BoardColumns.ToListAsync();

                // Match by ST hold reason ID (most reliable), fall back to name
                var existingByStId = existingColumns
                    .Where(c => c.ServiceTitanHoldReasonId.HasValue)
                    .ToDictionary(c => c.ServiceTitanHoldReasonId!.Value);

                var existingNames = existingColumns
                    .Select(c => c.Name.ToLower())
                    .ToHashSet();

                var maxSort = existingColumns.Any()
                    ? existingColumns.Max(c => c.SortOrder)
                    : -1;

                int added = 0;

                foreach (var reason in data.EnumerateArray())
                {
                    if (!reason.TryGetProperty("id", out var idProp)) continue;
                    var stId = idProp.GetInt64();

                    var name = reason.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? ""
                        : "";
                    var active = !reason.TryGetProperty("active", out var activeProp)
                        || activeProp.GetBoolean();

                    if (string.IsNullOrWhiteSpace(name) || !active) continue;

                    // If a column already exists for this ST hold reason ID, skip
                    if (existingByStId.ContainsKey(stId)) continue;

                    // If a column exists with the same name but no ST ID, stamp it
                    var matchByName = existingColumns.FirstOrDefault(c =>
                        c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                        !c.ServiceTitanHoldReasonId.HasValue);
                    if (matchByName != null)
                    {
                        matchByName.ServiceTitanHoldReasonId = stId;
                        continue;
                    }

                    _context.BoardColumns.Add(new BoardColumn
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Color = "#334155",
                        SortOrder = ++maxSort,
                        IsDefault = false,
                        ColumnRole = null,
                        ServiceTitanHoldReasonId = stId
                    });

                    existingNames.Add(name.ToLower());
                    added++;
                }

                if (added > 0 || _context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[HoldReasonSync] Added {added} new board column(s) from ST hold reasons.");
                }
                else
                {
                    Console.WriteLine("[HoldReasonSync] No new hold reasons to add.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HoldReasonSync] Failed (non-fatal): {ex.Message}");
            }
        }

        private async Task AutoBoardFromAppointmentsAsync(Dictionary<long, List<ApptInfo>> jobAppointments, HashSet<long> jobsWithTechAssigned)
        {
            var needReturnCol = await _context.BoardColumns
                .FirstOrDefaultAsync(c => c.ColumnRole == "NeedToReturn");

            var waitScheduleCol = await _context.BoardColumns
                .FirstOrDefaultAsync(c => c.ColumnRole == "WaitingToSchedule");
            if (needReturnCol == null && waitScheduleCol == null)
            {
                Console.WriteLine("[AutoBoard] No relevant board columns found.");
                return;
            }

            var existingJobNumbers = await _context.BoardCards
                .Select(c => c.JobNumber).ToListAsync();

            int added = 0;

            // ─── PART 1: Appointment-based auto-board (Need to Return) ───
            foreach (var (stJobId, appointments) in jobAppointments)
            {
                var wo = await _context.WorkOrders
                    .Include(w => w.Customer)
                    .FirstOrDefaultAsync(w => w.ServiceTitanJobId == stJobId);
                if (wo == null) continue;

                var woStatus = wo.Status?.ToLower() ?? "";
                if (woStatus.Contains("completed") || woStatus.Contains("cancel")) continue;
                if (existingJobNumbers.Contains(wo.JobNumber)) continue;

                var custName = wo.Customer?.Name ?? "";
                if (custName.StartsWith("[DEMO]")) continue;

                bool hasHold = appointments.Any(a =>
                    a.Active && a.Status.Equals("Hold", StringComparison.OrdinalIgnoreCase));
                var activeNonUnused = appointments.Where(a => a.Active && !a.Unused).ToList();
                bool multiVisits = activeNonUnused.Count > 1;

                BoardColumn? target = null;
                string? note = null;

                if (hasHold && needReturnCol != null)
                {
                    target = needReturnCol;
                    note = "Auto-added: Appointment on Hold in ServiceTitan";
                }
                else if (multiVisits && needReturnCol != null)
                {
                    target = needReturnCol;
                    note = $"Auto-added: {activeNonUnused.Count} appointments — return visit requested";
                }

                if (target == null) continue;

                await AddCardToColumn(target, wo, custName, note, existingJobNumbers);
                added++;
            }

            // ─── PART 2: Need to Schedule — jobs with no active tech assignment ───
            if (waitScheduleCol != null)
            {
                // Find active work orders not on the board
                var openJobs = await _context.WorkOrders
                    .Include(w => w.Customer)
                    .Where(w => w.Status != null
                        && !w.Status.ToLower().Contains("completed")
                        && !w.Status.ToLower().Contains("cancel")
                        && w.ServiceTitanJobId > 0)
                    .ToListAsync();

                foreach (var wo in openJobs)
                {
                    if (existingJobNumbers.Contains(wo.JobNumber)) continue;

                    var custName = wo.Customer?.Name ?? "";
                    if (custName.StartsWith("[DEMO]")) continue;

                    // Skip jobs that have an active tech assignment
                    if (jobsWithTechAssigned.Contains(wo.ServiceTitanJobId))
                        continue;

                    // This job has no active tech assignment → needs scheduling
                    string reason;
                    if (!jobAppointments.ContainsKey(wo.ServiceTitanJobId))
                        reason = "Auto-added: No appointment created yet";
                    else
                        reason = "Auto-added: Appointment exists but no technician assigned";

                    await AddCardToColumn(waitScheduleCol, wo, custName, reason, existingJobNumbers);
                    added++;
                }
            }

            if (added > 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"[AutoBoard] Added {added} cards from appointment sync.");
            }

            // ─── REVERSE SYNC: ST hold reason → board column ──────────
            // For any appointment that ST has on hold with a mapped hold reason,
            // move the board card to the matching column (if not already there).
            await SyncHoldReasonsToBoard(jobAppointments);
        }

        // ═══════════════════════════════════════════════════════════════
        // SYNC HOLD REASONS → BOARD CARDS
        // - Jobs with HoldReasonId set → add/move card to matching column
        // - Jobs in a hold reason column whose HoldReasonId was cleared → remove card
        // Reads directly from WorkOrders.HoldReasonId (populated during job sync).
        // ═══════════════════════════════════════════════════════════════
        private async Task SyncHoldReasonsToBoard(Dictionary<long, List<ApptInfo>> jobAppointments)
        {
            // Build a live ID → name map from ST
            Dictionary<long, string> holdReasonMap = new();
            try
            {
                var raw = await _service.GetHoldReasonsAsync();
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
                if (parsed.TryGetProperty("data", out var data))
                {
                    foreach (var reason in data.EnumerateArray())
                    {
                        if (reason.TryGetProperty("id", out var idProp) &&
                            reason.TryGetProperty("name", out var nameProp))
                        {
                            holdReasonMap[idProp.GetInt64()] = nameProp.GetString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HoldSync] Could not fetch hold reasons: {ex.Message}");
                return;
            }

            if (!holdReasonMap.Any()) return;

            var boardColumns = await _context.BoardColumns.ToListAsync();

            // Hold reason column IDs (only columns tied to a ST hold reason)
            var holdReasonColumnIds = boardColumns
                .Where(c => c.ServiceTitanHoldReasonId.HasValue)
                .Select(c => c.Id)
                .ToHashSet();

            // ─── ADD / MOVE: jobs with HoldReasonId set → correct column ───
            var jobsOnHold = await _context.WorkOrders
                .Include(w => w.Customer)
                .Where(w => w.HoldReasonId != null && holdReasonMap.Keys.Contains(w.HoldReasonId.Value))
                .ToListAsync();

            Console.WriteLine($"[HoldSync] Found {jobsOnHold.Count} jobs with active hold reasons in DB.");

            foreach (var wo in jobsOnHold)
            {
                var holdReasonId = wo.HoldReasonId!.Value;

                var targetColumn = boardColumns.FirstOrDefault(c => c.ServiceTitanHoldReasonId == holdReasonId)
                    ?? boardColumns.FirstOrDefault(c =>
                        c.Name.Equals(holdReasonMap[holdReasonId], StringComparison.OrdinalIgnoreCase));

                if (targetColumn == null) continue;

                var woStatus = wo.Status?.ToLower() ?? "";
                if (woStatus.Contains("completed") || woStatus.Contains("cancel")) continue;

                var custName = wo.Customer?.Name ?? "";
                if (custName.StartsWith("[DEMO]")) continue;

                var card = await _context.BoardCards
                    .FirstOrDefaultAsync(c => c.JobNumber == wo.JobNumber);

                if (card == null)
                {
                    var maxSort = await _context.BoardCards
                        .Where(c => c.BoardColumnId == targetColumn.Id)
                        .Select(c => (int?)c.SortOrder)
                        .MaxAsync() ?? 0;

                    var newCard = new BoardCard
                    {
                        Id = Guid.NewGuid(),
                        BoardColumnId = targetColumn.Id,
                        WorkOrderId = wo.Id,
                        JobNumber = wo.JobNumber,
                        CustomerName = custName,
                        SortOrder = maxSort + 1
                    };
                    _context.BoardCards.Add(newCard);
                    _context.BoardCardNotes.Add(new BoardCardNote
                    {
                        Id = Guid.NewGuid(),
                        BoardCardId = newCard.Id,
                        Text = $"Auto-added: Hold reason '{holdReasonMap[holdReasonId]}'",
                        Author = "System"
                    });
                    Console.WriteLine($"[HoldSync] Added card {wo.JobNumber} to '{targetColumn.Name}'.");
                }
                else if (card.BoardColumnId != targetColumn.Id)
                {
                    card.BoardColumnId = targetColumn.Id;
                    Console.WriteLine($"[HoldSync] Moved card {wo.JobNumber} to '{targetColumn.Name}'.");
                }
            }

            // ─── REMOVE: jobs in hold reason columns whose hold was cleared ───
            if (holdReasonColumnIds.Any())
            {
                var onHoldJobNumbers = jobsOnHold.Select(w => w.JobNumber).ToHashSet();

                var cardsInHoldColumns = await _context.BoardCards
                    .Where(c => holdReasonColumnIds.Contains(c.BoardColumnId))
                    .ToListAsync();

                int removed = 0;
                foreach (var card in cardsInHoldColumns)
                {
                    if (!onHoldJobNumbers.Contains(card.JobNumber))
                    {
                        _context.BoardCards.Remove(card);
                        removed++;
                        Console.WriteLine($"[HoldSync] Removed card {card.JobNumber} — hold reason cleared in ST.");
                    }
                }

                if (removed > 0)
                    Console.WriteLine($"[HoldSync] Removed {removed} card(s) with cleared hold reasons.");
            }

            await _context.SaveChangesAsync();
        }

        private async Task AddCardToColumn(BoardColumn target, WorkOrder wo, string custName,
            string? note, List<string> existingJobNumbers)
        {
            var maxSort = await _context.BoardCards
                .Where(c => c.BoardColumnId == target.Id)
                .Select(c => (int?)c.SortOrder)
                .MaxAsync() ?? 0;

            var card = new BoardCard
            {
                Id = Guid.NewGuid(),
                BoardColumnId = target.Id,
                WorkOrderId = wo.Id,
                JobNumber = wo.JobNumber,
                CustomerName = custName,
                SortOrder = maxSort + 1
            };
            _context.BoardCards.Add(card);

            if (note != null)
            {
                _context.BoardCardNotes.Add(new BoardCardNote
                {
                    Id = Guid.NewGuid(),
                    BoardCardId = card.Id,
                    Text = note,
                    Author = "System"
                });
            }

            existingJobNumbers.Add(wo.JobNumber);
        }

        private class ApptInfo
        {
            public string Status { get; set; } = "";
            public bool Active { get; set; }
            public bool Unused { get; set; }
            public bool HasTechnician { get; set; }
            public long? HoldReasonId { get; set; }
        }

        // ═══════════════════════════════════════════════════════════════
        // ESTIMATES (list endpoint, status=Open)
        // ═══════════════════════════════════════════════════════════════

        public async Task SyncEstimatesAsync()
        {
            try
            {
                // Pull last sync time to do incremental updates
                var syncState = await _context.ServiceTitanSyncStates
                    .FirstOrDefaultAsync(s => s.EntityName == "Estimates");

                DateTime? modifiedSince = syncState?.LastSynced;

                int page = 1;
                bool hasMore = true;

                while (hasMore)
                {
                    var raw = await _service.GetOpenEstimatesAsync(page: page, pageSize: 200, modifiedOnOrAfter: modifiedSince);
                    var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

                    if (!parsed.TryGetProperty("data", out var data)) break;

                    hasMore = parsed.TryGetProperty("hasMore", out var hm) && hm.GetBoolean();
                    page++;

                    // Build a customer lookup: ST customer ID -> our Guid
                    var stCustomerIds = new List<long>();
                    foreach (var est in data.EnumerateArray())
                    {
                        if (est.TryGetProperty("customerId", out var cid) && cid.ValueKind == JsonValueKind.Number)
                            stCustomerIds.Add(cid.GetInt64());
                    }
                    var customerMap = await _context.Customers
                        .Where(c => stCustomerIds.Contains(c.ServiceTitanCustomerId))
                        .ToDictionaryAsync(c => c.ServiceTitanCustomerId, c => c.Id);

                    foreach (var est in data.EnumerateArray())
                    {
                        try
                        {
                            var stEstId  = est.GetProperty("id").GetInt64();
                            var jobId    = est.TryGetProperty("jobId", out var ji) && ji.ValueKind == JsonValueKind.Number ? ji.GetInt64() : 0L;
                            var custId   = est.TryGetProperty("customerId", out var ci) && ci.ValueKind == JsonValueKind.Number ? ci.GetInt64() : 0L;
                            var jobNum   = est.TryGetProperty("jobNumber", out var jn) ? jn.GetString() ?? "" : "";
                            var name     = est.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                            var summary  = est.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "";
                            var buName   = est.TryGetProperty("businessUnitName", out var bu) ? bu.GetString() ?? "" : "";
                            var subtotal = est.TryGetProperty("subtotal", out var sub) && sub.ValueKind == JsonValueKind.Number ? sub.GetDecimal() : 0m;
                            var tax      = est.TryGetProperty("tax", out var tx) && tx.ValueKind == JsonValueKind.Number ? tx.GetDecimal() : 0m;
                            var active   = !est.TryGetProperty("active", out var ac) || ac.GetBoolean();

                            var statusName = "";
                            if (est.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.Object)
                                statusProp.TryGetProperty("name", out var sn); // handled below
                            if (est.TryGetProperty("status", out var sp) && sp.ValueKind == JsonValueKind.Object && sp.TryGetProperty("name", out var snv))
                                statusName = snv.GetString() ?? "";

                            var reviewStatus = est.TryGetProperty("reviewStatus", out var rs) ? rs.GetString() ?? "" : "";

                            DateTime? createdOn  = null;
                            DateTime? modifiedOn = null;
                            DateTime? soldOn     = null;
                            if (est.TryGetProperty("createdOn", out var co) && co.ValueKind == JsonValueKind.String)
                                createdOn = co.GetDateTime();
                            if (est.TryGetProperty("modifiedOn", out var mo) && mo.ValueKind == JsonValueKind.String)
                                modifiedOn = mo.GetDateTime();
                            if (est.TryGetProperty("soldOn", out var so) && so.ValueKind == JsonValueKind.String && so.GetString() != null)
                                soldOn = so.GetDateTime();

                            Guid? localCustomerId = customerMap.TryGetValue(custId, out var lcid) ? lcid : null;

                            var existing = await _context.Estimates
                                .FirstOrDefaultAsync(e => e.ServiceTitanEstimateId == stEstId);

                            if (existing == null)
                            {
                                _context.Estimates.Add(new Estimate
                                {
                                    Id = Guid.NewGuid(),
                                    ServiceTitanEstimateId = stEstId,
                                    ServiceTitanJobId = jobId,
                                    ServiceTitanCustomerId = custId,
                                    JobNumber = jobNum,
                                    EstimateName = name,
                                    Status = statusName,
                                    ReviewStatus = reviewStatus,
                                    Summary = summary,
                                    BusinessUnitName = buName,
                                    Subtotal = subtotal,
                                    Tax = tax,
                                    CreatedOn = createdOn,
                                    ModifiedOn = modifiedOn,
                                    SoldOn = soldOn,
                                    IsActive = active,
                                    CustomerId = localCustomerId,
                                    LastSyncedFromServiceTitan = DateTime.UtcNow
                                });
                            }
                            else
                            {
                                existing.ServiceTitanJobId = jobId;
                                existing.ServiceTitanCustomerId = custId;
                                existing.JobNumber = jobNum;
                                existing.EstimateName = name;
                                existing.Status = statusName;
                                existing.ReviewStatus = reviewStatus;
                                existing.Summary = summary;
                                existing.BusinessUnitName = buName;
                                existing.Subtotal = subtotal;
                                existing.Tax = tax;
                                existing.CreatedOn = createdOn;
                                existing.ModifiedOn = modifiedOn;
                                existing.SoldOn = soldOn;
                                existing.IsActive = active;
                                existing.CustomerId = localCustomerId;
                                existing.LastSyncedFromServiceTitan = DateTime.UtcNow;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[EstimatesSync] Skipping estimate: {ex.Message}");
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // Update sync state
                if (syncState == null)
                {
                    syncState = new ServiceTitanSyncState { EntityName = "Estimates" };
                    _context.ServiceTitanSyncStates.Add(syncState);
                }
                syncState.LastSynced = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Console.WriteLine("[EstimatesSync] Complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EstimatesSync] Error: {ex.Message}");
                throw;
            }
        }
    }
}