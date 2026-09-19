#pragma warning disable SKEXP0001, SKEXP0020, SKEXP0070
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Data.Sqlite; // <--- ADD THIS FOR SQLLITE CONNECTIONS

namespace AvaloniaAgent;

public class CodebaseAgentService
{
    private readonly Kernel _kernel;
    private readonly ISemanticTextMemory _memory;
    private const string MemoryCollectionName = "avalonia_source_code";
    private const string OllamaEndpoint = "http://192.168.1.50:11434"; // Replace with your actual Proxmox VM IP

    public CodebaseAgentService()
    {
        var builder = Kernel.CreateBuilder();

        // 1. Map Chat Completion via the explicit Ollama Extensions assembly
        builder.AddOllamaChatCompletion(
            modelId: "llama3.1",
            endpoint: new Uri(OllamaEndpoint)
        );
        _kernel = builder.Build();

        // 2. Setup the concrete local text embedding structure
        var embeddingGenerator = new OllamaTextEmbeddingGenerationService(
            modelId: "nomic-embed-text",
            endpoint: new Uri(OllamaEndpoint)
        );

        // 3. Establish local SQLite target vector database connection using a native client
        var dbConnection = new SqliteConnection("Data Source=codebase.db;");
        dbConnection.Open(); // Open connection to initialize local storage engine file

        var sqliteStore = new SqliteMemoryStore(dbConnection);

        // 4. Combine elements securely inside the pipeline configuration cache
        _memory = new SemanticTextMemory(sqliteStore, embeddingGenerator);
    }

    public async Task IndexCodebaseAsync(string projectDirectoryPath)
    {
        if (!Directory.Exists(projectDirectoryPath)) return;

        var cSharpFiles = Directory.GetFiles(projectDirectoryPath, "*.cs", SearchOption.AllDirectories);

        foreach (var filePath in cSharpFiles)
        {
            string fileContent = await File.ReadAllTextAsync(filePath);
            string fileName = Path.GetFileName(filePath);

            await _memory.SaveInformationAsync(
                collection: MemoryCollectionName,
                text: $"File: {fileName}\nContent:\n{fileContent}",
                id: filePath,
                description: $"Source code for {fileName}"
            );
        }
    }

    public async Task<string> AskAgentAboutCodeAsync(string userQuestion)
    {
        var searchResults = _memory.SearchAsync(MemoryCollectionName, userQuestion, limit: 3);

        StringBuilder contextBuilder = new();
        await foreach (var result in searchResults)
        {
            contextBuilder.AppendLine(result.Metadata.Text);
            contextBuilder.AppendLine("---");
        }

        string fullPrompt = $@"
You are an expert software engineer reviewing the user's local project codebase. 
Use the following source code context to accurately answer the question.

[SOURCE CODE CONTEXT]
{contextBuilder}

[QUESTION]
{userQuestion}
";

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatService.GetChatMessageContentAsync(new ChatHistory(fullPrompt));

        return response.Content ?? "No response generated.";
    }
}
