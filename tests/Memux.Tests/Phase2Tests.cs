using Xunit;
using Memux.Core.Models;
using Memux.Skills;
using Memux.Composer;
using Memux.Curriculum;
using Memux.CodeGen;

namespace Memux.Tests;

public class Phase2Tests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SkillLibrary _skillLibrary;
    
    public Phase2Tests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"memux_test_{Guid.NewGuid()}.db");
        _skillLibrary = new SkillLibrary(_testDbPath);
    }
    
    public void Dispose()
    {
        // Force garbage collection to release database connections
        GC.Collect();
        GC.WaitForPendingFinalizers();
        
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch (IOException)
        {
            // Database file might still be locked, ignore
        }
    }
    
    [Fact]
    public async Task SkillLibrary_CreateSeedSkills_Success()
    {
        // Arrange & Act
        await _skillLibrary.CreateSeedSkillsAsync();
        var skills = await _skillLibrary.GetTopSkillsAsync(10);
        
        // Assert
        Assert.NotEmpty(skills);
        Assert.True(skills.Count >= 4, "Should have at least 4 seed skills");
        Assert.Contains(skills, s => s.Name.Contains("Wait"));
    }
    
    [Fact]
    public async Task SkillLibrary_AddAndRetrieveSkill_Success()
    {
        // Arrange
        var skillName = "TestSkill";
        var skillCode = "queue.AddWait(100);";
        var tags = new List<string> { "test", "simple" };
        
        // Act
        var skill = await _skillLibrary.AddSkillAsync(skillName, skillCode, tags);
        var retrieved = await _skillLibrary.GetSkillAsync(skill.Id);
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(skillName, retrieved.Name);
        Assert.Equal(skillCode, retrieved.Code);
        Assert.Equal(tags, retrieved.Tags);
    }
    
    [Fact]
    public async Task SkillLibrary_EloRating_InitialValue()
    {
        // Arrange & Act
        await _skillLibrary.CreateSeedSkillsAsync();
        var skills = await _skillLibrary.GetTopSkillsAsync(1);
        
        // Assert
        Assert.NotEmpty(skills);
        Assert.Equal(1000.0, skills[0].EloRating);
    }
    
    [Fact]
    public async Task SkillLibrary_RecordUsage_UpdatesStats()
    {
        // Arrange
        var skill = await _skillLibrary.AddSkillAsync("TestSkill", "queue.AddWait(100);", new List<string>());
        
        // Act
        _skillLibrary.RecordUsage(skill.Id, success: true, executionTimeMs: 50);
        var updated = await _skillLibrary.GetSkillAsync(skill.Id);
        
        // Assert
        Assert.NotNull(updated);
        Assert.Equal(1, updated.UsageCount);
        Assert.Equal(1, updated.SuccessCount);
        Assert.Equal(0, updated.FailureCount);
    }
    
    [Fact]
    public void SkillTemplateEngine_GenerateVariations_Success()
    {
        // Arrange
        var engine = new SkillTemplateEngine();
        
        // Act
        var variations = engine.GenerateVariations("DirectionalDodge");
        
        // Assert
        Assert.NotEmpty(variations);
        Assert.True(variations.Count >= 4, "Should have at least 4 directional variations");
        Assert.All(variations, v => Assert.Contains("dodge", string.Join(",", v.tags), StringComparison.OrdinalIgnoreCase));
    }
    
    [Fact]
    public void SkillTemplateEngine_GenerateKeyPressVariations_Success()
    {
        // Arrange
        var engine = new SkillTemplateEngine();
        
        // Act
        var variations = engine.GenerateVariations("KeyPress");
        
        // Assert
        Assert.NotEmpty(variations);
        Assert.All(variations, v => Assert.Contains("AddKeyPress", v.code));
    }
    
    [Fact]
    public async Task SkillCompiler_CompileSimpleSkill_Success()
    {
        // Arrange
        var compiler = new SkillCompiler();
        var code = "queue.AddWait(100);";
        
        // Act
        var execute = await compiler.CompileAsync(code);
        
        // Assert
        Assert.NotNull(execute);
        
        // Test execution
        var state = new PerceptionState { Timestamp = DateTime.UtcNow, Width = 1920, Height = 1080 };
        var result = execute(state);
        Assert.NotNull(result);
    }
    
    [Fact]
    public async Task SkillCompiler_CompileInvalidSkill_ThrowsException()
    {
        // Arrange
        var compiler = new SkillCompiler();
        var code = "this is invalid C# code @#$%";
        
        // Act & Assert
        await Assert.ThrowsAsync<SkillCompilationException>(async () =>
        {
            await compiler.CompileAsync(code);
        });
    }
    
    [Fact]
    public void CurriculumAgent_Initialize_Success()
    {
        // Arrange & Act
        var mockClient = new MockLlmClient();
        var curriculum = new CurriculumAgent(mockClient);
        
        // Assert
        Assert.NotNull(curriculum);
    }
    
    [Fact]
    public void CurriculumAgent_AddGoal_Success()
    {
        // Arrange
        var mockClient = new MockLlmClient();
        var curriculum = new CurriculumAgent(mockClient);
        var goal = new Goal { Description = "Test goal" };
        
        // Act
        curriculum.AddGoal(goal);
        var currentGoal = curriculum.GetCurrentGoal();
        
        // Assert
        Assert.NotNull(currentGoal);
        Assert.Equal("Test goal", currentGoal.Description);
    }
    
    [Fact]
    public void CurriculumAgent_UpdateProgress_Success()
    {
        // Arrange
        var mockClient = new MockLlmClient();
        var curriculum = new CurriculumAgent(mockClient);
        var goal = new Goal { Description = "Test goal" };
        curriculum.AddGoal(goal);
        
        // Act
        curriculum.UpdateGoalProgress(goal.Id, 0.5f);
        var updated = curriculum.GetCurrentGoal();
        
        // Assert
        Assert.NotNull(updated);
        Assert.Equal(0.5f, updated.Progress);
        Assert.Equal(GoalStatus.InProgress, updated.Status);
    }
    
    [Fact]
    public void CurriculumAgent_CompleteGoal_Success()
    {
        // Arrange
        var mockClient = new MockLlmClient();
        var curriculum = new CurriculumAgent(mockClient);
        var goal = new Goal { Description = "Test goal" };
        curriculum.AddGoal(goal);
        
        // Act
        curriculum.CompleteGoal(goal.Id, 1.0f);
        var completed = curriculum.GetCurrentGoal();
        
        // Assert - GetCurrentGoal should return null or next goal since current is completed
        Assert.True(completed == null || completed.Status != GoalStatus.Completed);
    }
    
    [Fact]
    public void ComposerAgent_Initialize_Success()
    {
        // Arrange & Act
        var mockClient = new MockLlmClient();
        var composer = new ComposerAgent(mockClient);
        
        // Assert
        Assert.NotNull(composer);
    }
    
    [Fact]
    public void PromptTemplates_GetSystemPrompt_NotEmpty()
    {
        // Arrange
        var templates = new PromptTemplates();
        
        // Act
        var prompt = templates.GetSkillGenerationSystemPrompt();
        
        // Assert
        Assert.NotNull(prompt);
        Assert.NotEmpty(prompt);
        Assert.Contains("C#", prompt);
    }
    
    [Fact]
    public async Task PromptTemplates_GetUserPrompt_ContainsGoal()
    {
        // Arrange
        var templates = new PromptTemplates();
        await _skillLibrary.CreateSeedSkillsAsync();
        var skills = await _skillLibrary.GetTopSkillsAsync(5);
        var goalDescription = "Move forward for 2 seconds";
        
        // Act
        var prompt = templates.GetSkillGenerationUserPrompt(goalDescription, skills, null);
        
        // Assert
        Assert.NotNull(prompt);
        Assert.Contains(goalDescription, prompt);
        Assert.Contains("skill", prompt, StringComparison.OrdinalIgnoreCase);
    }
}

// Mock LLM client for testing
public class MockLlmClient : ILlmClient
{
    public Task<string> CompleteChatAsync(List<ChatMessage> messages, float temperature = 0.7f, int maxTokens = 2000)
    {
        // Return a mock response that simulates LLM output
        return Task.FromResult(@"SKILL_NAME: MockGeneratedSkill
TAGS: test, mock, generated
CODE:
```csharp
queue.AddWait(500);
```");
    }
}

