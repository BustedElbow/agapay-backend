using agapay_backend.Data;
using agapay_backend.Entities;
using agapay_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace agapay_backend.Controllers
{
  [Authorize]
  [ApiController]
  [Route("api/[controller]")]
  public class ChatController : ControllerBase
  {
    private readonly agapayDbContext _context;
    private readonly ISupabaseStorageService _storageService;

    public ChatController(agapayDbContext context, ISupabaseStorageService storageService)
    {
      _context = context;
      _storageService = storageService;
    }

    [HttpPost("conversations/{otherUserId}")]
    public async Task<IActionResult> StartConversation(string otherUserId)
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(currentUserId, out var currentUserGuid) || !Guid.TryParse(otherUserId, out var otherUserGuid))
      {
        return BadRequest("Invalid user ID format.");
      }

      if (currentUserGuid == otherUserGuid)
      {
        return BadRequest("Cannot start a conversation with yourself.");
      }

      var otherUserExists = await _context.Users.AnyAsync(u => u.Id == otherUserGuid);
      if (!otherUserExists)
      {
        return NotFound("The specified user does not exist.");
      }

      var (first, second) = NormalizeParticipants(currentUserGuid, otherUserGuid);

      var conversation = await _context.Conversations
          .FirstOrDefaultAsync(c => c.ParticipantAId == first && c.ParticipantBId == second);

      if (conversation == null)
      {
        conversation = new Conversation
        {
          ParticipantAId = first,
          ParticipantBId = second,
          CreatedAt = DateTime.UtcNow,
          UpdatedAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
      }

      // Try to include basic other user info (name + avatar) to allow immediate optimistic render
      var otherUser = await _context.Users
          .AsNoTracking()
          .FirstOrDefaultAsync(u => u.Id == otherUserGuid);

      string? avatar = null;
      if (!string.IsNullOrWhiteSpace(otherUser?.ProfilePictureUrl))
      {
        try
        {
          var signed = await _storageService.GetSignedUrlAsync(otherUser!.ProfilePictureUrl!, 3600);
          avatar = signed ?? _storageService.GetPublicUrl(otherUser.ProfilePictureUrl!);
        }
        catch { /* ignore avatar failure */ }
      }

      return Ok(new
      {
        conversationId = conversation.Id,
        otherUserId = otherUserId,
        otherUserName = otherUser != null ? ($"{otherUser.FirstName} {otherUser.LastName}") : null,
        otherUserAvatar = avatar
      });
    }

    [HttpGet("history/{otherUserId}")]
    public async Task<IActionResult> GetConversationHistory(string otherUserId)
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(currentUserId, out var currentUserGuid) || !Guid.TryParse(otherUserId, out var otherUserGuid))
      {
        return BadRequest("Invalid user ID format.");
      }

      // Attempt to gather patient context when the other user is a Patient
      object? patientContext = null;
      try
      {
        var otherIsPatient = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .AnyAsync(x => x.UserId == otherUserGuid && x.Name == "Patient");

        if (otherIsPatient)
        {
          var patient = await _context.Patients
              .AsNoTracking()
              .FirstOrDefaultAsync(p => p.UserId == otherUserGuid && p.IsActive);
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
      catch { /* ignore enrichment failures */ }

      var messages = await _context.ChatMessages
          .Where(m => (m.SenderId == currentUserGuid && m.ReceiverId == otherUserGuid) ||
                       (m.SenderId == otherUserGuid && m.ReceiverId == currentUserGuid))
          .OrderBy(m => m.Timestamp)
          .Select(m => new
          {
            m.Id,
            m.ConversationId,
            m.SenderId,
            m.ReceiverId,
            m.Content,
            m.Timestamp,
            m.IsRead,
            IsMine = m.SenderId == currentUserGuid,
            PatientContext = patientContext
          })
          .ToListAsync();

      return Ok(messages);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversationSummaries()
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(currentUserId, out var currentUserGuid))
      {
        return BadRequest("Invalid user ID format.");
      }

      var summaries = await _context.Conversations
          .Where(c => c.ParticipantAId == currentUserGuid || c.ParticipantBId == currentUserGuid)
          .Select(c => new
          {
            c.Id,
            OtherUserId = c.ParticipantAId == currentUserGuid ? c.ParticipantBId : c.ParticipantAId,
            LatestMessageTimestamp = c.Messages
                  .OrderByDescending(m => m.Timestamp)
                  .Select(m => (DateTime?)m.Timestamp)
                  .FirstOrDefault(),
            LatestMessage = c.Messages
                  .OrderByDescending(m => m.Timestamp)
                  .Select(m => m.Content)
                  .FirstOrDefault(),
            UnreadCount = c.Messages.Count(m => m.ReceiverId == currentUserGuid && !m.IsRead),
            c.UpdatedAt
          })
          .Select(c => new
          {
            conversationId = c.Id,
            otherUserId = c.OtherUserId,
            latestMessageTimestamp = c.LatestMessageTimestamp ?? c.UpdatedAt,
            latestMessage = c.LatestMessage,
            unreadCount = c.UnreadCount
          })
          .OrderByDescending(c => c.latestMessageTimestamp)
          .ToListAsync();

      var otherIds = summaries.Select(c => c.otherUserId).ToList();
      var users = await _context.Users
          .Where(u => otherIds.Contains(u.Id))
          .Select(u => new { u.Id, u.FirstName, u.LastName, u.ProfilePictureUrl })
          .ToListAsync();

      var roles = await _context.UserRoles
          .Where(ur => otherIds.Contains(ur.UserId))
          .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
          .ToListAsync();

      string PickRole(IEnumerable<string> names)
      {
        var order = new[] { "PhysicalTherapist", "Patient" };
        var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in order)
        {
          if (set.Contains(r)) return r;
        }
        var firstNonUser = names.FirstOrDefault(n => !string.Equals(n, "User", StringComparison.OrdinalIgnoreCase));
        return firstNonUser ?? "User";
      }

      var result = new List<object>();
      foreach (var summary in summaries)
      {
        var user = users.FirstOrDefault(u => u.Id == summary.otherUserId);
        var roleNames = roles.Where(r => r.UserId == summary.otherUserId)
            .Select(r => r.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!);

        string? avatar = null;
        if (!string.IsNullOrWhiteSpace(user?.ProfilePictureUrl))
        {
          try
          {
            var signed = await _storageService.GetSignedUrlAsync(user!.ProfilePictureUrl!, 3600);
            avatar = signed ?? _storageService.GetPublicUrl(user.ProfilePictureUrl!);
          }
          catch { /* ignore avatar errors */ }
        }

        result.Add(new
        {
          summary.conversationId,
          otherUserId = summary.otherUserId,
          otherUserName = user != null ? ($"{user.FirstName} {user.LastName}") : null,
          otherUserRole = PickRole(roleNames),
          otherUserAvatar = avatar,
          latestMessage = summary.latestMessage,
          latestMessageTimestamp = summary.latestMessageTimestamp,
          unreadCount = summary.unreadCount
        });
      }

      return Ok(result);
    }

    [HttpPost("history/{otherUserId}/read")]
    public async Task<IActionResult> MarkConversationAsRead(string otherUserId)
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!Guid.TryParse(currentUserId, out var currentUserGuid) || !Guid.TryParse(otherUserId, out var otherUserGuid))
      {
        return BadRequest("Invalid user ID format.");
      }

      var unreadMessages = await _context.ChatMessages
          .Where(m => m.SenderId == otherUserGuid && m.ReceiverId == currentUserGuid && !m.IsRead)
          .ToListAsync();

      if (unreadMessages.Count == 0)
      {
        return Ok(new { UpdatedMessages = 0 });
      }

      foreach (var message in unreadMessages)
      {
        message.IsRead = true;
      }

      await _context.SaveChangesAsync();

      return Ok(new { UpdatedMessages = unreadMessages.Count });
    }

    private static (Guid First, Guid Second) NormalizeParticipants(Guid userA, Guid userB)
    {
      return userA.CompareTo(userB) <= 0 ? (userA, userB) : (userB, userA);
    }
  }
}
