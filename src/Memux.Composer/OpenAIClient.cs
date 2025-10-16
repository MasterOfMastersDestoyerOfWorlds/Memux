using System.Text;
using System.Text.Json;
using System.Net.Http;

namespace Memux.Composer;

/// <summary>
/// OpenAI API client implementation
/// Uses HTTP client for maximum compatibility
/// </summary>
public class OpenAILlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;
    
    public OpenAILlmClient(string apiKey, string model = "gpt-4")
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }
    
    public async Task<string> CompleteChatAsync(List<ChatMessage> messages, float temperature = 0.7f, int maxTokens = 2000)
    {
        var request = new
        {
            model = _model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList(),
            temperature,
            max_tokens = maxTokens
        };
        
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("chat/completions", content);
        response.EnsureSuccessStatusCode();
        
        var responseBody = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseBody);
        
        return responseObj
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}

