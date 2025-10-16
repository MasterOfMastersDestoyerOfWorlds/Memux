using Memux.Core.Models;
using Memux.Composer;

namespace Memux.Curriculum;

/// <summary>
/// Generates progressive goals and re-evaluates them every 10 seconds
/// Following Voyager's automatic curriculum approach
/// </summary>
public class CurriculumAgent
{
    private readonly ILlmClient _client;
    private readonly List<Goal> _goals = new();
    private readonly Timer _evaluationTimer;
    private readonly object _lock = new();
    
    public event EventHandler<GoalEventArgs>? GoalCreated;
    public event EventHandler<GoalEventArgs>? GoalCompleted;
    public event EventHandler<GoalEventArgs>? GoalFailed;
    
    public CurriculumAgent(string apiKey, string model = "gpt-4")
    {
        _client = new OpenAILlmClient(apiKey, model);
        
        // Setup 10-second evaluation timer
        _evaluationTimer = new Timer(EvaluateGoals, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }
    
    public CurriculumAgent(ILlmClient client)
    {
        _client = client;
        
        // Setup 10-second evaluation timer
        _evaluationTimer = new Timer(EvaluateGoals, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }
    
    /// <summary>
    /// Generate initial goals for a new session
    /// </summary>
    public async Task<List<Goal>> GenerateInitialGoalsAsync(string gameContext)
    {
        var systemPrompt = @"You are a curriculum designer for an autonomous game-playing agent.

Generate a list of progressive goals that start simple and gradually increase in difficulty.
Goals should be specific, measurable, and achievable.

Format your response as a JSON array of goals:
[
  {""description"": ""goal description"", ""difficulty"": 1},
  {""description"": ""goal description"", ""difficulty"": 2},
  ...
]";
        
        var userPrompt = $@"Generate initial goals for this game context: {gameContext}

Start with very simple goals (e.g., basic movement, observation) and progress to more complex ones.
Generate 5-7 goals.";
        
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        var content = await _client.CompleteChatAsync(messages, temperature: 0.7f, maxTokens: 1000);
        
        // Parse response and create goals
        var goals = ParseGoalsFromResponse(content);
        
        lock (_lock)
        {
            _goals.AddRange(goals);
        }
        
        foreach (var goal in goals)
        {
            GoalCreated?.Invoke(this, new GoalEventArgs { Goal = goal });
        }
        
        return goals;
    }
    
    /// <summary>
    /// Add a new goal to the curriculum
    /// </summary>
    public void AddGoal(Goal goal)
    {
        lock (_lock)
        {
            _goals.Add(goal);
        }
        GoalCreated?.Invoke(this, new GoalEventArgs { Goal = goal });
    }
    
    /// <summary>
    /// Get the current active goal
    /// </summary>
    public Goal? GetCurrentGoal()
    {
        lock (_lock)
        {
            return _goals.FirstOrDefault(g => g.Status == GoalStatus.InProgress) ??
                   _goals.FirstOrDefault(g => g.Status == GoalStatus.Pending);
        }
    }
    
    /// <summary>
    /// Mark goal as completed
    /// </summary>
    public void CompleteGoal(string goalId, float progress = 1.0f)
    {
        Goal? goal;
        lock (_lock)
        {
            goal = _goals.FirstOrDefault(g => g.Id == goalId);
            if (goal != null)
            {
                goal.Status = GoalStatus.Completed;
                goal.CompletedAt = DateTime.UtcNow;
                goal.Progress = progress;
            }
        }
        
        if (goal != null)
        {
            GoalCompleted?.Invoke(this, new GoalEventArgs { Goal = goal });
        }
    }
    
    /// <summary>
    /// Update goal progress
    /// </summary>
    public void UpdateGoalProgress(string goalId, float progress)
    {
        lock (_lock)
        {
            var goal = _goals.FirstOrDefault(g => g.Id == goalId);
            if (goal != null)
            {
                goal.Progress = Math.Clamp(progress, 0.0f, 1.0f);
                goal.LastEvaluatedAt = DateTime.UtcNow;
                
                if (goal.Status == GoalStatus.Pending && progress > 0)
                {
                    goal.Status = GoalStatus.InProgress;
                    goal.StartedAt = DateTime.UtcNow;
                }
            }
        }
    }
    
    /// <summary>
    /// Re-evaluate all goals (called every 10 seconds)
    /// </summary>
    private void EvaluateGoals(object? state)
    {
        lock (_lock)
        {
            foreach (var goal in _goals.Where(g => g.Status == GoalStatus.InProgress))
            {
                goal.LastEvaluatedAt = DateTime.UtcNow;
                
                // Check if goal has been stuck for too long
                if (goal.StartedAt.HasValue && 
                    (DateTime.UtcNow - goal.StartedAt.Value).TotalMinutes > 5 &&
                    goal.Progress < 0.1f)
                {
                    // Goal might be too difficult or impossible
                    goal.Status = GoalStatus.Obsolete;
                    GoalFailed?.Invoke(this, new GoalEventArgs 
                    { 
                        Goal = goal,
                        Reason = "No progress after 5 minutes"
                    });
                }
            }
        }
    }
    
    private List<Goal> ParseGoalsFromResponse(string response)
    {
        var goals = new List<Goal>();
        
        // Simple parsing - look for descriptions
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("description") && trimmed.Contains(":"))
            {
                // Extract description from JSON-like format
                var start = trimmed.IndexOf(":") + 1;
                var end = trimmed.IndexOf(",");
                if (end < 0) end = trimmed.Length;
                
                var description = trimmed.Substring(start, end - start)
                    .Trim()
                    .Trim('"', ',', ' ');
                
                if (!string.IsNullOrEmpty(description))
                {
                    goals.Add(new Goal
                    {
                        Description = description,
                        Status = GoalStatus.Pending
                    });
                }
            }
        }
        
        return goals;
    }
    
    public void Dispose()
    {
        _evaluationTimer?.Dispose();
    }
}

public class GoalEventArgs : EventArgs
{
    public Goal Goal { get; set; } = null!;
    public string? Reason { get; set; }
}

