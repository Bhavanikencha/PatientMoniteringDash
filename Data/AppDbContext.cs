using Microsoft.EntityFrameworkCore;
using PatinetMo.Models;
namespace PatinetMo.Data
{

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<VitalSigns> VitalSigns { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<AlertHistory> AlertHistory { get; set; }

        // NEW TABLES
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Surgery> Surgeries { get; set; }
        public DbSet<Condition> Conditions { get; set; }
    }

}