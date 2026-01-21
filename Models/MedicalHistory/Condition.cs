namespace PatinetMo.Models
{
    public class Condition
    {
        public int Id { get; set; }
        public string Diagnosis { get; set; } // e.g., "Type 2 Diabetes"
        public string Notes { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }
    }
}
