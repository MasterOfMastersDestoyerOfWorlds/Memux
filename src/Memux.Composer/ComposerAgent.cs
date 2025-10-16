using Memux.Skills;

namespace Memux.Composer;

/// <summary>
/// Uses large LLM (GPT-4/Claude) for skill composition and generation
/// Following Voyager's iterative prompting pattern
/// </summary>
public class ComposerAgent
{
    private readonly ILlmClient _client;
    private readonly PromptTemplates _templates;
    
    public ComposerAgent(string apiKey, string model = "gpt-4")
    {
        _client = new OpenAILlmClient(apiKey, model);
        _templates = new PromptTemplates();
    }
    
    public ComposerAgent(ILlmClient client)
    {
        _client = client;
        _templates = new PromptTemplates();
    }
    
    /// <summary>
    /// Generate a new skill from a natural language goal
    /// </summary>
    public async Task<SkillGenerationResult> GenerateSkillAsync(
        string goalDescription,
        List<Skill> availableSkills,
        string? previousError = null)
    {
        var systemPrompt = _templates.GetSkillGenerationSystemPrompt();
        var userPrompt = _templates.GetSkillGenerationUserPrompt(
            goalDescription,
            availableSkills,
            previousError);
        
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        var content = await _client.CompleteChatAsync(messages, temperature: 0.7f, maxTokens: 2000);
        
        // Parse response to extract skill code
        var (skillName, skillCode, tags) = ParseSkillGenerationResponse(content);
        
        return new SkillGenerationResult
        {
            SkillName = skillName,
            SkillCode = skillCode,
            Tags = tags,
            RawResponse = content
        };
    }
    
    /// <summary>
    /// Compose multiple existing skills into a meta-skill
    /// </summary>
    public async Task<SkillGenerationResult> ComposeMetaSkillAsync(
        string metaSkillName,
        List<Skill> componentSkills,
        string purpose)
    {
        var systemPrompt = _templates.GetMetaSkillCompositionSystemPrompt();
        var userPrompt = _templates.GetMetaSkillCompositionUserPrompt(
            metaSkillName,
            componentSkills,
            purpose);
        
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        var content = await _client.CompleteChatAsync(messages, temperature: 0.5f, maxTokens: 1500);
        
        var (skillName, skillCode, tags) = ParseSkillGenerationResponse(content);
        
        return new SkillGenerationResult
        {
            SkillName = skillName,
            SkillCode = skillCode,
            Tags = tags,
            Dependencies = componentSkills.Select(s => s.Id).ToList(),
            RawResponse = content
        };
    }
    
    /// <summary>
    /// Debug a failed skill by analyzing error and suggesting fixes
    /// </summary>
    public async Task<string> DebugSkillAsync(
        Skill failedSkill,
        string errorMessage,
        string executionContext)
    {
        var systemPrompt = _templates.GetSkillDebuggingSystemPrompt();
        var userPrompt = _templates.GetSkillDebuggingUserPrompt(
            failedSkill,
            errorMessage,
            executionContext);
        
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        return await _client.CompleteChatAsync(messages, temperature: 0.3f, maxTokens: 1500);
    }
    
    private (string name, string code, List<string> tags) ParseSkillGenerationResponse(string response)
    {
        // Parse LLM response to extract structured skill information
        // Expected format:
        // SKILL_NAME: <name>
        // TAGS: <tag1>, <tag2>, ...
        // CODE:
        // <code block>
        
        string name = "UnnamedSkill";
        var tags = new List<string>();
        string code = "";
        
        var lines = response.Split('\n');
        bool inCodeBlock = false;
        var codeLines = new List<string>();
        
        foreach (var line in lines)
        {
            if (line.StartsWith("SKILL_NAME:", StringComparison.OrdinalIgnoreCase))
            {
                name = line.Substring("SKILL_NAME:".Length).Trim();
            }
            else if (line.StartsWith("TAGS:", StringComparison.OrdinalIgnoreCase))
            {
                var tagStr = line.Substring("TAGS:".Length).Trim();
                tags = tagStr.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }
            else if (line.Contains("```csharp") || line.Contains("```c#"))
            {
                inCodeBlock = true;
            }
            else if (line.Contains("```") && inCodeBlock)
            {
                inCodeBlock = false;
            }
            else if (inCodeBlock)
            {
                codeLines.Add(line);
            }
        }
        
        code = string.Join("\n", codeLines).Trim();
        
        // Fallback: if no code block found, try to extract everything after "CODE:"
        if (string.IsNullOrEmpty(code))
        {
            int codeIndex = response.IndexOf("CODE:", StringComparison.OrdinalIgnoreCase);
            if (codeIndex >= 0)
            {
                code = response.Substring(codeIndex + 5).Trim();
            }
        }
        
        return (name, code, tags);
    }
}

public class SkillGenerationResult
{
    public string SkillName { get; set; } = string.Empty;
    public string SkillCode { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;
}

