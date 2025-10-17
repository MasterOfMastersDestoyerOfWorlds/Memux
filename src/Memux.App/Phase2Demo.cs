using Memux.Core.Models;
using Memux.Skills;
using Memux.Composer;
using Memux.Curriculum;
using Memux.CodeGen;

namespace Memux;

/// <summary>
/// Comprehensive demonstration of Phase 2: LLM Integration
/// Shows all components working together
/// </summary>
public class Phase2Demo
{
    public static async Task RunAsync(string apiKey, string dbPath = "phase2_demo.db")
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Phase 2: LLM Integration - Full Demonstration         ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Clean up old demo database
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine($"[Setup] Cleaned up old database: {dbPath}");
        }

        var skillLibrary = new SkillLibrary(dbPath);
        await skillLibrary.CreateSeedSkillsAsync();
        Console.WriteLine("[Setup] Initialized skill library with seed skills");
        Console.WriteLine();

        // ============================================================
        // Feature 1: ILlmClient Abstraction
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 1: LLM Client Abstraction (ILlmClient)                │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        Console.WriteLine("✓ Abstract interface allows swapping between OpenAI, Anthropic, local models");
        Console.WriteLine($"✓ Using OpenAI client with API key: {apiKey.Substring(0, 7)}...");
        Console.WriteLine();

        var composer = new ComposerAgent(apiKey, "gpt-4");
        
        // ============================================================
        // Feature 2: Skill Generation with Voyager-style Prompts
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 2: Skill Generation (ComposerAgent)                   │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("[Step 1] Generating skill: 'Attack when enemy is in range'...");
        try
        {
            var attackResult = await composer.GenerateSkillAsync(
                goalDescription: "Create a skill that attacks with the right hand weapon when an enemy is detected close to the player",
                availableSkills: await skillLibrary.GetTopSkillsAsync(5)
            );
            
            Console.WriteLine($"✓ Generated skill: {attackResult.SkillName}");
            Console.WriteLine($"  Tags: {string.Join(", ", attackResult.Tags)}");
            Console.WriteLine($"  Code length: {attackResult.SkillCode.Length} characters");
            Console.WriteLine();
            
            var attackSkill = await skillLibrary.AddSkillAsync(
                attackResult.SkillName,
                attackResult.SkillCode,
                attackResult.Tags
            );
            
            Console.WriteLine($"✓ Added to library with ID: {attackSkill.Id}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to generate skill: {ex.Message}");
            Console.WriteLine("  (This is expected if API key is invalid or quota exceeded)");
            Console.WriteLine();
        }

        // ============================================================
        // Feature 3: Template-based Skill Generation
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 3: Template Engine (SkillTemplateEngine)              │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        var templateEngine = new SkillTemplateEngine();
        
        Console.WriteLine("[Step 2] Generating directional dodge variations...");
        var dodgeVariations = templateEngine.GenerateVariations("DirectionalDodge");
        int addedCount = 0;
        
        foreach (var (name, code, tags) in dodgeVariations.Take(4))
        {
            await skillLibrary.AddSkillAsync(name, code, tags);
            Console.WriteLine($"✓ Generated: {name} ({string.Join(", ", tags)})");
            addedCount++;
        }
        Console.WriteLine($"✓ Added {addedCount} dodge variations to library");
        Console.WriteLine();

        // ============================================================
        // Feature 4: Meta-Skill Composition
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 4: Meta-Skill Composition                             │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("[Step 3] Composing meta-skill from top-performing skills...");
        var topSkills = await skillLibrary.GetTopSkillsAsync(3);
        
        Console.WriteLine($"  Component skills:");
        foreach (var skill in topSkills)
        {
            Console.WriteLine($"    - {skill.Name} (ELO: {skill.EloRating:F0})");
        }
        Console.WriteLine();

        try
        {
            var metaResult = await composer.ComposeMetaSkillAsync(
                metaSkillName: "DefensiveManeuver",
                componentSkills: topSkills,
                purpose: "Combine movement and dodge to avoid danger"
            );
            
            Console.WriteLine($"✓ Generated meta-skill: {metaResult.SkillName}");
            Console.WriteLine($"  Dependencies: {metaResult.Dependencies.Count} skills");
            Console.WriteLine($"  Tags: {string.Join(", ", metaResult.Tags)}");
            Console.WriteLine();
            
            await skillLibrary.AddSkillAsync(
                metaResult.SkillName,
                metaResult.SkillCode,
                metaResult.Tags,
                metaResult.Dependencies
            );
            
            Console.WriteLine($"✓ Meta-skill added to library");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to compose meta-skill: {ex.Message}");
            Console.WriteLine();
        }

        // ============================================================
        // Feature 5: Curriculum Agent with 10-Second Timer
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 5: Curriculum Agent (10-Second Re-evaluation)         │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        var curriculum = new CurriculumAgent(apiKey);
        
        // Subscribe to events
        int goalsCreated = 0;
        int goalsCompleted = 0;
        int goalsFailed = 0;
        
        curriculum.GoalCreated += (s, e) =>
        {
            goalsCreated++;
            Console.WriteLine($"  [Event] Goal Created: {e.Goal.Description}");
        };
        
        curriculum.GoalCompleted += (s, e) =>
        {
            goalsCompleted++;
            Console.WriteLine($"  [Event] Goal Completed: {e.Goal.Description}");
        };
        
        curriculum.GoalFailed += (s, e) =>
        {
            goalsFailed++;
            Console.WriteLine($"  [Event] Goal Failed: {e.Goal.Description} (Reason: {e.Reason})");
        };
        
        Console.WriteLine("[Step 4] Generating initial curriculum goals...");
        try
        {
            var goals = await curriculum.GenerateInitialGoalsAsync(
                "Dark Souls Remastered - Starting at Firelink Shrine, new character"
            );
            
            Console.WriteLine($"✓ Generated {goals.Count} progressive goals");
            Console.WriteLine();
            
            Console.WriteLine("  Goals:");
            for (int i = 0; i < goals.Count; i++)
            {
                Console.WriteLine($"    {i + 1}. {goals[i].Description} (Status: {goals[i].Status})");
            }
            Console.WriteLine();
            
            // Simulate progress
            Console.WriteLine("[Step 5] Simulating goal progress over 25 seconds...");
            Console.WriteLine("  (10-second timer will re-evaluate goals automatically)");
            Console.WriteLine();
            
            var currentGoal = curriculum.GetCurrentGoal();
            if (currentGoal != null)
            {
                Console.WriteLine($"  Current goal: {currentGoal.Description}");
                Console.WriteLine("  Simulating progress: 0%...");
                
                // Update progress over time
                for (int i = 1; i <= 5; i++)
                {
                    await Task.Delay(5000); // Wait 5 seconds
                    float progress = i * 0.2f;
                    curriculum.UpdateGoalProgress(currentGoal.Id, progress);
                    Console.WriteLine($"  Progress update: {progress * 100:F0}%");
                }
                
                curriculum.CompleteGoal(currentGoal.Id);
                Console.WriteLine($"  ✓ Goal completed!");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to generate curriculum: {ex.Message}");
            Console.WriteLine();
        }

        // ============================================================
        // Feature 6: Skill Debugging
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Feature 6: Skill Debugging (Error Feedback Loop)              │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        Console.WriteLine("[Step 6] Testing skill debugging with intentional error...");
        var failedSkill = await skillLibrary.GetTopSkillsAsync(1);
        
        if (failedSkill.Any())
        {
            Console.WriteLine($"  Simulating failure for: {failedSkill[0].Name}");
            
            try
            {
                var debugAdvice = await composer.DebugSkillAsync(
                    failedSkill[0],
                    errorMessage: "NullReferenceException: Object reference not set to an instance of an object",
                    executionContext: "Skill was executed with empty DetectedObjects list"
                );
                
                Console.WriteLine($"✓ Debug advice received ({debugAdvice.Length} characters)");
                Console.WriteLine("  First 200 chars of advice:");
                Console.WriteLine($"  {debugAdvice.Substring(0, Math.Min(200, debugAdvice.Length))}...");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Failed to get debug advice: {ex.Message}");
                Console.WriteLine();
            }
        }

        // ============================================================
        // Summary Statistics
        // ============================================================
        Console.WriteLine("┌────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Phase 2 Summary                                                │");
        Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
        
        var allSkills = await skillLibrary.GetTopSkillsAsync(100);
        
        Console.WriteLine($"✓ Total skills in library: {allSkills.Count}");
        Console.WriteLine($"✓ Average ELO rating: {allSkills.Average(s => s.EloRating):F2}");
        Console.WriteLine($"✓ Goals created: {goalsCreated}");
        Console.WriteLine($"✓ Goals completed: {goalsCompleted}");
        Console.WriteLine($"✓ Goals failed: {goalsFailed}");
        Console.WriteLine();
        
        Console.WriteLine("Phase 2 Components Demonstrated:");
        Console.WriteLine("  ✓ ILlmClient abstraction (OpenAI HTTP client)");
        Console.WriteLine("  ✓ ComposerAgent for skill generation");
        Console.WriteLine("  ✓ Voyager-style prompt templates");
        Console.WriteLine("  ✓ CurriculumAgent with 10-second re-evaluation");
        Console.WriteLine("  ✓ SkillTemplateEngine for deterministic generation");
        Console.WriteLine("  ✓ Meta-skill composition");
        Console.WriteLine("  ✓ Iterative debugging feedback loop");
        Console.WriteLine();
        
        // Cleanup
        curriculum.Dispose();
        
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            Phase 2: LLM Integration - COMPLETE ✓              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    }
}

