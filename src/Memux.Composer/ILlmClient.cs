namespace Memux.Composer;

/// <summary>
/// Abstract interface for LLM clients
/// Allows swapping between OpenAI, Anthropic, local models, etc.
/// </summary>
public interface ILlmClient
{
    Task<string> CompleteChatAsync(List<ChatMessage> messages, float temperature = 0.7f, int maxTokens = 2000);
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty;
}

