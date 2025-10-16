using Memux.Skills;

namespace Memux.CodeGen;

/// <summary>
/// Generates skill variations from templates
/// Following the "Three Times to Tool" principle from the code generation essay
/// </summary>
public class SkillTemplateEngine
{
    private readonly Dictionary<string, SkillTemplate> _templates = new();
    
    public SkillTemplateEngine()
    {
        RegisterBuiltInTemplates();
    }
    
    private void RegisterBuiltInTemplates()
    {
        // Dodge template - generates DodgeLeft, DodgeRight, etc.
        RegisterTemplate(new SkillTemplate
        {
            Name = "DirectionalDodge",
            CodeTemplate = @"
                queue.AddStickMovement(""LEFT"", {{X}}, {{Y}}, 100);
                queue.AddButtonPress(""B"", 50);
            ",
            Parameters = new()
            {
                { "X", new List<string> { "-1", "1", "0" } },
                { "Y", new List<string> { "0", "0", "-1", "1" } }
            },
            TagTemplate = new List<string> { "combat", "dodge", "{{Direction}}" },
            NameTemplate = "Dodge{{Direction}}"
        });
        
        // Movement template
        RegisterTemplate(new SkillTemplate
        {
            Name = "DirectionalMovement",
            CodeTemplate = @"
                queue.AddStickMovement(""LEFT"", {{X}}, {{Y}}, {{Duration}});
            ",
            Parameters = new()
            {
                { "X", new List<string> { "-1", "1", "0" } },
                { "Y", new List<string> { "0", "0", "-1", "1" } },
                { "Duration", new List<string> { "500", "1000", "2000" } }
            },
            TagTemplate = new List<string> { "movement", "navigation", "{{Direction}}" },
            NameTemplate = "Move{{Direction}}_{{Duration}}ms"
        });
        
        // Key press template
        RegisterTemplate(new SkillTemplate
        {
            Name = "KeyPress",
            CodeTemplate = @"
                queue.AddKeyPress(""{{Key}}"", {{Duration}});
            ",
            Parameters = new()
            {
                { "Key", new List<string> { "SPACE", "E", "R", "F", "ESC" } },
                { "Duration", new List<string> { "50", "100", "200" } }
            },
            TagTemplate = new List<string> { "input", "keyboard", "{{Key}}" },
            NameTemplate = "Press{{Key}}_{{Duration}}ms"
        });
    }
    
    public void RegisterTemplate(SkillTemplate template)
    {
        _templates[template.Name] = template;
    }
    
    /// <summary>
    /// Generate all variations of a template
    /// </summary>
    public List<(string name, string code, List<string> tags)> GenerateVariations(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var template))
        {
            throw new ArgumentException($"Template '{templateName}' not found");
        }
        
        var variations = new List<(string name, string code, List<string> tags)>();
        
        // Generate all combinations of parameters
        var parameterCombinations = GenerateParameterCombinations(template.Parameters);
        
        foreach (var combination in parameterCombinations)
        {
            string code = template.CodeTemplate;
            string name = template.NameTemplate;
            var tags = new List<string>(template.TagTemplate);
            
            // Substitute parameters
            foreach (var (param, value) in combination)
            {
                code = code.Replace($"{{{{{param}}}}}", value);
                name = name.Replace($"{{{{{param}}}}}", value);
                
                for (int i = 0; i < tags.Count; i++)
                {
                    tags[i] = tags[i].Replace($"{{{{{param}}}}}", value);
                }
            }
            
            variations.Add((name, code, tags));
        }
        
        return variations;
    }
    
    private List<Dictionary<string, string>> GenerateParameterCombinations(
        Dictionary<string, List<string>> parameters)
    {
        var combinations = new List<Dictionary<string, string>>();
        
        if (parameters.Count == 0)
        {
            return combinations;
        }
        
        // Start with first parameter
        var firstParam = parameters.First();
        foreach (var value in firstParam.Value)
        {
            combinations.Add(new Dictionary<string, string> { { firstParam.Key, value } });
        }
        
        // Add remaining parameters
        foreach (var param in parameters.Skip(1))
        {
            var newCombinations = new List<Dictionary<string, string>>();
            foreach (var existing in combinations)
            {
                foreach (var value in param.Value)
                {
                    var newCombo = new Dictionary<string, string>(existing)
                    {
                        { param.Key, value }
                    };
                    newCombinations.Add(newCombo);
                }
            }
            combinations = newCombinations;
        }
        
        return combinations;
    }
}

public class SkillTemplate
{
    public string Name { get; set; } = string.Empty;
    public string CodeTemplate { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Parameters { get; set; } = new();
    public List<string> TagTemplate { get; set; } = new();
    public string NameTemplate { get; set; } = string.Empty;
}

