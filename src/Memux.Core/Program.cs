using Memux.Core;

namespace Memux;

/// <summary>
/// Main entry point for Memux
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Memux: General Skill Acquisition System ===");
        Console.WriteLine();
        
        // Parse command line arguments
        string? gamePath = null;
        string? apiKey = null;
        string dbPath = "memux.db";
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--game" && i + 1 < args.Length)
            {
                gamePath = args[i + 1];
            }
            else if (args[i] == "--api-key" && i + 1 < args.Length)
            {
                apiKey = args[i + 1];
            }
            else if (args[i] == "--db" && i + 1 < args.Length)
            {
                dbPath = args[i + 1];
            }
        }
        
        // Check for API key in environment if not provided
        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("WARNING: No OpenAI API key provided. Set OPENAI_API_KEY environment variable or use --api-key argument.");
            Console.WriteLine("LLM features will be disabled.");
        }
        
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine();
        
        // TODO: Initialize all components and start orchestrator
        Console.WriteLine("Memux is initializing...");
        Console.WriteLine("Press Ctrl+C to exit");
        
        // Keep running until interrupted
        var cancellationToken = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cancellationToken.Cancel();
        };
        
        try
        {
            cancellationToken.Token.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }
    }
}

