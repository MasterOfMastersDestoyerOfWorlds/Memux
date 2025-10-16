using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Memux.Core.Models;

namespace Memux.Skills;

/// <summary>
/// Compiles skill code strings into executable delegates using Roslyn
/// </summary>
public class SkillCompiler
{
    private readonly ScriptOptions _scriptOptions;
    
    public SkillCompiler()
    {
        // Setup script options with necessary references
        _scriptOptions = ScriptOptions.Default
            .AddReferences(
                typeof(PerceptionState).Assembly,    // Memux.Core
                typeof(ActionQueue).Assembly,        // Memux.Core
                typeof(Skill).Assembly               // Memux.Skills
            )
            .AddImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "Memux.Core.Models"
            );
    }
    
    /// <summary>
    /// Compile a skill code string into an executable delegate
    /// </summary>
    public async Task<Func<PerceptionState, ActionQueue>?> CompileAsync(string code)
    {
        try
        {
            // Wrap the code in a function signature if not already wrapped
            string wrappedCode = code;
            if (!code.Contains("PerceptionState"))
            {
                wrappedCode = $@"
                    (Memux.Core.Models.PerceptionState state) => 
                    {{
                        var queue = new Memux.Core.Models.ActionQueue();
                        {code}
                        return queue;
                    }}
                ";
            }
            
            // Compile the script
            var script = CSharpScript.Create<Func<PerceptionState, ActionQueue>>(
                wrappedCode,
                _scriptOptions);
            
            var compilation = script.GetCompilation();
            var diagnostics = compilation.GetDiagnostics();
            
            // Check for errors
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errors.Any())
            {
                var errorMessages = string.Join("\n", errors.Select(e => e.GetMessage()));
                throw new SkillCompilationException($"Compilation failed:\n{errorMessages}");
            }
            
            // Run the script to get the delegate
            var result = await script.RunAsync();
            return result.ReturnValue;
        }
        catch (CompilationErrorException ex)
        {
            throw new SkillCompilationException($"Compilation error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new SkillCompilationException($"Unexpected error during compilation: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Validate code syntax without executing
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(string code)
    {
        try
        {
            await CompileAsync(code);
            return new ValidationResult { IsValid = true };
        }
        catch (SkillCompilationException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

public class SkillCompilationException : Exception
{
    public SkillCompilationException(string message) : base(message) { }
    public SkillCompilationException(string message, Exception inner) : base(message, inner) { }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

