namespace PatinetMo.Models
{
    public class Medication
    {
        public int Id { get; set; }
        public string DrugName { get; set; } // e.g., "Metformin"
        public string Dosage { get; set; }   // e.g., "500mg BID"

        // Foreign Key
        public int PatientId { get; set; }
        public Patient Patient { get; set; }
    }
}
