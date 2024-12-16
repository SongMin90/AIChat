public class ChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
    public string ImageBase64 { get; set; }

    public ChatMessage(string role, string content, string imageBase64)
    {
        Role = role;
        Content = content;
        ImageBase64 = imageBase64;
    }
} 