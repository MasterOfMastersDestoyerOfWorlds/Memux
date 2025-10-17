using Xunit;
using Memux.Core.Models;
using Memux.Core.Database;
using Memux.Skills;

namespace Memux.Tests;

public class Phase1Tests : IDisposable
{
    private readonly string _testDbPath;
    
    public Phase1Tests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"memux_test_{Guid.NewGuid()}.db");
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
    public void MemuxDatabase_Initialize_CreatesFile()
    {
        // Arrange & Act
        var db = new MemuxDatabase(_testDbPath);
        
        // Assert
        Assert.True(File.Exists(_testDbPath));
    }
    
    [Fact]
    public void PerceptionState_Initialize_Success()
    {
        // Arrange & Act
        var state = new PerceptionState
        {
            Timestamp = DateTime.UtcNow,
            Width = 1920,
            Height = 1080,
            ScreenData = new byte[1920 * 1080 * 4]
        };
        
        // Assert
        Assert.NotNull(state);
        Assert.Equal(1920, state.Width);
        Assert.Equal(1080, state.Height);
        Assert.NotNull(state.ScreenData);
    }
    
    [Fact]
    public void ActionQueue_AddActions_Success()
    {
        // Arrange
        var queue = new ActionQueue();
        
        // Act
        queue.AddWait(100);
        queue.AddKeyPress("SPACE", 50);
        
        // Assert
        var str = queue.ToCompactString();
        Assert.Contains("Wait", str);
        Assert.Contains("SPACE", str);
    }
    
    [Fact]
    public void ActionQueue_AddButtonPress_Success()
    {
        // Arrange
        var queue = new ActionQueue();
        
        // Act
        queue.AddButtonPress("A", 100);
        
        // Assert
        var str = queue.ToCompactString();
        Assert.Contains("Btn", str); // Uses abbreviated format
        Assert.Contains("A", str);
    }
    
    [Fact]
    public void ActionQueue_AddStickMovement_Success()
    {
        // Arrange
        var queue = new ActionQueue();
        
        // Act
        queue.AddStickMovement("LEFT", 0.5f, 0.8f, 200);
        
        // Assert
        var str = queue.ToCompactString();
        Assert.Contains("Stick", str);
        Assert.Contains("LEFT", str);
    }
    
    [Fact]
    public void Goal_Initialize_DefaultStatus()
    {
        // Arrange & Act
        var goal = new Goal
        {
            Description = "Test goal"
        };
        
        // Assert
        Assert.Equal(GoalStatus.Pending, goal.Status);
        Assert.NotNull(goal.Id);
        Assert.NotEqual(Guid.Empty.ToString(), goal.Id);
    }
    
    [Fact]
    public void Goal_UpdateProgress_Success()
    {
        // Arrange
        var goal = new Goal
        {
            Description = "Test goal",
            Progress = 0.0f
        };
        
        // Act
        goal.Progress = 0.5f;
        
        // Assert
        Assert.Equal(0.5f, goal.Progress);
    }
    
    [Fact]
    public void Goal_MarkCompleted_Success()
    {
        // Arrange
        var goal = new Goal
        {
            Description = "Test goal",
            Status = GoalStatus.InProgress
        };
        
        // Act
        goal.Status = GoalStatus.Completed;
        goal.CompletedAt = DateTime.UtcNow;
        
        // Assert
        Assert.Equal(GoalStatus.Completed, goal.Status);
        Assert.NotNull(goal.CompletedAt);
    }
    
    [Fact]
    public async Task Skill_CompileAndExecute_Simple()
    {
        // Arrange
        var compiler = new SkillCompiler();
        var code = "queue.AddWait(100);";
        
        // Act
        var execute = await compiler.CompileAsync(code);
        
        // Assert
        Assert.NotNull(execute);
        
        var state = new PerceptionState { Timestamp = DateTime.UtcNow, Width = 1920, Height = 1080 };
        var result = execute(state);
        Assert.NotNull(result);
    }
    
    [Fact]
    public void BoundingBox_Properties_WorkCorrectly()
    {
        // Arrange & Act
        var bbox = new BoundingBox
        {
            X = 100,
            Y = 200,
            Width = 50,
            Height = 75
        };
        
        // Assert
        Assert.Equal(100, bbox.X);
        Assert.Equal(200, bbox.Y);
        Assert.Equal(50, bbox.Width);
        Assert.Equal(75, bbox.Height);
    }
    
    [Fact]
    public void DetectedObject_Initialize_Success()
    {
        // Arrange & Act
        var obj = new DetectedObject
        {
            ClassName = "enemy",
            Confidence = 0.9f,
            BoundingBox = new BoundingBox { X = 0, Y = 0, Width = 100, Height = 100 }
        };
        
        // Assert
        Assert.Equal("enemy", obj.ClassName);
        Assert.Equal(0.9f, obj.Confidence);
        Assert.NotNull(obj.BoundingBox);
    }
    
    [Fact]
    public void OcrResult_Initialize_Success()
    {
        // Arrange & Act
        var result = new OcrResult
        {
            Text = "Health: 100",
            Confidence = 0.95f,
            BoundingBox = new BoundingBox { X = 10, Y = 10, Width = 200, Height = 50 }
        };
        
        // Assert
        Assert.Equal("Health: 100", result.Text);
        Assert.Equal(0.95f, result.Confidence);
        Assert.NotNull(result.BoundingBox);
    }
    
    [Fact]
    public async Task SkillLibrary_GetTopSkillsByElo_OrdersCorrectly()
    {
        // Arrange
        var library = new SkillLibrary(_testDbPath);
        var skill1 = await library.AddSkillAsync("Skill1", "queue.AddWait(100);", new List<string>());
        var skill2 = await library.AddSkillAsync("Skill2", "queue.AddWait(200);", new List<string>());
        
        // Simulate skill usage to update ELO
        library.RecordUsage(skill1.Id, success: true, 50);
        library.RecordUsage(skill1.Id, success: true, 50);
        library.RecordUsage(skill2.Id, success: false, 50);
        
        // Act
        var topSkills = await library.GetTopSkillsAsync(2);
        
        // Assert
        Assert.Equal(2, topSkills.Count);
        // Skill1 should have higher ELO due to successes
        Assert.True(topSkills[0].SuccessCount >= topSkills[1].SuccessCount);
    }
    
    [Fact]
    public async Task SkillLibrary_SearchByTags_FindsMatches()
    {
        // Arrange
        var library = new SkillLibrary(_testDbPath);
        await library.AddSkillAsync("CombatSkill", "queue.AddButtonPress(\"A\", 100);", new List<string> { "combat", "attack" });
        await library.AddSkillAsync("MovementSkill", "queue.AddStickMovement(\"LEFT\", 1, 0, 500);", new List<string> { "movement", "navigation" });
        
        // Act
        var combatSkills = await library.SearchByTagsAsync(new List<string> { "combat" });
        
        // Assert
        Assert.NotEmpty(combatSkills);
        Assert.All(combatSkills, s => Assert.Contains("combat", s.Tags));
    }
    
    [Fact]
    public void ActionQueue_ToCompactString_FormatsCorrectly()
    {
        // Arrange
        var queue = new ActionQueue();
        queue.AddWait(100);
        queue.AddKeyPress("E", 50);
        
        // Act
        var str = queue.ToCompactString();
        
        // Assert
        Assert.NotNull(str);
        Assert.NotEmpty(str);
        Assert.Contains("100", str); // Wait duration
        Assert.Contains("E", str); // Key name
    }
}

