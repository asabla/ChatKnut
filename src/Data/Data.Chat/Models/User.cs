using System.ComponentModel.DataAnnotations;

namespace ChatKnut.Data.Chat.Models;

public class User
{
    public Guid Id { get; set; }

    [Required]
    public string UserName { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public ICollection<ChatMessage> Messages { get; set; }
        = new List<ChatMessage>();
}