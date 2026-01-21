namespace PatinetMo.Models
{
    public class Surgery
    {
        public int Id { get; set; }
        public string ProcedureName { get; set; } // e.g., "Appendectomy"
        public string Year { get; set; }          // e.g., "2015"
        public string Complications { get; set; } // e.g., "None"

        public int PatientId { get; set; }
        public Patient Patient { get; set; }
    }
}

