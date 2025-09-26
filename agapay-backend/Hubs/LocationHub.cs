using agapay_backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace agapay_backend.Hubs
{
    [Authorize]
    public class LocationHub : Hub
    {
        private readonly agapayDbContext _db;

        public LocationHub(agapayDbContext db)
        {
            _db = db;
        }

        private string GetGroupName(int sessionId) => $"session-tracking-{sessionId}";

        /// <summary>
        /// Called by the therapist's app when they start their journey.
        /// Only the therapist assigned to the session may join this group as sender.
        /// </summary>
        public async Task StartTracking(int sessionId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return;

            var session = await _db.TherapySessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return;

            var therapist = await _db.PhysicalTherapists.AsNoTracking().FirstOrDefaultAsync(t => t.UserId.ToString() == userId);
            if (therapist is null || therapist.Id != session.PhysicalTherapistId) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(sessionId));
        }

        /// <summary>
        /// Called by the patient's app to listen for the therapist's location updates.
        /// Only the patient for the session may subscribe.
        /// </summary>
        public async Task SubscribeToLocation(int sessionId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return;

            var session = await _db.TherapySessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return;

            var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.UserId.ToString() == userId);
            if (patient is null || patient.Id != session.PatientId) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(sessionId));
        }

        /// <summary>
        /// Called by the therapist's app to send its current coordinates.
        /// The location is then broadcast to the patient in the session group.
        /// Only the session therapist may publish updates.
        /// </summary>
        public async Task UpdateLocation(int sessionId, double latitude, double longitude)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return;

            var session = await _db.TherapySessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return;

            var therapist = await _db.PhysicalTherapists.AsNoTracking().FirstOrDefaultAsync(t => t.UserId.ToString() == userId);
            if (therapist is null || therapist.Id != session.PhysicalTherapistId) return;

            var groupName = GetGroupName(sessionId);
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

