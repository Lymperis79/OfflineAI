using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System;
using System.Text;

namespace AvaloniaAgent;

public partial class MainWindow : Window
{
    private readonly CodebaseAgentService _codebaseService = new();
    private readonly StringBuilder _chatSessionHistory = new();

    public MainWindow()
    {
        InitializeComponent();
        _chatSessionHistory.AppendLine("# Local Developer AI Session Log\n---");
    }

    private async void ExecuteAgentTurn()
    {
        string text = InputBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        _chatSessionHistory.AppendLine(\$"\n\n### 👤 You:\n{text}");
        UpdateUiDisplay();
        InputBox.Text = string.Empty;

        try
        {
            string reply = await _codebaseService.AskAgentAboutCodeAsync(text);
            _chatSessionHistory.AppendLine(\$"\n\n### 🤖 Agent Response:\n{reply}");
        }
        catch (Exception ex)
        {
            _chatSessionHistory.AppendLine(\$"\n\n⚠️ **[System Error]**: Interface lost communication with local server. Context: {ex.Message}");
        }

        UpdateUiDisplay();
    }

    private void UpdateUiDisplay()
    {
        MarkdownViewer.Markdown = _chatSessionHistory.ToString();
    }

    private void OnSendClick(object sender, RoutedEventArgs e) => ExecuteAgentTurn();

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteAgentTurn();
        }
    }

    private async void OnScanCodebaseClick(object sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Local C# Project to Read",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string localPath = folders.Path.LocalPath;
            _chatSessionHistory.AppendLine(\$"\n\n⚙️ *[System Notice]*: Indexing C# source contents inside `{localPath}` into local vector cache...");
            UpdateUiDisplay();

            await _codebaseService.IndexCodebaseAsync(localPath);

            _chatSessionHistory.AppendLine("\n\n✅ *[System Notice]*: Codebase mapped successfully. The model can now utilize exact class schemas.");
            UpdateUiDisplay();
        }
    }
}
