using LLama;
using LLama.Common;
using Memux.Core.Models;
using Memux.Skills;

namespace Memux.Selection;

/// <summary>
/// Fast skill selection using local LLM
/// Target: <16ms selection time
/// </summary>
public class SkillSelector : IDisposable
{
    private readonly LLamaWeights? _model;
    private readonly LLamaContext? _context;
    private readonly SkillLibrary _skillLibrary;
    private readonly ContextAnalyzer _contextAnalyzer;
    private readonly SkillCache _cache;
    private readonly int _maxSkillsToConsider = 20; // Limit for performance
    
    public SkillSelector(string modelPath, SkillLibrary skillLibrary, bool useCache = true)
    {
        _skillLibrary = skillLibrary;
        _contextAnalyzer = new ContextAnalyzer();
        _cache = new SkillCache(cacheEnabled: useCache);
        
        // Load local LLM model
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"Warning: Local LLM model not found at {modelPath}");
            Console.WriteLine("Skill selection will use fallback rule-based selector.");
            return;
        }
        
        try
        {
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 2048,      // Smaller context for speed
                GpuLayerCount = 20,      // Offload some layers to GPU
                UseMemorymap = true,     // Fast loading
                UseMemoryLock = false,
                Seed = 1337
            };
            
            _model = LLamaWeights.LoadFromFile(parameters);
            _context = _model.CreateContext(parameters);
            
            Console.WriteLine($"Skill selector loaded: {modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load skill selector model: {ex.Message}");
            Console.WriteLine("Falling back to rule-based selection.");
        }
    }
    
    /// <summary>
    /// Select best skill to execute given current perception state
    /// Target: <16ms execution time
    /// </summary>
    public async Task<Skill?> SelectSkillAsync(PerceptionState state, Goal? currentGoal = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Analyze context
            var context = _contextAnalyzer.AnalyzeContext(state);
            
            // Check cache first
            var cacheKey = _cache.GenerateCacheKey(context, currentGoal);
            if (_cache.TryGetCachedSkill(cacheKey, out var cachedSkill))
            {
                stopwatch.Stop();
                return cachedSkill;
            }
            
            // Get candidate skills
            var candidates = GetCandidateSkills(context, currentGoal);
            
            if (candidates.Count == 0)
            {
                Console.WriteLine("No candidate skills available");
                return null;
            }
            
            // Fast path: if only one candidate, return it
            if (candidates.Count == 1)
            {
                var skill = candidates[0];
                _cache.CacheSkill(cacheKey, skill);
                stopwatch.Stop();
                return skill;
            }
            
            // Select best skill using LLM or fallback
            Skill? selected;
            if (_model != null && _context != null)
            {
                selected = await SelectWithLlmAsync(candidates, context, currentGoal);
            }
            else
            {
                selected = SelectWithRules(candidates, context, currentGoal);
            }
            
            // Cache result
            if (selected != null)
            {
                _cache.CacheSkill(cacheKey, selected);
            }
            
            stopwatch.Stop();
            Console.WriteLine($"Skill selection: {selected?.Name ?? "none"} in {stopwatch.ElapsedMilliseconds}ms");
            
            // Log warning if too slow
            if (stopwatch.ElapsedMilliseconds > 16)
            {
                Console.WriteLine($"WARNING: Skill selection took {stopwatch.ElapsedMilliseconds}ms (target: <16ms)");
            }
            
            return selected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skill selection error: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get candidate skills that might be relevant given the context
    /// Uses tags, ELO ranking, and dependencies
    /// </summary>
    private List<Skill> GetCandidateSkills(ContextInfo context, Goal? currentGoal)
    {
        var allSkills = _skillLibrary.GetAllSkills();
        
        // Filter by tags (if context has tags)
        var candidates = new List<Skill>();
        
        foreach (var skill in allSkills)
        {
            // Program scoping: allow generic skills or those tagged for current program
            if (!IsSkillAllowedForFocusedProgram(skill, context))
            {
                continue;
            }
            var relevance = CalculateRelevance(skill, context, currentGoal);
            if (relevance > 0)
            {
                candidates.Add(skill);
            }
        }
        
        // Sort by ELO rating (higher is better) and take top N
        candidates = candidates
            .OrderByDescending(s => s.EloRating)
            .Take(_maxSkillsToConsider)
            .ToList();
        
        return candidates;
    }

    private static bool IsSkillAllowedForFocusedProgram(Skill skill, ContextInfo context)
    {
        // Focused program is encoded into ContextInfo tags by ContextAnalyzer; it can also be passed via PerceptionState
        // Convention: program:<id>
        var programTag = context.Tags.FirstOrDefault(t => t.StartsWith("program:", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(programTag))
        {
            // No program focus → allow generic
            return true;
        }
        var programId = programTag.Substring("program:".Length);
        // A skill is allowed if:
        // - It has tag program:<programId>
        // - Or it is generic: has any of tags [vision, input, generic]
        bool generic = skill.Tags.Any(t =>
            t.Equals("vision", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("input", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("generic", StringComparison.OrdinalIgnoreCase));
        if (generic) return true;
        return skill.Tags.Any(t => t.Equals($"program:{programId}", StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Calculate relevance score for a skill given context
    /// </summary>
    private double CalculateRelevance(Skill skill, ContextInfo context, Goal? currentGoal)
    {
        double score = 1.0; // Base score
        
        // Check tag overlap
        if (skill.Tags.Any())
        {
            var tagOverlap = skill.Tags.Intersect(context.Tags, StringComparer.OrdinalIgnoreCase).Count();
            if (tagOverlap > 0)
            {
                score += tagOverlap * 2.0; // Boost for matching tags
            }
            else if (context.Tags.Any())
            {
                score *= 0.3; // Penalize if context has tags but skill doesn't match
            }
        }
        
        // Check goal alignment
        if (currentGoal != null && !string.IsNullOrEmpty(skill.Name))
        {
            var goalText = currentGoal.Description.ToLower();
            var skillName = skill.Name.ToLower();
            
            // Simple keyword matching
            if (goalText.Contains(skillName) || skillName.Contains(goalText.Split(' ')[0]))
            {
                score += 5.0; // Strong boost for goal alignment
            }
        }
        
        // Penalize if dependencies aren't met
        if (skill.Dependencies.Any())
        {
            // Check if dependencies are satisfied
            foreach (var depId in skill.Dependencies)
            {
                var dep = _skillLibrary.GetSkillById(depId);
                if (dep == null)
                {
                    score *= 0.1; // Heavy penalty for missing dependencies
                }
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Use local LLM to select best skill
    /// </summary>
    private async Task<Skill?> SelectWithLlmAsync(List<Skill> candidates, ContextInfo context, Goal? currentGoal)
    {
        if (_context == null) return null;
        
        // Build prompt
        var prompt = BuildSelectionPrompt(candidates, context, currentGoal);
        
        // Run inference with very low token budget for speed
        var executor = new InteractiveExecutor(_context);
        var inferenceParams = new InferenceParams
        {
            Temperature = 0.1f,  // Low temperature for consistency
            MaxTokens = 10,      // Just need skill name/number
            AntiPrompts = new[] { "\n", "." }
        };
        
        var response = "";
        await foreach (var text in executor.InferAsync(prompt, inferenceParams))
        {
            response += text;
        }
        
        // Parse response to get skill name or index
        var selected = ParseLlmResponse(response.Trim(), candidates);
        return selected;
    }
    
    /// <summary>
    /// Fallback rule-based selection when LLM is not available
    /// </summary>
    private Skill? SelectWithRules(List<Skill> candidates, ContextInfo context, Goal? currentGoal)
    {
        if (candidates.Count == 0) return null;
        
        // Simple heuristic: highest ELO with best relevance
        var scored = candidates
            .Select(s => new
            {
                Skill = s,
                Score = CalculateRelevance(s, context, currentGoal) * (1.0 + s.EloRating / 1000.0)
            })
            .OrderByDescending(x => x.Score)
            .ToList();
        
        return scored.First().Skill;
    }
    
    /// <summary>
    /// Build prompt for LLM skill selection
    /// Keep it minimal for speed
    /// </summary>
    private string BuildSelectionPrompt(List<Skill> candidates, ContextInfo context, Goal? currentGoal)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("Select the best skill:");
        sb.AppendLine($"Context: {_contextAnalyzer.GetContextDescription(context)}");
        
        if (currentGoal != null)
        {
            sb.AppendLine($"Goal: {currentGoal.Description}");
        }
        
        sb.AppendLine("\nAvailable skills:");
        for (int i = 0; i < candidates.Count && i < 10; i++) // Limit to 10 for speed
        {
            var skill = candidates[i];
            sb.AppendLine($"{i + 1}. {skill.Name} (ELO: {skill.EloRating:F0}) - {string.Join(", ", skill.Tags)}");
        }
        
        sb.AppendLine("\nBest skill number:");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Parse LLM response to extract skill selection
    /// </summary>
    private Skill? ParseLlmResponse(string response, List<Skill> candidates)
    {
        // Try to parse as number
        if (int.TryParse(response.Trim(), out int index))
        {
            if (index >= 1 && index <= candidates.Count)
            {
                return candidates[index - 1];
            }
        }
        
        // Try to match skill name
        var lowerResponse = response.ToLower();
        var match = candidates.FirstOrDefault(s => 
            lowerResponse.Contains(s.Name.ToLower()));
        
        if (match != null)
        {
            return match;
        }
        
        // Fallback: return first candidate (highest ELO)
        return candidates.FirstOrDefault();
    }
    
    /// <summary>
    /// Clear the selection cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    public void Dispose()
    {
        _context?.Dispose();
        _model?.Dispose();
    }
}

