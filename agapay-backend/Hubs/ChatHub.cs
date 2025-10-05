using agapay_backend.Data;
using agapay_backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace agapay_backend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly agapayDbContext _context;

        public ChatHub(agapayDbContext context)
        {
            _context = context;
        }

        public class SendMessageRequest
        {
            public string? ReceiverId { get; set; }
            public string? Content { get; set; }
        }

        public async Task SendMessage(SendMessageRequest payload)
        {
            var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(senderId) || !Guid.TryParse(senderId, out var senderGuid))
            {
                // This should not happen for an authorized user
                return;
            }

            if (payload == null || string.IsNullOrEmpty(payload.ReceiverId))
            {
                return;
            }

            if (!Guid.TryParse(payload.ReceiverId, out var receiverGuid))
            {
                // Handle invalid receiverId format
                return;
            }

            var content = payload.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var conversation = await GetOrCreateConversationAsync(senderGuid, receiverGuid);

            var chatMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderId = senderGuid,
                ReceiverId = receiverGuid,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            conversation.UpdatedAt = chatMessage.Timestamp;

            await _context.ChatMessages.AddAsync(chatMessage);
            await _context.SaveChangesAsync();

            // Try to enrich with active patient context if sender is a Patient
            object? patientContext = null;
            try
            {
                var isPatient = await _context.UserRoles
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                    .AnyAsync(x => x.UserId == senderGuid && x.Name == "Patient");

                if (isPatient)
                {
                    var patient = await _context.Patients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.UserId == senderGuid && p.IsActive);

                    if (patient != null)
                    {
                        patientContext = new
                        {
                            id = patient.Id,
                            firstName = patient.FirstName,
                            lastName = patient.LastName,
                            dateOfBirth = patient.DateOfBirth,
                            relationshipToUser = patient.RelationshipToUser,
                            address = patient.Address,
                            barangay = patient.Barangay,
                            latitude = patient.Latitude,
                            longitude = patient.Longitude,
                            occupation = patient.Occupation,
                            activityLevel = patient.ActivityLevel,
                            medicalCondition = patient.MedicalCondition,
                            surgicalHistory = patient.SurgicalHistory,
                            medicationBeingTaken = patient.MedicationBeingTaken,
                            currentComplaints = patient.CurrentComplaints
                        };
                    }
                }
            }
            catch
            {
                // swallow enrichment errors to avoid breaking chat
            }

            var broadcast = new
            {
                chatMessage.Id,
                ConversationId = conversation.Id,
                SenderId = senderId,
                ReceiverId = payload.ReceiverId,
                Content = chatMessage.Content,
                chatMessage.Timestamp,
                chatMessage.IsRead,
                PatientContext = patientContext
            };

            await Clients.Users(new[] { senderId, payload.ReceiverId }).SendAsync("ReceiveMessage", broadcast);
        }

        /// <summary>
        /// Patient explicitly shares their active patient context with the other user (therapist) for session details.
        /// If no active patient exists, a null payload is sent so clients can clear any cached context.
        /// </summary>
        /// <param name="otherUserId">The other user's Guid string.</param>
        public async Task SharePatientContext(string otherUserId)
        {
            var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(senderId) || !Guid.TryParse(senderId, out var senderGuid))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(otherUserId) || !Guid.TryParse(otherUserId, out var otherGuid))
            {
                return;
            }

            // Only patients can share patient context
            try
            {
                var isPatient = await _context.UserRoles
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                    .AnyAsync(x => x.UserId == senderGuid && x.Name == "Patient");

                if (!isPatient)
                {
                    return;
                }

                object? patientContext = null;
                var patient = await _context.Patients
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == senderGuid && p.IsActive);
                if (patient != null)
                {
                    patientContext = new
                    {
                        id = patient.Id,
                        firstName = patient.FirstName,
                        lastName = patient.LastName,
                        dateOfBirth = patient.DateOfBirth,
                        relationshipToUser = patient.RelationshipToUser,
                        address = patient.Address,
                        barangay = patient.Barangay,
                        latitude = patient.Latitude,
                        longitude = patient.Longitude,
                        occupation = patient.Occupation,
                        activityLevel = patient.ActivityLevel,
                        medicalCondition = patient.MedicalCondition,
                        surgicalHistory = patient.SurgicalHistory,
                        medicationBeingTaken = patient.MedicationBeingTaken,
                        currentComplaints = patient.CurrentComplaints
                    };
                }

                // Send to the therapist and echo to sender so both can update UI state
                await Clients.Users(new[] { otherUserId, senderId }).SendAsync("PatientContextUpdated", patientContext);
            }
            catch
            {
                // swallow errors; do not disrupt connection
            }
        }

        private async Task<Conversation> GetOrCreateConversationAsync(Guid senderId, Guid receiverId)
        {
            var (first, second) = NormalizeParticipants(senderId, receiverId);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ParticipantAId == first && c.ParticipantBId == second);

            if (conversation != null)
            {
                return conversation;
            }

            conversation = new Conversation
            {
                ParticipantAId = first,
                ParticipantBId = second,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(conversation);

            try
            {
                await _context.SaveChangesAsync();
                return conversation;
            }
            catch (DbUpdateException)
            {
                _context.Entry(conversation).State = EntityState.Detached;
                return await _context.Conversations
                    .FirstAsync(c => c.ParticipantAId == first && c.ParticipantBId == second);
            }
        }

        private static (Guid First, Guid Second) NormalizeParticipants(Guid senderId, Guid receiverId)
        {
            return senderId.CompareTo(receiverId) <= 0
                ? (senderId, receiverId)
                : (receiverId, senderId);
        }
    }
}
