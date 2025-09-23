using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace agapay_backend.Hubs
{
    [Authorize]
    public class LocationHub : Hub
    {
        private string GetGroupName(int sessionId) => $"session-tracking-{sessionId}";

        /// <summary>
        /// Called by the therapist's app when they start their journey.
        /// </summary>
        public async Task StartTracking(int sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(sessionId));
        }

        /// <summary>
        /// Called by the patient's app to listen for the therapist's location updates.
        /// </summary>
        public async Task SubscribeToLocation(int sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(sessionId));
        }

        /// <summary>
        /// Called by the therapist's app to send its current coordinates.
        /// The location is then broadcast to the patient.
        /// </summary>
        public async Task UpdateLocation(int sessionId, double latitude, double longitude)
        {
            var groupName = GetGroupName(sessionId);
            // Broadcast the location to others in the group (the patient).
            await Clients.OthersInGroup(groupName).SendAsync("ReceiveLocationUpdate", new { latitude, longitude });
        }

        /// <summary>
        /// Called by either app to stop listening for updates.
        /// </summary>
        public async Task StopTracking(int sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(sessionId));
        }
    }
}