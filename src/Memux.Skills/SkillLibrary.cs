using Memux.Core.Database;
using System.Text.Json;

namespace Memux.Skills;

/// <summary>
/// Manages the skill library with database persistence
/// Implements CRUD operations, ELO updates, and skill retrieval
/// </summary>
public class SkillLibrary
{
    private readonly MemuxDatabase _database;
    private readonly SkillCompiler _compiler;
    private readonly Dictionary<string, Skill> _loadedSkills = new();
    private readonly object _lock = new();
    
    public SkillLibrary(string databasePath)
    {
        _database = new MemuxDatabase(databasePath);
        _compiler = new SkillCompiler();
    }
    
    /// <summary>
    /// Add a new skill to the library
    /// </summary>
    public async Task<Skill> AddSkillAsync(
        string name,
        string code,
        List<string> tags,
        List<string>? dependencies = null,
        string? codeLocation = null)
    {
        var skill = new Skill
        {
            Name = name,
            Code = code,
            Tags = tags,
            Dependencies = dependencies ?? new(),
            CodeLocation = codeLocation
        };
        
        // Compile the skill
        skill.Execute = await _compiler.CompileAsync(code);
        
        // Store in database
        _database.InsertSkill(
            skill.Id,
            skill.Name,
            skill.Code,
            skill.Dependencies,
            skill.Tags,
            skill.CodeLocation,
            skill.EloRating
        );
        
        // Cache in memory
        lock (_lock)
        {
            _loadedSkills[skill.Id] = skill;
        }
        
        return skill;
    }
    
    /// <summary>
    /// Get skill by ID
    /// </summary>
    public async Task<Skill?> GetSkillAsync(string id)
    {
        lock (_lock)
        {
            if (_loadedSkills.TryGetValue(id, out var cachedSkill))
            {
                return cachedSkill;
            }
        }
        
        var record = _database.GetSkillById(id);
        if (record == null)
        {
            return null;
        }
        
        var skill = await LoadSkillFromRecord(record);
        
        lock (_lock)
        {
            _loadedSkills[skill.Id] = skill;
        }
        
        return skill;
    }
    
    /// <summary>
    /// Get top skills by ELO rating
    /// </summary>
    public async Task<List<Skill>> GetTopSkillsAsync(int limit = 10)
    {
        var records = _database.GetTopSkillsByElo(limit);
        var skills = new List<Skill>();
        
        foreach (var record in records)
        {
            var skill = await LoadSkillFromRecord(record);
            skills.Add(skill);
            
            lock (_lock)
            {
                _loadedSkills[skill.Id] = skill;
            }
        }
        
        return skills;
    }
    
    /// <summary>
    /// Search skills by tags
    /// </summary>
    public async Task<List<Skill>> SearchByTagsAsync(List<string> tags, int limit = 10)
    {
        var records = _database.SearchSkillsByTags(tags, limit);
        var skills = new List<Skill>();
        
        foreach (var record in records)
        {
            var skill = await LoadSkillFromRecord(record);
            skills.Add(skill);
            
            lock (_lock)
            {
                _loadedSkills[skill.Id] = skill;
            }
        }
        
        return skills;
    }
    
    /// <summary>
    /// Update skill ELO rating after execution
    /// Uses standard ELO formula: new_elo = old_elo + K * (actual - expected)
    /// </summary>
    public void UpdateElo(string winnerId, string loserId, double k = 32.0)
    {
        var winner = _database.GetSkillById(winnerId);
        var loser = _database.GetSkillById(loserId);
        
        if (winner == null || loser == null)
        {
            return;
        }
        
        // Calculate expected scores
        double expectedWinner = 1.0 / (1.0 + Math.Pow(10, (loser.EloRating - winner.EloRating) / 400.0));
        double expectedLoser = 1.0 - expectedWinner;
        
        // Update ratings
        double newWinnerElo = winner.EloRating + k * (1.0 - expectedWinner);
        double newLoserElo = loser.EloRating + k * (0.0 - expectedLoser);
        
        _database.UpdateSkillElo(winnerId, newWinnerElo);
        _database.UpdateSkillElo(loserId, newLoserElo);
        
        // Update cached skills
        lock (_lock)
        {
            if (_loadedSkills.TryGetValue(winnerId, out var winnerSkill))
            {
                winnerSkill.EloRating = newWinnerElo;
            }
            if (_loadedSkills.TryGetValue(loserId, out var loserSkill))
            {
                loserSkill.EloRating = newLoserElo;
            }
        }
    }
    
    /// <summary>
    /// Record skill usage outcome
    /// </summary>
    public void RecordUsage(string skillId, bool success, int executionTimeMs, string? context = null, string? errorMessage = null)
    {
        _database.RecordSkillUsage(skillId, success, executionTimeMs, context, errorMessage);
        
        lock (_lock)
        {
            if (_loadedSkills.TryGetValue(skillId, out var skill))
            {
                skill.UsageCount++;
                if (success)
                {
                    skill.SuccessCount++;
                }
                else
                {
                    skill.FailureCount++;
                }
                skill.LastUsed = DateTime.UtcNow;
            }
        }
    }
    
    private async Task<Skill> LoadSkillFromRecord(SkillRecord record)
    {
        var skill = new Skill
        {
            Id = record.Id,
            Name = record.Name,
            Code = record.Code,
            Dependencies = record.Dependencies,
            EloRating = record.EloRating,
            Tags = record.Tags,
            CodeLocation = record.CodeLocation,
            UsageCount = record.UsageCount
        };
        
        // Compile the skill
        try
        {
            skill.Execute = await _compiler.CompileAsync(record.Code);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to compile skill {record.Name}: {ex.Message}");
            // Skill will have null Execute delegate
        }
        
        return skill;
    }
    
    /// <summary>
    /// Create seed skills for bootstrapping
    /// </summary>
    public async Task CreateSeedSkillsAsync()
    {
        // Basic wait skill
        await AddSkillAsync(
            "Wait",
            @"queue.AddWait(500);",
            new List<string> { "utility", "timing" }
        );
        
        // Press key skill
        await AddSkillAsync(
            "PressSpace",
            @"queue.AddKeyPress(""SPACE"", 100);",
            new List<string> { "input", "keyboard" }
        );
        
        // Dodge (for Dark Souls)
        await AddSkillAsync(
            "DodgeRoll",
            @"queue.AddButtonPress(""B"", 50);",
            new List<string> { "combat", "dodge", "defensive" }
        );
        
        // Move forward
        await AddSkillAsync(
            "MoveForward",
            @"queue.AddStickMovement(""LEFT"", 0, 1.0f, 1000);",
            new List<string> { "movement", "navigation" }
        );
    }
}

