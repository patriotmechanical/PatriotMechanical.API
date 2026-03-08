namespace PatriotMechanical.API.Domain.Entities
{
    public class Appointment
    {
        public Guid Id { get; set; }
        public long ServiceTitanAppointmentId { get; set; }
        public long ServiceTitanJobId { get; set; }

        public Guid? WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "Scheduled";
        public int TechnicianCount { get; set; }
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AppointmentTechnician> Technicians { get; set; } = new List<AppointmentTechnician>();
    }
}