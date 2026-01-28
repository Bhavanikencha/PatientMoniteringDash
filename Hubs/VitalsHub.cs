using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace PatinetMo.Hubs
{
    public class VitalsHub : Hub
    {
        public async Task JoinPatientMonitor(int patientId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Patient_{patientId}");
            }
            catch (Exception ex)
            {
                // This keeps the connection alive even if joining fails
                Console.WriteLine($"Error Joining Group: {ex.Message}");
            }
        }

        public async Task LeavePatientMonitor(int patientId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Patient_{patientId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Leaving Group: {ex.Message}");
            }
        }
    }
}



