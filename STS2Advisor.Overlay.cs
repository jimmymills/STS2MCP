using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace STS2Advisor;

/// <summary>
/// Available AI model choices for the advisor.
/// </summary>
public enum AdvisorModel
{
    ClaudeOpus,
    ClaudeSonnet,
    OllamaGemma,
}

/// <summary>
/// In-game overlay for displaying AI advisor responses.
/// Hotkeys trigger advice requests via local Claude Code CLI.
/// Session context is maintained per run via --session-id.
///
/// Requires 'claude' CLI to be available on PATH.
/// </summary>
public partial class AdvisorOverlay : CanvasLayer
{
    private static AdvisorOverlay? _instance;
    public static AdvisorOverlay? Instance => _instance;

    private PanelContainer? _panel;
    private RichTextLabel? _label;
    private Button? _closeButton;
    private PanelContainer? _hotkeyHint; // Always-visible hotkey reminder
    private LineEdit? _chatInput; // Text input for follow-up messages
    private Button? _sendButton;
    private static readonly System.Net.Http.HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Model selection
    private static AdvisorModel _selectedModel = AdvisorModel.ClaudeOpus;
    private static bool _modelSelected = false;
    private PanelContainer? _modelSelectPanel;
    private bool _waitingForModelSelection = false;
    private string? _pendingAdviceType = null;

    // Session key for persistent context during a run
    private static string? _sessionKey = null;
    private static bool _sessionCreated = false;

    // Track the current run_id so we can detect new runs
    private static string? _currentRunId = null;

    private bool _isVisible = false;
    private string _currentAdviceType = "";

    public override void _Ready()
    {
        _instance = this;
        Layer = 100; // On top of everything

        CreateUI();
        _panel?.Hide();  // Start with advice panel hidden, but hotkey hint visible

        InstallSkillFiles();

        GD.Print("[STS2 Advisor] Overlay ready. Hotkeys: F1=Card, F2=Rest, F3=Event, F4=Combat, F5=Shop, F7=Hide, F8=Reset, F9=End Run");
        GD.Print("[STS2 Advisor] Using local Claude Code CLI for advice");
    }

    /// <summary>
    /// Copies the bundled skill files from the mod directory to ~/.claude/skills/
    /// so that the Claude Code CLI can use them.
    /// </summary>
    private static void InstallSkillFiles()
    {
        try
        {
            string? modDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (modDir == null) return;

            string sourceSkillDir = Path.Combine(modDir, "skills", "slay-the-spire-2");
            if (!Directory.Exists(sourceSkillDir))
            {
                GD.PrintErr($"[STS2 Advisor] Skill files not found at {sourceSkillDir}");
                return;
            }

            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string destSkillDir = Path.Combine(home, ".claude", "skills", "slay-the-spire-2");

            // Copy all skill files, overwriting to keep them up to date
            foreach (string sourceFile in Directory.GetFiles(sourceSkillDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceSkillDir, sourceFile);
                string destFile = Path.Combine(destSkillDir, relativePath);

                string? destDir = Path.GetDirectoryName(destFile);
                if (destDir != null) Directory.CreateDirectory(destDir);

                File.Copy(sourceFile, destFile, overwrite: true);
            }

            GD.Print($"[STS2 Advisor] Installed skill files to {destSkillDir}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] Failed to install skill files: {ex.Message}");
        }
    }

    private void CreateUI()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;

        // ========== Always-visible hotkey hint (top right) ==========
        _hotkeyHint = new PanelContainer();
        var hintStyle = new StyleBoxFlat();
        hintStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        hintStyle.SetCornerRadiusAll(5);
        hintStyle.SetContentMarginAll(8);
        _hotkeyHint.AddThemeStyleboxOverride("panel", hintStyle);

        var hintLabel = new Label();
        hintLabel.Text = "🎮 F1:Card | F6:Rest | F3:Event | F4:Combat | F5:Shop | F7:Hide | F8:Reset | F9:End Run";
        hintLabel.AddThemeFontSizeOverride("font_size", 14);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
        _hotkeyHint.AddChild(hintLabel);

        AddChild(_hotkeyHint);
        // Position in top right area, but left of menu buttons
        _hotkeyHint.Position = new Vector2(viewportSize.X - 900, 10);

        // ========== Main advice panel (moved up to avoid card selection) ==========
        _panel = new PanelContainer();
        _panel.CustomMinimumSize = new Vector2(600, 350);

