namespace PatriotMechanical.API.Domain.Entities
{
    public class AppointmentTechnician
    {
        public Guid Id { get; set; }

        public Guid AppointmentId { get; set; }
        public Appointment Appointment { get; set; } = null!;

        public long ServiceTitanTechnicianId { get; set; }
        public string TechnicianName { get; set; } = "";

        public long ServiceTitanJobId { get; set; }
        public long ServiceTitanAppointmentId { get; set; }
    }
}