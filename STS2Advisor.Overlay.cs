using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace STS2Advisor;

/// <summary>
/// In-game overlay for displaying AI advisor responses.
/// Hotkeys trigger advice requests, responses display on screen.
/// 
/// Configure via STS2Advisor.conf (same folder as DLL):
///   port=15526
///   openclaw_url=https://openclaw.tail0ddab.ts.net
///   openclaw_token=your-hook-token
/// </summary>
public partial class AdvisorOverlay : CanvasLayer
{
    private static AdvisorOverlay? _instance;
    public static AdvisorOverlay? Instance => _instance;

    private PanelContainer? _panel;
    private RichTextLabel? _label;
    private Button? _closeButton;
    private static readonly System.Net.Http.HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    
    // Loaded from config file or defaults
    private static string _openclawBaseUrl = "";
    private static string _openclawHookToken = "";
    private static bool _configLoaded = false;
    
    private bool _isVisible = false;
    private string _currentAdviceType = "";

    public override void _Ready()
    {
        _instance = this;
        Layer = 100; // On top of everything
        
        LoadConfig();
        CreateUI();
        Hide();
        
        GD.Print("[STS2 Advisor] Overlay ready. Hotkeys: F1=Card, F2=Shop, F3=Event, F4=Combat, F5=Hide");
        if (!string.IsNullOrEmpty(_openclawBaseUrl))
        {
            GD.Print($"[STS2 Advisor] OpenClaw configured: {_openclawBaseUrl}");
        }
        else
        {
            GD.Print("[STS2 Advisor] OpenClaw not configured - will show raw state. Add openclaw_url and openclaw_token to STS2Advisor.conf");
        }
    }

