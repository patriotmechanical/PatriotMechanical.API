using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;

namespace PatriotMechanical.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<CompanySettings> CompanySettings { get; set; }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerContact> CustomerContacts { get; set; }
        public DbSet<CustomerLocation> CustomerLocations { get; set; }
        public DbSet<LocationContact> LocationContacts { get; set; }
        public DbSet<Technician> Technicians { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkOrderLabor> WorkOrderLabors { get; set; }
        public DbSet<WorkOrderMaterial> WorkOrderMaterials { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<ApBill> ApBills { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<VendorBill> VendorBills { get; set; }
        public DbSet<Part> Parts { get; set; }
        public DbSet<ServiceTitanSyncState> ServiceTitanSyncStates { get; set; }
        public DbSet<Subcontractor> Subcontractors { get; set; }
        public DbSet<SubcontractorEntry> SubcontractorEntries { get; set; }
        public DbSet<Equipment> Equipment { get; set; }

        // Work Order Board
        public DbSet<BoardColumn> BoardColumns { get; set; }
        public DbSet<BoardCard> BoardCards { get; set; }
        public DbSet<BoardCardNote> BoardCardNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(u => u.Company)
                .WithMany()
                .HasForeignKey(u => u.CompanySettingsId);

            modelBuilder.Entity<BoardCard>()
                .HasOne(c => c.Column)
                .WithMany(col => col.Cards)
                .HasForeignKey(c => c.BoardColumnId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BoardCard>()
                .HasOne(c => c.WorkOrder)
                .WithMany()
                .HasForeignKey(c => c.WorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BoardCardNote>()
                .HasOne(n => n.Card)
                .WithMany(c => c.Notes)
                .HasForeignKey(n => n.BoardCardId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}