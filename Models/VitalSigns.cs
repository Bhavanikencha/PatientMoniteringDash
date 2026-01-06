namespace PatinetMo.Models
{
    public class VitalSigns
    {
        
            public int Id { get; set; }
            public int PatientId { get; set; }
            public int HeartRate { get; set; }
            public int Oxygen { get; set; }
            public float Temperature { get; set; }
            public DateTime UpdatedAt { get; set; }
        
    }
}