    private static void LoadConfig()
    {
        if (_configLoaded) return;
        _configLoaded = true;
        
        try
        {
            string? modDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (modDir == null) return;

            string configPath = Path.Combine(modDir, "STS2Advisor.conf");
            if (!File.Exists(configPath)) return;

            foreach (var line in File.ReadAllLines(configPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2) continue;
                
                var key = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();
                
                switch (key)
                {
                    case "openclaw_url":
                        _openclawBaseUrl = value;
                        break;
                    case "openclaw_token":
                        _openclawHookToken = value;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] Failed to load overlay config: {ex.Message}");
        }
    }

    private void CreateUI()
    {
        // Main panel
        _panel = new PanelContainer();
        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(600, 400);
        
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
        scroll.CustomMinimumSize = new Vector2(0, 300);
        
        _label = new RichTextLabel();
        _label.BbcodeEnabled = true;
        _label.FitContent = true;
        _label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _label.Text = "Press a hotkey to get advice:\n\n[b]F1[/b] - Card Reward\n[b]F2[/b] - Shop\n[b]F3[/b] - Event\n[b]F4[/b] - Combat\n[b]F5[/b] - Hide";
        
        scroll.AddChild(_label);
        vbox.AddChild(scroll);
        
        // Hotkey hints at bottom
        var hints = new Label();
        hints.Text = "F1:Card | F2:Shop | F3:Event | F4:Combat | F5/ESC:Hide";
        hints.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        hints.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(hints);
        
        _panel.AddChild(vbox);
        AddChild(_panel);
        
        // Center the panel
        _panel.Position = new Vector2(
            (GetViewport().GetVisibleRect().Size.X - _panel.CustomMinimumSize.X) / 2,
            (GetViewport().GetVisibleRect().Size.Y - _panel.CustomMinimumSize.Y) / 2
        );
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
                case Key.F2:
                    RequestAdvice("shop");
                    break;
                case Key.F3:
                    RequestAdvice("event");
                    break;
                case Key.F4:
                    RequestAdvice("combat");
                    break;
                case Key.F5:
                case Key.Escape when _isVisible:
                    HideOverlay();
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
        Show();
    }

    public void HideOverlay()
    {
        _isVisible = false;
        Hide();
    }

    private async void RequestAdvice(string adviceType)
    {
        _currentAdviceType = adviceType;
        ShowOverlay();
        SetText($"[i]Fetching {adviceType} advice...[/i]");

        try
        {
            // Get the current game state from local API
            string endpoint = adviceType switch
            {
                "card" => "/card-reward",
                "shop" => "/shop",
                "event" => "/event",
                "combat" => "/combat",
                _ => "/state"
            };

            var stateResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}{endpoint}");
            var deckResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/deck");
            var relicsResponse = await _httpClient.GetStringAsync($"http://localhost:{Advisor.DefaultPort}/relics");
            
            // Build the prompt for OpenClaw
            string prompt = adviceType switch
            {
                "card" => $"STS2 Card Reward - give me a quick recommendation.\n\nCard choices:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nGive a brief recommendation (2-3 sentences max). Format: recommend one card or skip, with a one-line reason.",
                "shop" => $"STS2 Shop - what should I buy?\n\nShop items:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nGive a brief recommendation (2-3 sentences max). What's worth buying?",
                "event" => $"STS2 Event - which option should I choose?\n\nEvent:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nGive a brief recommendation (2-3 sentences max). Which option and why?",
                "combat" => $"STS2 Combat - what's my play?\n\nCombat state:\n{stateResponse}\n\nMy deck:\n{deckResponse}\n\nMy relics:\n{relicsResponse}\n\nGive a brief tactical suggestion (2-3 sentences max).",
                _ => $"STS2 Game State:\n{stateResponse}"
            };
            
            // Package for OpenClaw /hooks/agent endpoint
            var payload = new
            {
                message = prompt,
                name = $"STS2-{adviceType}",
                model = "anthropic/claude-sonnet-4-5",  // Use Sonnet for speed
                timeoutSeconds = 30
            };

            var json = JsonSerializer.Serialize(payload, Advisor.JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            // Check if OpenClaw is configured
            if (string.IsNullOrEmpty(_openclawBaseUrl) || string.IsNullOrEmpty(_openclawHookToken))
            {
                SetText("[color=yellow]OpenClaw not configured[/color]\n\nAdd to STS2Advisor.conf:\n  openclaw_url=https://your-openclaw-url\n  openclaw_token=your-hook-token\n\n[i]Showing raw state:[/i]\n\n" + FormatStateAsText(adviceType, stateResponse));
                return;
            }
            
            // Set up request with auth header
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_openclawBaseUrl}/hooks/agent");
            request.Headers.Add("Authorization", $"Bearer {_openclawHookToken}");
            request.Content = content;
            
            try
            {
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    
                    // Parse the OpenClaw response
                    try
                    {
                        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseText);
                        
                        // OpenClaw returns { output: "..." } or similar
                        if (responseJson.TryGetProperty("output", out var output))
                        {
                            SetText(output.GetString() ?? responseText);
                        }
                        else if (responseJson.TryGetProperty("result", out var result))
                        {
                            SetText(result.GetString() ?? responseText);
                        }
                        else if (responseJson.TryGetProperty("message", out var message))
                        {
                            SetText(message.GetString() ?? responseText);
                        }
                        else
                        {
                            SetText(responseText);
                        }
                    }
                    catch
                    {
                        SetText(responseText);
                    }
                }
                else
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    SetText($"[color=yellow]OpenClaw returned {response.StatusCode}[/color]\n\n{errorText}\n\n[i]Showing raw state:[/i]\n\n" + FormatStateAsText(adviceType, stateResponse));
                }
            }
            catch (HttpRequestException ex)
            {
                // No webhook configured or network error, show raw state
                SetText($"[color=yellow]Could not reach OpenClaw[/color]\n{ex.Message}\n\n[i]Showing raw state:[/i]\n\n" + FormatStateAsText(adviceType, stateResponse));
            }
        }
        catch (Exception ex)
        {
            SetText($"[color=red]Error[/color]\n\n{ex.Message}");
        }
    }

    private string FormatStateAsText(string adviceType, string jsonState)
    {
        try
        {
            var state = JsonSerializer.Deserialize<JsonElement>(jsonState);
            var sb = new StringBuilder();
            
            sb.AppendLine($"[b]{adviceType.ToUpper()} STATE[/b]\n");
            
            if (adviceType == "card" && state.TryGetProperty("cards", out var cards))
            {
                sb.AppendLine("[b]Card Choices:[/b]\n");
                int i = 1;
                foreach (var card in cards.EnumerateArray())
                {
                    var name = card.GetProperty("name").GetString();
                    var type = card.GetProperty("type").GetString();
                    var cost = card.GetProperty("energy_cost").GetString();
                    var desc = card.GetProperty("description").GetString();
                    var rarity = card.GetProperty("rarity").GetString();
                    
                    sb.AppendLine($"{i}. [b]{name}[/b] ({rarity}) - {cost} energy");
                    sb.AppendLine($"   {type}: {desc}\n");
                    i++;
                }
                sb.AppendLine("\n[i]Webhook not configured - showing raw state.[/i]");
                sb.AppendLine("[i]Configure OPENCLAW_WEBHOOK_URL for AI advice.[/i]");
            }
            else if (adviceType == "shop" && state.TryGetProperty("items", out var items))
            {
                var gold = state.TryGetProperty("gold", out var g) ? g.GetInt32() : 0;
                sb.AppendLine($"[b]Gold:[/b] {gold}\n");
                sb.AppendLine("[b]Items:[/b]\n");
                foreach (var item in items.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();
                    var cost = item.GetProperty("cost").GetInt32();
                    var canAfford = item.TryGetProperty("can_afford", out var ca) && ca.GetBoolean();
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : type;
                    var afford = canAfford ? "[color=green]✓[/color]" : "[color=red]✗[/color]";
                    
                    sb.AppendLine($"{afford} {name} - {cost}g");
                }
            }
            else if (adviceType == "event" && state.TryGetProperty("options", out var options))
            {
                var eventName = state.TryGetProperty("event_name", out var en) ? en.GetString() : "Unknown";
                var isAncient = state.TryGetProperty("is_ancient", out var ia) && ia.GetBoolean();
                
                sb.AppendLine($"[b]Event:[/b] {eventName}");
                if (isAncient) sb.AppendLine("[color=purple](Ancient Boon)[/color]");
                sb.AppendLine();
                
                sb.AppendLine("[b]Options:[/b]\n");
                foreach (var opt in options.EnumerateArray())
                {
                    var title = opt.TryGetProperty("title", out var t) ? t.GetString() : "?";
                    var desc = opt.TryGetProperty("description", out var d) ? d.GetString() : "";
                    
                    sb.AppendLine($"• [b]{title}[/b]");
                    sb.AppendLine($"  {desc}\n");
                }
            }
            else if (adviceType == "combat")
            {
                var energy = state.TryGetProperty("energy", out var e) ? e.GetInt32() : 0;
                var maxEnergy = state.TryGetProperty("max_energy", out var me) ? me.GetInt32() : 0;
                var hp = state.TryGetProperty("hp", out var h) ? h.GetInt32() : 0;
                var maxHp = state.TryGetProperty("max_hp", out var mh) ? mh.GetInt32() : 0;
                
                sb.AppendLine($"[b]Combat[/b] | Energy: {energy}/{maxEnergy} | HP: {hp}/{maxHp}\n");
                
                if (state.TryGetProperty("enemies", out var enemies))
                {
                    sb.AppendLine("[b]Enemies:[/b]");
                    foreach (var enemy in enemies.EnumerateArray())
                    {
                        var name = enemy.TryGetProperty("name", out var n) ? n.GetString() : "?";
                        var ehp = enemy.TryGetProperty("hp", out var eh) ? eh.GetInt32() : 0;
                        var emhp = enemy.TryGetProperty("max_hp", out var emh) ? emh.GetInt32() : 0;
                        var intents = enemy.TryGetProperty("intents", out var ints) ? ints.ToString() : "?";
                        
                        sb.AppendLine($"• {name} ({ehp}/{emhp}) - Intent: {intents}");
                    }
                    sb.AppendLine();
                }
                
                if (state.TryGetProperty("hand", out var hand))
                {
                    sb.AppendLine("[b]Hand:[/b]");
                    foreach (var card in hand.EnumerateArray())
                    {
                        var name = card.TryGetProperty("name", out var n) ? n.GetString() : "?";
                        var cost = card.TryGetProperty("energy_cost", out var c) ? c.GetString() : "?";
                        var canPlay = card.TryGetProperty("can_play", out var cp) && cp.GetBoolean();
                        var playable = canPlay ? "" : " [color=red](unplayable)[/color]";
                        
                        sb.AppendLine($"• {name} ({cost}){playable}");
                    }
                }
            }
            else
            {
                // Generic JSON display
                sb.AppendLine(jsonState);
            }
            
            return sb.ToString();
        }
        catch
        {
            return jsonState;
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
