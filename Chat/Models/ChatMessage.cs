namespace Chat.Models
{
    public class ChatMessage
    {
        public string User { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Room { get; set; } = "general";
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}