using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public static class DemoSeeder
    {
        public static readonly Guid DemoCompanyId = Guid.Parse("demo0000-0000-0000-0000-000000000001");
        public static readonly Guid DemoUserId = Guid.Parse("demo0000-0000-0000-0000-000000000002");
        public const string DemoEmail = "demo@patriotmechanical.app";

        public static async Task ResetDemoDataAsync(AppDbContext db)
        {
            // Clean up all demo data using raw SQL (much simpler than LINQ for cascading deletes)
            await db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM ""BoardCardNotes"" WHERE ""BoardCardId"" IN (
                    SELECT bc.""Id"" FROM ""BoardCards"" bc
                    JOIN ""WorkOrders"" wo ON bc.""WorkOrderId"" = wo.""Id""
                    JOIN ""Customers"" c ON wo.""CustomerId"" = c.""Id""
                    WHERE c.""Name"" LIKE '[DEMO]%'
                );
                DELETE FROM ""BoardCards"" WHERE ""WorkOrderId"" IN (
                    SELECT wo.""Id"" FROM ""WorkOrders"" wo
                    JOIN ""Customers"" c ON wo.""CustomerId"" = c.""Id""
                    WHERE c.""Name"" LIKE '[DEMO]%'
                );
                DELETE FROM ""SubcontractorEntries"" WHERE ""WorkOrderId"" IN (
                    SELECT wo.""Id"" FROM ""WorkOrders"" wo
                    JOIN ""Customers"" c ON wo.""CustomerId"" = c.""Id""
                    WHERE c.""Name"" LIKE '[DEMO]%'
                );
                DELETE FROM ""WorkOrderMaterials"" WHERE ""WorkOrderId"" IN (
                    SELECT wo.""Id"" FROM ""WorkOrders"" wo
                    JOIN ""Customers"" c ON wo.""CustomerId"" = c.""Id""
                    WHERE c.""Name"" LIKE '[DEMO]%'
                );
                DELETE FROM ""WorkOrderLabors"" WHERE ""WorkOrderId"" IN (
                    SELECT wo.""Id"" FROM ""WorkOrders"" wo
                    JOIN ""Customers"" c ON wo.""CustomerId"" = c.""Id""
                    WHERE c.""Name"" LIKE '[DEMO]%'
                );
                DELETE FROM ""Invoices"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""WorkOrders"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""Equipment"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""LocationContacts"" WHERE ""LocationId"" IN (
                    SELECT ""Id"" FROM ""CustomerLocations"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%')
                );
                DELETE FROM ""CustomerLocations"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""CustomerContacts"" WHERE ""CustomerId"" IN (SELECT ""Id"" FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""Customers"" WHERE ""Name"" LIKE '[DEMO]%';
                DELETE FROM ""ApBills"" WHERE ""VendorId"" IN (SELECT ""Id"" FROM ""Vendors"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""Vendors"" WHERE ""Name"" LIKE '[DEMO]%';
                DELETE FROM ""SubcontractorEntries"" WHERE ""SubcontractorId"" IN (SELECT ""Id"" FROM ""Subcontractors"" WHERE ""Name"" LIKE '[DEMO]%');
                DELETE FROM ""Subcontractors"" WHERE ""Name"" LIKE '[DEMO]%';
            ");

            // Ensure demo company exists
            var demoCompany = await db.CompanySettings.FindAsync(DemoCompanyId);
            if (demoCompany == null)
            {
                demoCompany = new CompanySettings
                {
                    Id = DemoCompanyId,
                    CompanyName = "Freedom Air Heating & Cooling",
                    CreditCardFeePercent = 2.5m
                };
                db.CompanySettings.Add(demoCompany);
            }

            // Ensure demo user exists
            var demoUser = await db.Users.FindAsync(DemoUserId);
            if (demoUser == null)
            {
                db.Users.Add(new User
                {
                    Id = DemoUserId,
                    Email = DemoEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo1234"),
                    FullName = "Demo User",
                    CompanySettingsId = DemoCompanyId,
                    IsActive = true
                });
            }

            await db.SaveChangesAsync();

            // ─── SEED CUSTOMERS ───────────────────────────────────────
            var customers = new[]
            {
                ("Johnson Residence", "Phone", "817-555-0142", "johnson.family@email.com"),
                ("Smith Commercial", "Phone", "972-555-0198", "accounting@smithcommercial.com"),
                ("Martinez Family", "MobilePhone", "469-555-0234", "carlos.martinez@email.com"),
                ("Oakwood Apartments", "Phone", "214-555-0301", "maintenance@oakwoodapts.com"),
                ("Chen Dental Office", "Phone", "817-555-0455", "drchen@chendental.com"),
                ("First Baptist Church", "Phone", "940-555-0187", "facilities@firstbaptist.org"),
                ("Riverside Restaurant", "MobilePhone", "682-555-0321", "mike@riversidedining.com"),
                ("Thompson Estate", "Phone", "817-555-0567", "sarah.thompson@email.com"),
                ("Valley View Elementary", "Phone", "940-555-0432", "principal@valleyview.edu"),
                ("Garcia Auto Body", "MobilePhone", "469-555-0189", "rgarcia@garciaautobody.com"),
                ("Lakeside Veterinary", "Phone", "817-555-0654", "office@lakesidevet.com"),
                ("Premier Real Estate", "Phone", "972-555-0444", "ops@premierrealestate.com"),
                ("Williams Home", "MobilePhone", "214-555-0876", "dwilliams@email.com"),
                ("CrossFit Iron Will", "Phone", "682-555-0222", "owner@ironwillcf.com"),
                ("Sunset Senior Living", "Phone", "940-555-0999", "facilities@sunsetliving.com"),
            };

            var customerEntities = new List<Customer>();
            var now = DateTime.UtcNow;

            foreach (var (name, phoneType, phone, email) in customers)
            {
                var c = new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = "[DEMO] " + name,
                    ServiceTitanCustomerId = 0,
                    LastSyncedFromServiceTitan = now
                };
                db.Customers.Add(c);
                customerEntities.Add(c);

                db.CustomerContacts.Add(new CustomerContact { Id = Guid.NewGuid(), CustomerId = c.Id, Type = phoneType, Value = phone, Active = true });
                db.CustomerContacts.Add(new CustomerContact { Id = Guid.NewGuid(), CustomerId = c.Id, Type = "Email", Value = email, Active = true });

                db.CustomerLocations.Add(new CustomerLocation
                {
                    Id = Guid.NewGuid(), CustomerId = c.Id, Name = name,
                    Street = $"{Random.Shared.Next(100, 9999)} {StreetNames[Random.Shared.Next(StreetNames.Length)]}",
                    City = Cities[Random.Shared.Next(Cities.Length)], State = "TX",
                    Zip = $"76{Random.Shared.Next(100, 299)}", Active = true
                });
            }

            await db.SaveChangesAsync();

            // ─── SEED WORK ORDERS ─────────────────────────────────────
            var jobTypes = new[] { "AC Repair", "Heating Repair", "AC Install", "Furnace Install", "Maintenance", "Tune-Up", "Duct Work", "Thermostat Install", "Refrigerant Recharge", "Compressor Replacement" };
            var statuses = new[] { "Completed", "Completed", "Completed", "Completed", "Open", "Open", "Hold", "Completed" };
            int jobNum = 80001;

            var allWorkOrders = new List<WorkOrder>();

            foreach (var cust in customerEntities)
            {
                int woCount = Random.Shared.Next(2, 6);
                for (int i = 0; i < woCount; i++)
                {
                    var daysAgo = Random.Shared.Next(5, 400);
                    var status = statuses[Random.Shared.Next(statuses.Length)];
                    var jobType = jobTypes[Random.Shared.Next(jobTypes.Length)];
                    var amount = Math.Round((decimal)(Random.Shared.NextDouble() * 4000 + 150), 2);
                    var created = now.AddDays(-daysAgo);

                    var wo = new WorkOrder
                    {
                        Id = Guid.NewGuid(),
                        JobNumber = (jobNum++).ToString(),
                        CustomerId = cust.Id,
                        Status = status,
                        JobTypeName = jobType,
                        TotalAmount = amount,
                        TotalRevenueCalculated = amount,
                        CreatedAt = created,
                        CompletedAt = status == "Completed" ? created.AddDays(Random.Shared.Next(1, 5)) : null
                    };
                    db.WorkOrders.Add(wo);
                    allWorkOrders.Add(wo);
                }
            }

            await db.SaveChangesAsync();

            // ─── SEED INVOICES ────────────────────────────────────────
            foreach (var wo in allWorkOrders.Where(w => w.Status == "Completed"))
            {
                var balance = Random.Shared.Next(0, 4) == 0 ? wo.TotalAmount : 0; // ~25% unpaid
                db.Invoices.Add(new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = $"INV-{wo.JobNumber}",
                    CustomerId = wo.CustomerId,
                    WorkOrderId = wo.Id,
                    InvoiceDate = wo.CompletedAt ?? now,
                    DueDate = (wo.CompletedAt ?? now).AddDays(30),
                    TotalAmount = wo.TotalAmount,
                    BalanceRemaining = balance,
                    Status = balance > 0 ? "Open" : "Paid"
                });
            }

            await db.SaveChangesAsync();

            // ─── SEED EQUIPMENT ───────────────────────────────────────
            var equipTypes = new[] { "AC Unit", "Furnace", "Heat Pump", "Mini Split", "RTU", "Thermostat" };
            var brands = new[] { "Carrier", "Trane", "Lennox", "Goodman", "Rheem", "Daikin", "York" };

            foreach (var cust in customerEntities.Take(10))
            {
                int eqCount = Random.Shared.Next(1, 4);
                for (int i = 0; i < eqCount; i++)
                {
                    var installYearsAgo = Random.Shared.Next(1, 12);
                    db.Equipment.Add(new Equipment
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = cust.Id,
                        Type = equipTypes[Random.Shared.Next(equipTypes.Length)],
                        Brand = brands[Random.Shared.Next(brands.Length)],
                        ModelNumber = $"{(char)('A' + Random.Shared.Next(26))}{Random.Shared.Next(100, 999)}{(char)('A' + Random.Shared.Next(26))}",
                        SerialNumber = $"SN-{Random.Shared.Next(100000, 999999)}",
                        InstallDate = now.AddYears(-installYearsAgo),
                        WarrantyExpiration = now.AddYears(-installYearsAgo + 5),
                        WarrantyRegistered = Random.Shared.Next(0, 2) == 1
                    });
                }
            }

            await db.SaveChangesAsync();

            // ─── SEED VENDORS & AP BILLS ──────────────────────────────
            var vendorData = new[]
            {
                ("[DEMO] United Refrigeration", 3200m),
                ("[DEMO] Johnstone Supply", 1800m),
                ("[DEMO] Ferguson GNAC", 950m),
                ("[DEMO] R.E. Michel", 2400m),
                ("[DEMO] Comfort Products", 675m)
            };

            foreach (var (vName, totalOwed) in vendorData)
            {
                var vendor = new Vendor { Id = Guid.NewGuid(), Name = vName };
                db.Vendors.Add(vendor);
                db.ApBills.Add(new ApBill
                {
                    Id = Guid.NewGuid(),
                    VendorId = vendor.Id,
                    Amount = totalOwed * 0.6m,
                    TotalAmount = totalOwed,
                    DueDate = now.AddDays(Random.Shared.Next(-5, 25)),
                    IsPaid = false
                });
            }

            await db.SaveChangesAsync();

            // ─── SEED SUBCONTRACTORS ──────────────────────────────────
            var subs = new[]
            {
                ("[DEMO] Tony Ramirez", "Ramirez Plumbing", "Plumbing"),
                ("[DEMO] Jake Mitchell", "Mitchell Electric", "Electrical"),
                ("[DEMO] Dave Cooper", "Cooper Sheet Metal", "Sheet Metal")
            };

            foreach (var (sName, company, trade) in subs)
            {
                var sub = new Subcontractor { Id = Guid.NewGuid(), Name = sName, Company = company, Trade = trade };
                db.Subcontractors.Add(sub);

                // Add some entries
                var openWos = allWorkOrders.Where(w => w.Status == "Open").Take(2).ToList();
                foreach (var wo in openWos)
                {
                    db.SubcontractorEntries.Add(new SubcontractorEntry
                    {
                        Id = Guid.NewGuid(),
                        SubcontractorId = sub.Id,
                        WorkOrderId = wo.Id,
                        Hours = Random.Shared.Next(2, 10),
                        HourlyRate = Random.Shared.Next(35, 75),
                        Date = now.AddDays(-Random.Shared.Next(1, 30)),
                        Notes = "Demo entry"
                    });
                }
            }

            await db.SaveChangesAsync();

            // ─── SEED BOARD CARDS ─────────────────────────────────────
            var boardCols = await db.BoardColumns.OrderBy(c => c.SortOrder).ToListAsync();
            if (boardCols.Count > 0)
            {
                var openWos = allWorkOrders.Where(w => w.Status == "Open" || w.Status == "Hold").ToList();
                for (int i = 0; i < Math.Min(openWos.Count, boardCols.Count); i++)
                {
                    var wo = openWos[i];
                    var custName = customerEntities.FirstOrDefault(c => c.Id == wo.CustomerId)?.Name ?? "Unknown";
                    var card = new BoardCard
                    {
                        Id = Guid.NewGuid(),
                        BoardColumnId = boardCols[i % boardCols.Count].Id,
                        WorkOrderId = wo.Id,
                        JobNumber = wo.JobNumber,
                        CustomerName = custName,
                        SortOrder = 0
                    };
                    db.BoardCards.Add(card);
                    db.BoardCardNotes.Add(new BoardCardNote
                    {
                        Id = Guid.NewGuid(),
                        BoardCardId = card.Id,
                        Text = DemoNotes[Random.Shared.Next(DemoNotes.Length)],
                        Author = "Demo User"
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        private static readonly string[] StreetNames = { "Oak Dr", "Maple Ave", "Main St", "Elm St", "Cedar Ln", "Pine Rd", "Hickory Blvd", "Pecan Way", "Walnut St", "Birch Ct" };
        private static readonly string[] Cities = { "Denton", "Fort Worth", "Dallas", "Arlington", "Keller", "Southlake", "Flower Mound", "Lewisville", "Frisco", "McKinney" };
        private static readonly string[] DemoNotes = {
            "Customer called — prefers morning appointments",
            "Waiting on 3-ton condenser from United Refrigeration",
            "Quote sent via email, following up Friday",
            "Need 410A refrigerant — check stock",
            "Thermostat wiring issue — need to return with multimeter",
            "Parts on backorder, ETA next Tuesday",
            "Customer approved quote — ready to schedule",
            "Ductwork measurements needed before ordering"
        };
    }
}