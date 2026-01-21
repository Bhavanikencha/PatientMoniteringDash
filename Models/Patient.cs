using System;
using System.Collections.Generic; // Required for List<>

namespace PatinetMo.Models
{
    public class Patient
    {
        public int PatientId { get; set; }
        public string Name { get; set; }
        public bool IsPregnant { get; set; }

        // Personal Details
        public DateTime DateOfBirth { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public DateTime AdmissionDate { get; set; }
        public string BloodType { get; set; }

        // --- IMPROVED RELATIONSHIPS (One-to-Many) ---
        public List<Medication> Medications { get; set; } = new List<Medication>();
        public List<Surgery> Surgeries { get; set; } = new List<Surgery>();
        public List<Condition> Conditions { get; set; } = new List<Condition>();

        public string FamilyHistory { get; set; } // Can stay as text for now
        public string SocialHistory { get; set; }

        public int? DoctorId { get; set; }
        public Doctor Doctor { get; set; }
    }
}
