using agapay_backend.Data;
using agapay_backend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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

        public async Task SendMessage(string receiverId, string message)
        {
            var senderId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(senderId) || !Guid.TryParse(senderId, out var senderGuid))
            {
                // This should not happen for an authorized user
                return;
            }

            if (!Guid.TryParse(receiverId, out var receiverGuid))
            {
                // Handle invalid receiverId format
                return;
            }

            var chatMessage = new ChatMessage
            {
                SenderId = senderGuid,
                ReceiverId = receiverGuid,
                Content = message,
                Timestamp = DateTime.UtcNow
            };

            await _context.ChatMessages.AddAsync(chatMessage);
            await _context.SaveChangesAsync();

            // Send the message to the recipient
            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message, chatMessage.Timestamp);

            // Also send the message back to the sender for UI confirmation
            await Clients.User(senderId).SendAsync("ReceiveMessage", senderId, message, chatMessage.Timestamp);
        }
    }
}