namespace PatinetMo.Services
{
    public class AlertService
    {

        public string GetStatus(int heartRate, int oxygen, float temperature)
        {
            if (heartRate > 120 || oxygen < 90 || temperature > 38)
                return "Critical";

            if (heartRate > 100 || oxygen < 95)
                return "Warning";

            return "Normal";
        }
    }
}
