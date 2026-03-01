using BCrypt.Net;
using PatriotMechanical.API.Domain.Entities;

namespace PatriotMechanical.API.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Seed User
            if (!context.Users.Any())
            {
                var user = new User
                {
                    Email = "brandon@patriotmechanicaltx.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!")
                };

                context.Users.Add(user);
                context.SaveChanges();
            }

            // Seed WorkOrder + Customer + Technician
            if (!context.WorkOrders.Any())
            {
                var customer = new Customer
{
    Name = "DFW Commercial Property",
    Email = "manager@dfwproperty.com",
    Phone = "940-555-1212",

    // NEW required ServiceTitan sync fields
    ServiceTitanCustomerId = 0, 
    LastSyncedFromServiceTitan = DateTime.UtcNow
};

                var tech = new Technician
                {
                    FirstName = "Mike",
                    LastName = "Rodriguez",
                    HourlyCost = 35m
                };

                var workOrder = new WorkOrder
                {
                    JobNumber = "PM-1001",
                    Status = "Completed",
                    Customer = customer,
                    Technician = tech,
                    Subtotal = 5000m,
                    Tax = 0m,
                    TotalAmount = 5000m
                };

                workOrder.LaborEntries.Add(new WorkOrderLabor
                {
                    HoursWorked = 20m,
                    HourlyCostSnapshot = 35m,
                    BilledHours = 20m,
                    BilledRate = 125m
                });

                workOrder.MaterialEntries.Add(new WorkOrderMaterial
                {
                    PartName = "Mitsubishi Mini Split",
                    Quantity = 1,
                    UnitCostSnapshot = 2500m,
                    OriginalCalculatedPrice = 4000m,
                    FinalUnitPrice = 4000m,
                    WasPriceOverridden = false
                });

                context.WorkOrders.Add(workOrder);
                context.SaveChanges();
            }

            // Seed Invoice + Payment
            if (!context.Invoices.Any())
            {
                var workOrder = context.WorkOrders.First();

                var invoice = new Invoice
                {
                    WorkOrderId = workOrder.Id,
                    InvoiceNumber = "INV-1001",
                    IssueDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    Subtotal = workOrder.Subtotal,
                    Tax = workOrder.Tax,
                    TotalAmount = workOrder.TotalAmount,
                    BalanceRemaining = workOrder.TotalAmount,
                    Status = "Open"
                };

                var payment = new Payment
                {
                    Invoice = invoice,
                    Amount = 2000m,
                    Method = "CreditCard",
                    CreditCardFeeAmount = 2000m * 0.025m
                };

                invoice.BalanceRemaining -= payment.Amount;
                invoice.Status = "Partial";

                context.Invoices.Add(invoice);
                context.Payments.Add(payment);
                context.SaveChanges();
            }

            // Seed Vendors + Bills
            if (!context.Vendors.Any())
            {
                var workOrder = context.WorkOrders.First();

                var vendor = new Vendor
                {
                    Name = "United Refrigeration",
                    Email = "ap@unitedref.com",
                    Phone = "800-555-1111"
                };

                var jobBill = new VendorBill
                {
                    Vendor = vendor,
                    WorkOrderId = workOrder.Id,
                    BillNumber = "UR-5001",
                    IssueDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    TotalAmount = 1500m,
                    BalanceRemaining = 1500m,
                    Status = "Open"
                };

                var overheadBill = new VendorBill
                {
                    Vendor = vendor,
                    BillNumber = "UR-5002",
                    IssueDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(15),
                    TotalAmount = 800m,
                    BalanceRemaining = 800m,
                    Status = "Open"
                };

                context.Vendors.Add(vendor);
                context.VendorBills.Add(jobBill);
                context.VendorBills.Add(overheadBill);
                context.SaveChanges();
            }

            // Seed Parts (independent block)
            if (!context.Parts.Any())
            {
                var miniSplit = new Part
                {
                    Name = "Mitsubishi Mini Split",
                    UnitCost = 2500m
                };

                context.Parts.Add(miniSplit);
                context.SaveChanges();
            }
        }
    }
}