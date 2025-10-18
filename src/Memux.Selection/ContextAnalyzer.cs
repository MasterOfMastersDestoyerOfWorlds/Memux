using Memux.Core.Models;

namespace Memux.Selection;

/// <summary>
/// Extracts high-level context from PerceptionState
/// Used to determine which skills are relevant
/// </summary>
public class ContextAnalyzer
{
    public ContextInfo AnalyzeContext(PerceptionState state)
    {
        var context = new ContextInfo
        {
            Timestamp = state.Timestamp
        };
        
        // Focused program tag for skill scoping
        if (!string.IsNullOrEmpty(state.FocusedProgram))
        {
            context.Tags.Add($"program:{state.FocusedProgram}");
        }
        
        // Analyze OCR text for context clues
        if (state.OcrResults != null && state.OcrResults.Any())
        {
            var allText = string.Join(" ", state.OcrResults.Select(r => r.Text)).ToLower();
            
            // Menu detection
            if (allText.Contains("menu") || allText.Contains("options") || allText.Contains("settings"))
            {
                context.IsInMenu = true;
                context.Tags.Add("menu");
            }
            
            // Combat detection
            if (allText.Contains("hp") || allText.Contains("health") || allText.Contains("stamina"))
            {
                context.IsInCombat = true;
                context.Tags.Add("combat");
            }
            
            // Dialog detection
            if (allText.Contains("talk") || allText.Contains("speak") || allText.Length > 50)
            {
                context.IsInDialog = true;
                context.Tags.Add("dialog");
            }
        }
        
        // Analyze depth map for spatial context
        if (state.DepthMap != null && state.DepthMap.Length > 0)
        {
            // Check for nearby obstacles (low depth values in center)
            int centerStart = state.DepthMap.Length / 2 - 1000;
            int centerEnd = state.DepthMap.Length / 2 + 1000;
            if (centerStart >= 0 && centerEnd < state.DepthMap.Length)
            {
                float avgCenterDepth = state.DepthMap[centerStart..centerEnd].Average();
                if (avgCenterDepth < 0.3f)
                {
                    context.HasNearbyObstacle = true;
                    context.Tags.Add("obstacle");
                }
            }
        }
        
        // Analyze detected objects
        if (state.DetectedObjects != null && state.DetectedObjects.Any())
        {
            foreach (var obj in state.DetectedObjects)
            {
                context.DetectedObjects.Add(obj.ClassName);
                context.Tags.Add(obj.ClassName.ToLower());
            }
            
            // Check for enemies
            if (state.DetectedObjects.Any(o => 
                o.ClassName.Contains("enemy", StringComparison.OrdinalIgnoreCase) ||
                o.ClassName.Contains("hollow", StringComparison.OrdinalIgnoreCase)))
            {
                context.IsInCombat = true;
                context.Tags.Add("combat");
                context.Tags.Add("enemy-present");
            }
        }
        
        // Check game-specific context hints
        if (state.ContextHints != null)
        {
            foreach (var hint in state.ContextHints)
            {
                context.CustomHints[hint.Key] = hint.Value;
                context.Tags.Add(hint.Key.ToLower());
            }
        }
        
        // Deduplicate tags
        context.Tags = context.Tags.Distinct().ToList();
        
        return context;
    }
    
    /// <summary>
    /// Generate a compact string description of context
    /// Used for LLM prompts
    /// </summary>
    public string GetContextDescription(ContextInfo context)
    {
        var parts = new List<string>();
        
        if (context.IsInCombat)
            parts.Add("in combat");
        if (context.IsInMenu)
            parts.Add("in menu");
        if (context.IsInDialog)
            parts.Add("in dialog");
        if (context.HasNearbyObstacle)
            parts.Add("obstacle ahead");
        if (context.DetectedObjects.Any())
            parts.Add($"sees: {string.Join(", ", context.DetectedObjects.Take(3))}");
        
        return parts.Any() ? string.Join(", ", parts) : "idle";
    }
}

public class ContextInfo
{
    public DateTime Timestamp { get; set; }
    public bool IsInCombat { get; set; }
    public bool IsInMenu { get; set; }
    public bool IsInDialog { get; set; }
    public bool HasNearbyObstacle { get; set; }
    public List<string> DetectedObjects { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> CustomHints { get; set; } = new();
}

