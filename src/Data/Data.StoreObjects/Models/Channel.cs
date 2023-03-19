using System.ComponentModel.DataAnnotations;

namespace Data.StoreObjects.Models;

public class Channel
{
    public Guid Id { get; set; }

    [Required]
    public string ChannelName { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public bool AutoJoin { get; set; } = false;

    public ICollection<ChatMessage> Messages { get; set; } = default!;
}