        // Add a semi-transparent background style
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        styleBox.SetCornerRadiusAll(10);
        styleBox.SetBorderWidthAll(2);
        styleBox.BorderColor = new Color(0.4f, 0.6f, 0.9f, 1.0f);
        styleBox.SetContentMarginAll(15);
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Header with title and close button
        var header = new HBoxContainer();

        var title = new Label();
        title.Text = "🎮 STS2 Advisor";
        title.AddThemeFontSizeOverride("font_size", 24);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);

        _closeButton = new Button();
        _closeButton.Text = "✕";
        _closeButton.Pressed += OnClosePressed;
        header.AddChild(_closeButton);

        vbox.AddChild(header);

        // Separator
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // Scrollable content area
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(0, 250);

        _label = new RichTextLabel();
        _label.BbcodeEnabled = true;
        _label.FitContent = true;
        _label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _label.Text = "Press a hotkey to get advice:\n\n[b]F1[/b] - Card Reward\n[b]F3[/b] - Event\n[b]F4[/b] - Combat\n[b]F5[/b] - Shop\n[b]F7[/b] - Hide\n[b]F8[/b] - Reset Session";

        scroll.AddChild(_label);
        vbox.AddChild(scroll);

        // Chat input for follow-up messages
        var inputRow = new HBoxContainer();
        inputRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _chatInput = new LineEdit();
        _chatInput.PlaceholderText = "Type follow-up question or clarification...";
        _chatInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _chatInput.TextSubmitted += OnChatSubmitted;
        inputRow.AddChild(_chatInput);

        _sendButton = new Button();
        _sendButton.Text = "Send";
        _sendButton.Pressed += OnSendPressed;
        inputRow.AddChild(_sendButton);

        vbox.AddChild(inputRow);

        // Hotkey hints at bottom of panel
        var hints = new Label();
        hints.Text = "F1:Card | F6:Rest | F3:Event | F4:Combat | F5:Shop | F7:Hide | F8:Reset | F9:End Run | Enter:Send";
        hints.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        hints.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(hints);

        _panel.AddChild(vbox);
        AddChild(_panel);

        // Position panel in upper portion of screen (moved up from center)
        _panel.Position = new Vector2(
            (viewportSize.X - _panel.CustomMinimumSize.X) / 2,
            80  // Near top, below the hotkey hint
        );

        CreateModelSelectUI(viewportSize);
    }

    private void CreateModelSelectUI(Vector2 viewportSize)
    {
        _modelSelectPanel = new PanelContainer();
        _modelSelectPanel.CustomMinimumSize = new Vector2(420, 260);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.5f, 0.7f, 1.0f, 1.0f);
        style.SetContentMarginAll(20);
        _modelSelectPanel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);

        var title = new Label();
        title.Text = "Select AI Model";
        title.AddThemeFontSizeOverride("font_size", 22);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var sep = new HSeparator();
        vbox.AddChild(sep);

        var btnOpus = new Button();
        btnOpus.Text = "Claude Opus 4.6";
        btnOpus.CustomMinimumSize = new Vector2(0, 40);
        btnOpus.Pressed += () => OnModelSelected(AdvisorModel.ClaudeOpus);
        vbox.AddChild(btnOpus);

        var btnSonnet = new Button();
        btnSonnet.Text = "Claude Sonnet 4.6";
        btnSonnet.CustomMinimumSize = new Vector2(0, 40);
        btnSonnet.Pressed += () => OnModelSelected(AdvisorModel.ClaudeSonnet);
        vbox.AddChild(btnSonnet);

        var btnOllama = new Button();
        btnOllama.Text = "Ollama - gemma4:31b-cloud";
        btnOllama.CustomMinimumSize = new Vector2(0, 40);
        btnOllama.Pressed += () => OnModelSelected(AdvisorModel.OllamaGemma);
        vbox.AddChild(btnOllama);

        _modelSelectPanel.AddChild(vbox);
        AddChild(_modelSelectPanel);

        _modelSelectPanel.Position = new Vector2(
            (viewportSize.X - _modelSelectPanel.CustomMinimumSize.X) / 2,
            (viewportSize.Y - _modelSelectPanel.CustomMinimumSize.Y) / 2
        );

        _modelSelectPanel.Hide();
    }

    private void OnModelSelected(AdvisorModel model)
    {
        _selectedModel = model;
        _modelSelected = true;
        _waitingForModelSelection = false;
        _modelSelectPanel?.Hide();

        string modelName = model switch
        {
            AdvisorModel.ClaudeOpus => "Claude Opus 4.6",
            AdvisorModel.ClaudeSonnet => "Claude Sonnet 4.6",
            AdvisorModel.OllamaGemma => "Ollama gemma4:31b-cloud",
            _ => "Unknown"
        };
        GD.Print($"[STS2 Advisor] Model selected: {modelName}");

        // Resume the pending advice request if there is one
        if (_pendingAdviceType != null)
        {
            string adviceType = _pendingAdviceType;
            _pendingAdviceType = null;
            RequestAdvice(adviceType);
        }
    }

    private void ShowModelSelection(string pendingAdviceType)
    {
        _waitingForModelSelection = true;
        _pendingAdviceType = pendingAdviceType;
        _modelSelectPanel?.Show();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.F1:
                    RequestAdvice("card");
                    break;
                case Key.F6:
                    RequestAdvice("rest");
                    break;
                case Key.F3:
                    RequestAdvice("event");
                    break;
                case Key.F4:
                    RequestAdvice("combat");
                    break;
                case Key.F5:
                    RequestAdvice("shop");
                    break;
                case Key.F7:
                case Key.Escape when _isVisible:
                    HideOverlay();
                    break;
                case Key.F8:
                    ResetSession();
                    break;
                case Key.F9:
                    EndRunAndRecord();
                    break;
            }
        }
    }

    private void OnClosePressed()
    {
        HideOverlay();
    }

    public void ShowOverlay()
    {
        _isVisible = true;
        _panel?.Show();
    }

    public void HideOverlay()
    {
        _isVisible = false;
        _panel?.Hide();
    }

    public void ResetSession()
    {
        _sessionKey = null;
        _sessionCreated = false;
        _currentRunId = null;
        _modelSelected = false;
        GD.Print("[STS2 Advisor] Session reset - next request will start fresh context");
        ShowOverlay();
        SetText("[color=green]Session reset![/color]\n\nNext advice request will start with fresh context and model selection.\nThis happens automatically when you start a new run.");
    }

    private void OnChatSubmitted(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            SendFollowUp(text);
        }
    }

    private void OnSendPressed()
    {
        if (_chatInput != null && !string.IsNullOrWhiteSpace(_chatInput.Text))
        {
            SendFollowUp(_chatInput.Text);
        }
    }

    /// <summary>
    /// Invoke the selected AI model with the given prompt.
    /// Routes to Claude CLI or Ollama based on the current model selection.
    /// Ollama calls ignore sessionId — Ollama has no CLI session concept here.
    /// </summary>
    private static async Task<string> InvokeModel(string prompt, string? sessionId)
    {
        return _selectedModel switch
        {
            AdvisorModel.OllamaGemma => await InvokeOllama(prompt),
            _ => await InvokeClaude(prompt, sessionId),
        };
    }

    /// <summary>
    /// Invoke Claude Code CLI with the given prompt, using --session-id for context persistence.
    /// Returns the CLI's stdout as the response.
    /// </summary>
    private static async Task<string> InvokeClaude(string prompt, string? sessionId = null)
    {
        string modelId = _selectedModel switch
        {
            AdvisorModel.ClaudeSonnet => "claude-sonnet-4-6",
            _ => "claude-opus-4-6",
        };

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--dangerously-skip-permissions");
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(modelId);

        if (!string.IsNullOrEmpty(sessionId))
        {
            if (_sessionCreated)
            {
                // Resume an existing session
                psi.ArgumentList.Add("--resume");
                psi.ArgumentList.Add(sessionId);
            }
            else
            {
                // Create a new session with this ID
                psi.ArgumentList.Add("--session-id");
                psi.ArgumentList.Add(sessionId);
                _sessionCreated = true;
            }
        }

        psi.ArgumentList.Add(prompt);

        // Inherit PATH so claude CLI is found
        psi.Environment["PATH"] = System.Environment.GetEnvironmentVariable("PATH") ?? "/usr/local/bin:/usr/bin:/bin";

        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait with a timeout (120 seconds for complex analysis)
        var exited = await Task.Run(() => process.WaitForExit(120_000));
        if (!exited)
        {
            try { process.Kill(); } catch { }
            return "[Request timed out after 120 seconds]";
        }

        if (process.ExitCode != 0 && stdout.Length == 0)
        {
            var err = stderr.ToString().Trim();
            return $"[Claude CLI error (exit {process.ExitCode})]\n{err}";
        }

        return stdout.ToString().Trim();
    }

    /// <summary>
    /// Invoke Ollama with gemma4:31b-cloud model via its HTTP API.
    /// </summary>
    private static async Task<string> InvokeOllama(string prompt)
    {
        try
        {
            var requestBody = new
            {
                model = "gemma4:31b-cloud",
                prompt = prompt,
                stream = false,
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));
            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return $"[Ollama error (HTTP {(int)response.StatusCode})]\n{await response.Content.ReadAsStringAsync()}";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseDoc = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (responseDoc.TryGetProperty("response", out var responseProp))
            {
                return responseProp.GetString() ?? "[Empty response from Ollama]";
            }

            return "[Unexpected Ollama response format]";
        }
        catch (TaskCanceledException)
        {
            return "[Ollama request timed out after 120 seconds]";
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return $"[Ollama connection failed - is Ollama running?]\n{ex.Message}";
        }
    }

    private async void SendFollowUp(string message)
    {
        if (_chatInput != null)
        {
            _chatInput.Text = "";
        }

        SetText($"[i]Sending: {message}...[/i]");

        try
        {
            GD.Print($"[STS2 Advisor] Sending follow-up via {GetModelDisplayName()} (session: {_sessionKey})");
            var advice = await InvokeModel(message, _sessionKey);
            SetText(advice);
        }
        catch (Exception ex)
        {
            SetText($"[color=red]Error: {ex.Message}[/color]");
        }
    }

    private async void RequestAdvice(string adviceType)
    {
        // If waiting for model selection, ignore new requests
        if (_waitingForModelSelection) return;

        _currentAdviceType = adviceType;
        ShowOverlay();
        SetText($"[i]Fetching {adviceType} advice...[/i]");

        try
        {
            // Get run state to check if we're in a run
            var runStateResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/state");
            var runState = JsonSerializer.Deserialize<JsonElement>(runStateResponse);

            // Check if we're in a run
            if (runState.TryGetProperty("state", out var stateProp) && stateProp.GetString() == "menu")
            {
                SetText("[color=yellow]No run in progress.[/color]\n\nStart a run in the game, then press a hotkey for advice.");
                return;
            }

            // Detect new run by checking run_id
            string? runId = null;
            if (runState.TryGetProperty("run_id", out var runIdProp))
            {
                runId = runIdProp.GetString();
            }

            // If run_id changed, this is a new run - reset session
            if (!string.IsNullOrEmpty(runId) && runId != _currentRunId)
            {
                GD.Print($"[STS2 Advisor] New run detected (id: {runId}), resetting session");
                _currentRunId = runId;
                _sessionKey = null;
                _sessionCreated = false;
                _modelSelected = false;
            }

            // Prompt for model selection at the start of each new run
            if (!_modelSelected)
            {
                ShowModelSelection(adviceType);
                return;
            }

            // Generate session key if needed
            if (string.IsNullOrEmpty(_sessionKey))
            {
                _sessionKey = Guid.NewGuid().ToString();
            }

            // Get character for tier list context
            var character = runState.TryGetProperty("player", out var playerObj) && playerObj.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "Unknown" : "Unknown";

            // Get the current game state from local API
            string endpoint = adviceType switch
            {
                "card" => "/card-reward",
                "shop" => "/shop",
                "event" => "/event",
                "combat" => "/combat",
                "rest" => "/rest",
                _ => "/state"
            };

            var stateResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}{endpoint}");
            var deckResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/deck");
            var relicsResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/relics");

            // Build the prompt
            string prompt = adviceType switch
            {
                "card" => $"Slay the Spire 2 - Help me pick cards.\nCharacter: {character}\n\nCard choices:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nConsult the tier lists and reference data. Recommend one card or skip, with a one-line reason. Keep advice brief (2-3 sentences).",
                "shop" => $"Slay the Spire 2 - Shop advice.\nCharacter: {character}\n\nShop items:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nConsult the tier lists and reference data. What's worth buying? Keep advice brief (2-3 sentences).",
                "event" => $"Slay the Spire 2 - Event advice.\nCharacter: {character}\n\nEvent:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nConsult the tier lists and reference data. Which option and why? Keep advice brief (2-3 sentences).",
                "combat" => $"Slay the Spire 2 - Combat advice.\nCharacter: {character}\n\nCombat state:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nConsult the tier lists and reference data. Brief tactical suggestion (2-3 sentences).",
                "rest" => $"Slay the Spire 2 - Rest site advice.\nCharacter: {character}\nHP: {(runState.TryGetProperty("player", out var playerProp) && playerProp.TryGetProperty("hp", out var hpProp) ? hpProp.ToString() : "?")}/{(playerProp.TryGetProperty("max_hp", out var maxHpProp) ? maxHpProp.ToString() : "?")}\n\nRest site options:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nConsult the tier lists and reference data. Should I rest, upgrade, or use another option? Which card to upgrade if upgrading? Keep advice brief (2-3 sentences).",
                _ => $"Slay the Spire 2 - General advice.\nCharacter: {character}\n\nGame State:\n{stateResponse}\n\nConsult the tier lists and reference data. Keep advice brief."
            };

            GD.Print($"[STS2 Advisor] Sending {adviceType} request via {GetModelDisplayName()} (session: {_sessionKey})");
            SetText($"[i]Analyzing {adviceType} ({GetModelDisplayName()})...[/i]");

            var advice = await InvokeModel(prompt, _sessionKey);
            SetText(advice);
        }
        catch (Exception ex)
        {
            SetText($"[color=red]Error[/color]\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Returns the display name for the currently selected model.
    /// </summary>
    private static string GetModelDisplayName()
    {
        return _selectedModel switch
        {
            AdvisorModel.ClaudeOpus => "Opus 4.6",
            AdvisorModel.ClaudeSonnet => "Sonnet 4.6",
            AdvisorModel.OllamaGemma => "gemma4:31b-cloud",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// End the current run and record the results to run-notes.md.
    /// Triggered by F9. Fetches final game state and asks Claude to write a run autopsy.
    /// </summary>
    private async void EndRunAndRecord()
    {
        ShowOverlay();
        SetText("[i]Recording run results...[/i]");

        try
        {
            // Fetch final game state
            var stateResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/state");
            var deckResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/deck");
            var relicsResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/relics");

            var runState = JsonSerializer.Deserialize<JsonElement>(stateResponse);
            var character = runState.TryGetProperty("player", out var playerObj) && playerObj.TryGetProperty("character", out var charProp) ? charProp.GetString() ?? "Unknown" : "Unknown";

            // Find the run-notes.md path
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            string runNotesPath = Path.Combine(home, ".claude", "skills", "slay-the-spire-2", "references", "run-notes.md");

            string prompt = $"The current Slay the Spire 2 run has ended. Write a run autopsy and append it to the run notes file.\n\n" +
                $"Character: {character}\n" +
                $"Final game state:\n{stateResponse}\n\n" +
                $"Final deck:\n{deckResponse}\n\n" +
                $"Final relics:\n{relicsResponse}\n\n" +
                $"Instructions:\n" +
                $"1. Read the existing run notes at: {runNotesPath}\n" +
                $"2. Analyze the run - what went well, what went wrong, key decisions\n" +
                $"3. Append a new run autopsy section to {runNotesPath} using the format from the slay-the-spire-2 skill\n" +
                $"4. Include: character, archetype built, key cards/relics, what caused the win/loss, and actionable lessons\n" +
                $"5. Return the autopsy summary so I can display it\n\n" +
                $"Keep the autopsy concise but include specific, actionable lessons for future runs.";

            GD.Print("[STS2 Advisor] Recording run results...");
            SetText("[i]Analyzing run and recording lessons learned...[/i]");

            var advice = await InvokeModel(prompt, _sessionKey);
            SetText($"[color=green]Run Recorded![/color]\n\n{advice}");

            // Reset session after recording
            _currentRunId = null;
            _sessionKey = null;
            _sessionCreated = false;
            _modelSelected = false;
            GD.Print("[STS2 Advisor] Run recorded and session reset");
        }
        catch (Exception ex)
        {
            SetText($"[color=red]Failed to record run:[/color]\n\n{ex.Message}");
        }
    }

    public void SetText(string text)
    {
        if (_label != null)
        {
            // Convert markdown-ish formatting to BBCode
            text = text.Replace("**", "[b]").Replace("**", "[/b]");
            text = text.Replace("✅", "[color=green]✅[/color]");
            text = text.Replace("❌", "[color=red]❌[/color]");
            text = text.Replace("⭐", "[color=yellow]⭐[/color]");

            _label.Text = text;
        }
    }

    public override void _ExitTree()
    {
        _instance = null;
    }
}
