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

        private static readonly Random Rng = new Random(42);

        public static async Task ResetDemoDataAsync(AppDbContext db)
        {
            // Clean all demo-prefixed data with raw SQL
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

            // Clear EF change tracker to avoid stale references
            db.ChangeTracker.Clear();

            // Ensure demo company
            var hasCompany = await db.CompanySettings.AnyAsync(c => c.Id == DemoCompanyId);
            if (!hasCompany)
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ""CompanySettings"" (""Id"", ""CompanyName"", ""CreditCardFeePercent"", ""AutoSyncEnabled"", ""SyncIntervalMinutes"")
                    VALUES ({0}, 'Freedom Air Heating & Cooling', 2.5, false, 60)
                    ON CONFLICT (""Id"") DO NOTHING
                ", DemoCompanyId);
            }

            // Ensure demo user
            var hasUser = await db.Users.AnyAsync(u => u.Id == DemoUserId);
            if (!hasUser)
            {
                var hash = BCrypt.Net.BCrypt.HashPassword("demo1234");
                await db.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ""Users"" (""Id"", ""Email"", ""PasswordHash"", ""FullName"", ""IsActive"", ""CompanySettingsId"", ""CreatedAt"")
                    VALUES ({0}, {1}, {2}, 'Demo User', true, {3}, now())
                    ON CONFLICT (""Id"") DO NOTHING
                ", DemoUserId, DemoEmail, hash, DemoCompanyId);
            }

            db.ChangeTracker.Clear();

            // Now seed fresh demo data
            await SeedCustomersAndData(db);
        }

        private static async Task SeedCustomersAndData(AppDbContext db)
        {
            var now = DateTime.UtcNow;
            var customerNames = new[]
            {
                "Johnson Residence", "Smith Commercial", "Martinez Family", "Oakwood Apartments",
                "Chen Dental Office", "First Baptist Church", "Riverside Restaurant", "Thompson Estate",
                "Valley View Elementary", "Garcia Auto Body", "Lakeside Veterinary", "Premier Real Estate",
                "Williams Home", "CrossFit Iron Will", "Sunset Senior Living"
            };

            var phones = new[]
            {
                "817-555-0142", "972-555-0198", "469-555-0234", "214-555-0301",
                "817-555-0455", "940-555-0187", "682-555-0321", "817-555-0567",
                "940-555-0432", "469-555-0189", "817-555-0654", "972-555-0444",
                "214-555-0876", "682-555-0222", "940-555-0999"
            };

            var emails = new[]
            {
                "johnson.family@email.com", "accounting@smithcommercial.com", "carlos.martinez@email.com",
                "maintenance@oakwoodapts.com", "drchen@chendental.com", "facilities@firstbaptist.org",
                "mike@riversidedining.com", "sarah.thompson@email.com", "principal@valleyview.edu",
                "rgarcia@garciaautobody.com", "office@lakesidevet.com", "ops@premierrealestate.com",
                "dwilliams@email.com", "owner@ironwillcf.com", "facilities@sunsetliving.com"
            };

            var streetNames = new[] { "Oak Dr", "Maple Ave", "Main St", "Elm St", "Cedar Ln", "Pine Rd", "Hickory Blvd", "Pecan Way", "Walnut St", "Birch Ct" };
            var cities = new[] { "Denton", "Fort Worth", "Dallas", "Arlington", "Keller", "Southlake", "Flower Mound", "Lewisville", "Frisco", "McKinney" };
            var jobTypes = new[] { "AC Repair", "Heating Repair", "AC Install", "Furnace Install", "Maintenance", "Tune-Up", "Duct Work", "Thermostat Install", "Refrigerant Recharge", "Compressor Replacement" };
            var statuses = new[] { "Completed", "Completed", "Completed", "Completed", "Open", "Open", "Hold", "Completed" };
            var equipTypes = new[] { "AC Unit", "Furnace", "Heat Pump", "Mini Split", "RTU", "Thermostat" };
            var brands = new[] { "Carrier", "Trane", "Lennox", "Goodman", "Rheem", "Daikin", "York" };

            var customerIds = new List<Guid>();
            var allWorkOrders = new List<WorkOrder>();
            int jobNum = 80001;

            // ─── Customers with contacts and locations ────────────────
            for (int i = 0; i < customerNames.Length; i++)
            {
                var custId = Guid.NewGuid();
                customerIds.Add(custId);

                db.Customers.Add(new Customer
                {
                    Id = custId,
                    Name = "[DEMO] " + customerNames[i],
                    ServiceTitanCustomerId = 0,
                    LastSyncedFromServiceTitan = now
                });

                db.CustomerContacts.Add(new CustomerContact
                {
                    Id = Guid.NewGuid(), CustomerId = custId, Type = "Phone",
                    Value = phones[i], Active = true
                });

                db.CustomerContacts.Add(new CustomerContact
                {
                    Id = Guid.NewGuid(), CustomerId = custId, Type = "Email",
                    Value = emails[i], Active = true
                });

                var streetNum = 100 + (i * 137) % 9000;
                db.CustomerLocations.Add(new CustomerLocation
                {
                    Id = Guid.NewGuid(), CustomerId = custId,
                    Name = customerNames[i],
                    Street = $"{streetNum} {streetNames[i % streetNames.Length]}",
                    City = cities[i % cities.Length], State = "TX",
                    Zip = $"76{100 + (i * 17) % 200}", Active = true
                });
            }

            await db.SaveChangesAsync();

            // ─── Work Orders ──────────────────────────────────────────
            for (int ci = 0; ci < customerIds.Count; ci++)
            {
                int woCount = 2 + (ci % 4);
                for (int w = 0; w < woCount; w++)
                {
                    int daysAgo = 5 + ((ci * 7 + w * 53) % 395);
                    string status = statuses[(ci + w) % statuses.Length];
                    string jobType = jobTypes[(ci + w) % jobTypes.Length];
                    decimal amount = Math.Round(150m + (decimal)((ci * 371 + w * 197) % 4000), 2);
                    var created = now.AddDays(-daysAgo);

                    var wo = new WorkOrder
                    {
                        Id = Guid.NewGuid(),
                        JobNumber = (jobNum++).ToString(),
                        CustomerId = customerIds[ci],
                        Status = status,
                        JobTypeName = jobType,
                        TotalAmount = amount,
                        TotalRevenueCalculated = amount,
                        CreatedAt = created,
                        CompletedAt = status == "Completed" ? created.AddDays(1 + (w % 4)) : null
                    };
                    db.WorkOrders.Add(wo);
                    allWorkOrders.Add(wo);
                }
            }

            await db.SaveChangesAsync();

            // ─── Invoices ─────────────────────────────────────────────
            int invIdx = 0;
            foreach (var wo in allWorkOrders.Where(w => w.Status == "Completed"))
            {
                decimal balance = (invIdx++ % 4 == 0) ? wo.TotalAmount : 0;
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

            // ─── Equipment ────────────────────────────────────────────
            for (int ci = 0; ci < Math.Min(10, customerIds.Count); ci++)
            {
                int eqCount = 1 + (ci % 3);
                for (int e = 0; e < eqCount; e++)
                {
                    int installYearsAgo = 1 + ((ci + e * 3) % 11);
                    db.Equipment.Add(new Equipment
                    {
                        Id = Guid.NewGuid(),
                        CustomerId = customerIds[ci],
                        Type = equipTypes[(ci + e) % equipTypes.Length],
                        Brand = brands[(ci + e) % brands.Length],
                        ModelNumber = $"{(char)('A' + (ci % 26))}{100 + ci * 13 + e}{(char)('A' + (e % 26))}",
                        SerialNumber = $"SN-{100000 + ci * 1000 + e}",
                        InstallDate = now.AddYears(-installYearsAgo),
                        WarrantyExpiration = now.AddYears(-installYearsAgo + 5),
                        WarrantyRegistered = ci % 2 == 0
                    });
                }
            }

            await db.SaveChangesAsync();

            // ─── Vendors & AP Bills ───────────────────────────────────
            var vendorData = new (string Name, decimal Amount)[]
            {
                ("[DEMO] United Refrigeration", 3200m),
                ("[DEMO] Johnstone Supply", 1800m),
                ("[DEMO] Ferguson HVAC", 950m),
                ("[DEMO] R.E. Michel", 2400m),
                ("[DEMO] Comfort Products", 675m)
            };

            for (int vi = 0; vi < vendorData.Length; vi++)
            {
                var vendorId = Guid.NewGuid();
                db.Vendors.Add(new Vendor { Id = vendorId, Name = vendorData[vi].Name });
                db.ApBills.Add(new ApBill
                {
                    Id = Guid.NewGuid(),
                    VendorId = vendorId,
                    Amount = vendorData[vi].Amount * 0.6m,
                    TotalAmount = vendorData[vi].Amount,
                    DueDate = now.AddDays(-5 + vi * 6),
                    IsPaid = false
                });
            }

            await db.SaveChangesAsync();

            // ─── Subcontractors ───────────────────────────────────────
            var subData = new (string Name, string Company, string Trade)[]
            {
                ("[DEMO] Tony Ramirez", "Ramirez Plumbing", "Plumbing"),
                ("[DEMO] Jake Mitchell", "Mitchell Electric", "Electrical"),
                ("[DEMO] Dave Cooper", "Cooper Sheet Metal", "Sheet Metal")
            };

            var openWos = allWorkOrders.Where(w => w.Status == "Open").Take(4).ToList();

            for (int si = 0; si < subData.Length; si++)
            {
                var subId = Guid.NewGuid();
                db.Subcontractors.Add(new Subcontractor
                {
                    Id = subId, Name = subData[si].Name,
                    Company = subData[si].Company, Trade = subData[si].Trade
                });

                if (si < openWos.Count)
                {
                    db.SubcontractorEntries.Add(new SubcontractorEntry
                    {
                        Id = Guid.NewGuid(),
                        SubcontractorId = subId,
                        WorkOrderId = openWos[si].Id,
                        Hours = 3 + si * 2,
                        HourlyRate = 40 + si * 10,
                        Date = now.AddDays(-(1 + si * 5)),
                        Notes = "Demo entry"
                    });
                }
            }

            await db.SaveChangesAsync();

            // ─── Board Cards ──────────────────────────────────────────
            var boardCols = await db.BoardColumns.OrderBy(c => c.SortOrder).ToListAsync();
            if (boardCols.Count > 0)
            {
                var boardWos = allWorkOrders.Where(w => w.Status == "Open" || w.Status == "Hold").Take(6).ToList();
                var demoNotes = new[]
                {
                    "Customer called — prefers morning appointments",
                    "Waiting on 3-ton condenser from United Refrigeration",
                    "Quote sent via email, following up Friday",
                    "Need 410A refrigerant — check stock",
                    "Thermostat wiring issue — need to return with multimeter",
                    "Parts on backorder, ETA next Tuesday"
                };

                for (int bi = 0; bi < boardWos.Count && bi < boardCols.Count; bi++)
                {
                    var wo = boardWos[bi];
                    var custIdx = customerIds.IndexOf(wo.CustomerId);
                    var custName = custIdx >= 0 ? "[DEMO] " + customerNames[custIdx] : "Unknown";

                    var cardId = Guid.NewGuid();
                    db.BoardCards.Add(new BoardCard
                    {
                        Id = cardId,
                        BoardColumnId = boardCols[bi].Id,
                        WorkOrderId = wo.Id,
                        JobNumber = wo.JobNumber,
                        CustomerName = custName,
                        SortOrder = 0
                    });
                    db.BoardCardNotes.Add(new BoardCardNote
                    {
                        Id = Guid.NewGuid(),
                        BoardCardId = cardId,
                        Text = demoNotes[bi],
                        Author = "Demo User"
                    });
                }

                await db.SaveChangesAsync();
            }
        }
    }
}