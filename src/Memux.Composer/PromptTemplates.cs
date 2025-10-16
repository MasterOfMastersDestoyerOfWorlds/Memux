using Memux.Skills;

namespace Memux.Composer;

/// <summary>
/// System prompts following Voyager's iterative prompting pattern
/// </summary>
public class PromptTemplates
{
    public string GetSkillGenerationSystemPrompt()
    {
        return @"You are an expert programmer helping to create skills for an autonomous agent in a game environment.

Skills are C# functions that take a PerceptionState (what the agent sees) and return an ActionQueue (what the agent should do).

Available perception data:
- ScreenData: raw screen pixels
- DepthMap: depth estimation (0=near, 1=far)
- DetectedObjects: list of detected objects with bounding boxes
- OcrResults: text extracted from screen
- ContextHints: game-specific hints

Available actions:
- queue.AddKeyPress(key, durationMs) - keyboard input
- queue.AddButtonPress(button, durationMs) - controller button
- queue.AddStickMovement(stick, x, y, durationMs) - analog stick (-1 to 1)
- queue.AddWait(durationMs) - wait/delay
- queue.AddPerceptionCheck(condition, timeoutMs) - wait for condition

Your response must follow this format:
SKILL_NAME: <name>
TAGS: <tag1>, <tag2>, <tag3>
CODE:
```csharp
<code here>
```

Keep skills simple, focused, and composable. Use clear, descriptive names.";
    }
    
    public string GetSkillGenerationUserPrompt(
        string goalDescription,
        List<Skill> availableSkills,
        string? previousError)
    {
        var prompt = $@"Create a skill to accomplish this goal: {goalDescription}

Available skills you can reference or compose with:
{string.Join("\n", availableSkills.Take(10).Select(s => $"- {s.Name}: {string.Join(", ", s.Tags)}"))}";
        
        if (!string.IsNullOrEmpty(previousError))
        {
            prompt += $@"

Previous attempt failed with error: {previousError}
Please fix the error and try again.";
        }
        
        return prompt;
    }
    
    public string GetMetaSkillCompositionSystemPrompt()
    {
        return @"You are composing multiple existing skills into a higher-level meta-skill.

A meta-skill calls other skills in sequence to accomplish a complex goal.

In your code, you can call other skills by name (they will be injected as dependencies).

Your response must follow this format:
SKILL_NAME: <name>
TAGS: <tag1>, <tag2>, <tag3>
CODE:
```csharp
<code that calls component skills>
```";
    }
    
    public string GetMetaSkillCompositionUserPrompt(
        string metaSkillName,
        List<Skill> componentSkills,
        string purpose)
    {
        return $@"Create a meta-skill called '{metaSkillName}' with purpose: {purpose}

Component skills to compose:
{string.Join("\n", componentSkills.Select(s => $"- {s.Name} ({string.Join(", ", s.Tags)}): ELO {s.EloRating:F0}, {s.SuccessRate:P0} success"))}

The meta-skill should intelligently sequence these component skills to achieve the goal.";
    }
    
    public string GetSkillDebuggingSystemPrompt()
    {
        return @"You are debugging a failed skill execution.

Analyze the error and suggest a fixed version of the skill code.

Common issues:
- Incorrect perception state access
- Wrong action types or parameters
- Missing error handling
- Incorrect conditional logic

Provide the corrected code in this format:
```csharp
<corrected code>
```";
    }
    
    public string GetSkillDebuggingUserPrompt(
        Skill failedSkill,
        string errorMessage,
        string executionContext)
    {
        return $@"This skill failed:

Skill Name: {failedSkill.Name}
Tags: {string.Join(", ", failedSkill.Tags)}

Code:
```csharp
{failedSkill.Code}
```

Error: {errorMessage}

Execution Context: {executionContext}

Please provide the corrected code.";
    }
}

