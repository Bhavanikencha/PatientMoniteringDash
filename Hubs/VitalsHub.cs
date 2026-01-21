using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace PatinetMo.Hubs
{
    public class VitalsHub : Hub
    {
        // This method is optional but useful for debugging.
        // It runs automatically when a browser connects.
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
    }
}
