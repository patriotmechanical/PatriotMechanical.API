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
                        existing.ServiceTitanLocationId = locationId;
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
                            JobTypeName = jobTypeName,
                            CompletedAt = completedAt,
                            CreatedAt = createdAt ?? DateTime.UtcNow
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

                    if (!jobAppointments.ContainsKey(jobId))
                        jobAppointments[jobId] = new List<ApptInfo>();

                    jobAppointments[jobId].Add(new ApptInfo { Status = status, Active = active, Unused = unused, HasTechnician = false });
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

            await AutoBoardFromAppointmentsAsync(jobAppointments, jobsWithTechAssigned);
        }

        private async Task AutoBoardFromAppointmentsAsync(Dictionary<long, List<ApptInfo>> jobAppointments, HashSet<long> jobsWithTechAssigned)
        {
            var needReturnCol = await _context.BoardColumns
                .FirstOrDefaultAsync(c => c.Name.ToLower().Contains("need to return")
                                       || c.Name.ToLower().Contains("return"));

            var waitScheduleCol = await _context.BoardColumns
                .FirstOrDefaultAsync(c => c.Name.ToLower().Contains("schedule"));

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
        }

        // ═══════════════════════════════════════════════════════════════
        // APPOINTMENTS SYNC — today + next 3 days
        // ═══════════════════════════════════════════════════════════════
        public async Task SyncAppointmentsAsync()
        {
            var todayUtc = DateTime.UtcNow.Date;
            var windowEnd = todayUtc.AddDays(4);

            // ── Step 1: Fetch appointments from ST ─────────────────
            string raw;
            try { raw = await _service.GetAppointmentsAsync(todayUtc, windowEnd); }
            catch { return; }

            JsonElement parsed;
            try { parsed = JsonSerializer.Deserialize<JsonElement>(raw); }
            catch { return; }

            if (!parsed.TryGetProperty("data", out var appts)) return;

            // ── Step 2: Collect ST appt IDs from response ──────────
            var incomingApptIds = new List<long>();
            foreach (var appt in appts.EnumerateArray())
            {
                if (appt.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                    incomingApptIds.Add(idProp.GetInt64());
            }

            // Delete existing rows for these specific ST appt IDs (avoids duplicates)
            if (incomingApptIds.Any())
            {
                var staleAppts = await _context.Appointments
                    .Where(a => incomingApptIds.Contains(a.ServiceTitanAppointmentId))
                    .ToListAsync();
                _context.Appointments.RemoveRange(staleAppts);
                await _context.SaveChangesAsync();
            }

            // ── Step 3: Insert fresh appointment rows ──────────────
            var newAppts = new List<PatriotMechanical.API.Domain.Entities.Appointment>();
            foreach (var appt in appts.EnumerateArray())
            {
                try
                {
                    var apptId = appt.GetProperty("id").GetInt64();

                    long jobId = 0;
                    if (appt.TryGetProperty("jobId", out var jobProp) && jobProp.ValueKind == JsonValueKind.Number)
                        jobId = jobProp.GetInt64();

                    long locationId = 0;
                    if (appt.TryGetProperty("locationId", out var locProp) && locProp.ValueKind == JsonValueKind.Number)
                        locationId = locProp.GetInt64();

                    DateTime start = DateTime.MinValue, end = DateTime.MinValue;
                    if (appt.TryGetProperty("start", out var startProp) && startProp.ValueKind == JsonValueKind.String)
                        DateTime.TryParse(startProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out start);
                    if (appt.TryGetProperty("end", out var endProp) && endProp.ValueKind == JsonValueKind.String)
                        DateTime.TryParse(endProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal, out end);

                    if (start == DateTime.MinValue) continue;

                    string status = "Scheduled";
                    if (appt.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
                        status = statusProp.GetString() ?? "Scheduled";

                    Guid? workOrderId = null;
                    if (jobId > 0)
                    {
                        var wo = await _context.WorkOrders
                            .Where(w => w.ServiceTitanJobId == jobId)
                            .Select(w => new { w.Id })
                            .FirstOrDefaultAsync();
                        workOrderId = wo?.Id;
                    }

                    newAppts.Add(new PatriotMechanical.API.Domain.Entities.Appointment
                    {
                        Id = Guid.NewGuid(),
                        ServiceTitanAppointmentId = apptId,
                        ServiceTitanJobId = jobId,
                        ServiceTitanLocationId = locationId,
                        WorkOrderId = workOrderId,
                        Start = start,
                        End = end,
                        Status = status,
                        LastSyncedAt = DateTime.UtcNow
                    });
                }
                catch { /* skip malformed */ }
            }

            _context.Appointments.AddRange(newAppts);
            await _context.SaveChangesAsync();

            // ── Step 4: Fetch tech assignments via list endpoint ────
            // Uses appointmentIds filter — targeted, no pagination issues
            if (!newAppts.Any()) return;

            var apptLookup = newAppts.ToDictionary(a => a.ServiceTitanAppointmentId, a => a.Id);
            var stApptIds = newAppts.Select(a => a.ServiceTitanAppointmentId);

            string assignRaw;
            try { assignRaw = await _service.GetAppointmentAssignmentsAsync(stApptIds); }
            catch { return; }

            JsonElement assignParsed;
            try { assignParsed = JsonSerializer.Deserialize<JsonElement>(assignRaw); }
            catch { return; }

            if (!assignParsed.TryGetProperty("data", out var assignments)) return;

            // Also update TechnicianCount on the appointment rows
            var techCountByAppt = new Dictionary<long, int>();

            foreach (var assign in assignments.EnumerateArray())
            {
                try
                {
                    long stApptId = 0, stTechId = 0, stJobId = 0;
                    if (assign.TryGetProperty("appointmentId", out var aIdProp) && aIdProp.ValueKind == JsonValueKind.Number)
                        stApptId = aIdProp.GetInt64();
                    if (assign.TryGetProperty("technicianId", out var tIdProp) && tIdProp.ValueKind == JsonValueKind.Number)
                        stTechId = tIdProp.GetInt64();
                    if (assign.TryGetProperty("jobId", out var jIdProp) && jIdProp.ValueKind == JsonValueKind.Number)
                        stJobId = jIdProp.GetInt64();

                    string techName = "";
                    if (assign.TryGetProperty("technicianName", out var tNameProp) && tNameProp.ValueKind == JsonValueKind.String)
                        techName = tNameProp.GetString() ?? "";

                    bool active = true;
                    if (assign.TryGetProperty("active", out var activeProp) && activeProp.ValueKind == JsonValueKind.False)
                        active = false;

                    if (!active || stApptId == 0 || !apptLookup.ContainsKey(stApptId)) continue;

                    techCountByAppt[stApptId] = techCountByAppt.GetValueOrDefault(stApptId, 0) + 1;

                    _context.AppointmentTechnicians.Add(new PatriotMechanical.API.Domain.Entities.AppointmentTechnician
                    {
                        Id = Guid.NewGuid(),
                        AppointmentId = apptLookup[stApptId],
                        ServiceTitanTechnicianId = stTechId,
                        TechnicianName = techName,
                        ServiceTitanJobId = stJobId,
                        ServiceTitanAppointmentId = stApptId
                    });
                }
                catch { /* skip malformed */ }
            }

            // Patch TechnicianCount back onto the appointment rows
            foreach (var appt in newAppts.Where(a => techCountByAppt.ContainsKey(a.ServiceTitanAppointmentId)))
            {
                var tracked = await _context.Appointments.FindAsync(appt.Id);
                if (tracked != null) tracked.TechnicianCount = techCountByAppt[appt.ServiceTitanAppointmentId];
            }

            await _context.SaveChangesAsync();
        }
    }
}