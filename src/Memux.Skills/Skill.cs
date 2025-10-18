using Memux.Core.Models;

namespace Memux.Skills;

/// <summary>
/// Represents an executable skill in the Memux system
/// Skills are C# code that transforms PerceptionState -> ActionQueue
/// </summary>
public class Skill
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public double EloRating { get; set; } = 1000.0;
    public int UsageCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Dependencies { get; set; } = new();
    public string? CodeLocation { get; set; }
    
    /// <summary>
    /// Compiled delegate for execution
    /// This is not serialized - recompiled on load
    /// </summary>
    public Func<PerceptionState, ActionQueue>? Execute { get; set; }

    /// <summary>
    /// Optional runtime subskill invoker. If provided, skills can call subskills
    /// by name and this will record a runtime call graph for UI.
    /// </summary>
    public Func<string, PerceptionState, ActionQueue?>? InvokeSubskill { get; set; }
    
    /// <summary>
    /// Success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => UsageCount > 0 ? (double)SuccessCount / UsageCount : 0.0;
    
    /// <summary>
    /// Description generated from tags and usage
    /// </summary>
    public string GetDescription()
    {
        var tagStr = Tags.Count > 0 ? string.Join(", ", Tags) : "general";
        return $"{Name} ({tagStr}) - Used {UsageCount} times, {SuccessRate:P0} success rate, ELO: {EloRating:F0}";
    }
}

