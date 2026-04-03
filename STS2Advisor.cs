using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace STS2Advisor;

/// <summary>
/// Lightweight read-only mod for Slay the Spire 2 that exposes game state
/// via a simple HTTP API. Designed for AI advisors to read current state
/// without taking control of gameplay.
/// </summary>
[ModInitializer("Initialize")]
public static partial class Advisor
{
    public const string Version = "1.0.0";
    public const int DefaultPort = 15526;
    private const string ConfigFileName = "STS2Advisor.conf";

    private static HttpListener? _listener;
    private static Thread? _serverThread;
    private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    
    // Pending advice responses from OpenClaw
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAdvice = new();
    private static readonly ConcurrentDictionary<string, string> _adviceResponses = new();
    
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static int LoadPort()
    {
        try
        {
            string? modDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (modDir == null) return DefaultPort;

            string configPath = Path.Combine(modDir, ConfigFileName);
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Dictionary<string, object> { ["port"] = DefaultPort };
                string json = JsonSerializer.Serialize(defaultConfig, JsonOptions);
                File.WriteAllText(configPath, json);
                GD.Print($"[STS2 Advisor] Created default config at {configPath}");
                return DefaultPort;
            }

            string content = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("port", out var portElem)
                && portElem.TryGetInt32(out int port)
                && port is > 0 and <= 65535)
            {
                return port;
            }

            GD.PrintErr($"[STS2 Advisor] Invalid or missing 'port' in {configPath}, using default {DefaultPort}");
            return DefaultPort;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] Failed to load config: {ex.Message}, using default port {DefaultPort}");
            return DefaultPort;
        }
    }

    public static void Initialize()
    {
        try
        {
            // Apply Harmony patches if needed
            new Harmony("com.sts2advisor").PatchAll();

            // Connect to main thread for safe game state access
            var tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(ProcessMainThreadQueue));

            int port = LoadPort();

            // Try binding options in order of preference
            var prefixes = new[]
            {
                $"http://*:{port}/",           // All interfaces (requires admin/urlacl)
                $"http://127.0.0.1:{port}/",   // Loopback IP
                $"http://localhost:{port}/",   // Localhost name (fallback)
            };
            
            foreach (var prefix in prefixes)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add(prefix);
                    _listener.Start();
                    GD.Print($"[STS2 Advisor] Bound to {prefix}");
                    break;
                }
                catch (HttpListenerException ex)
                {
                    GD.Print($"[STS2 Advisor] Could not bind to {prefix}: {ex.Message}");
                    _listener = null;
                }
            }
            
            if (_listener == null)
            {
                throw new Exception("Could not bind to any address");
            }

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "STS2_Advisor_Server"
            };
            _serverThread.Start();

            // Create the overlay UI
            CreateOverlay();

            GD.Print($"[STS2 Advisor] v{Version} started on http://localhost:{port}/");
            GD.Print($"[STS2 Advisor] Hotkeys: F1=Card, F2=Shop, F3=Event, F4=Combat, F5=Hide");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] Failed to start: {ex}");
        }
    }

    private static void ProcessMainThreadQueue()
    {
        int processed = 0;
        while (_mainThreadQueue.TryDequeue(out var action) && processed < 10)
        {
            try { action(); }
            catch (Exception ex) { GD.PrintErr($"[STS2 Advisor] Main thread action error: {ex}"); }
            processed++;
        }
    }

    private static void CreateOverlay()
    {
        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var overlay = new AdvisorOverlay();
            overlay.Name = "STS2AdvisorOverlay";
            tree.Root.CallDeferred("add_child", overlay);
            GD.Print("[STS2 Advisor] Overlay created");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] Failed to create overlay: {ex}");
        }
    }

    internal static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        _mainThreadQueue.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private static void ServerLoop()
    {
        while (_listener?.IsListening == true)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            
            // CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // Allow GET for state queries, POST only for /advice callback
            if (request.HttpMethod != "GET" && request.HttpMethod != "POST")
            {
                SendError(response, 405, "Method not allowed.");
                return;
            }

            string path = request.Url?.AbsolutePath ?? "/";

            switch (path)
            {
                case "/":
                    SendJson(response, new { 
                        message = $"STS2 Advisor v{Version} - Read-only game state API",
                        status = "ok",
                        endpoints = new[] {
                            "GET /state - Full game state",
                            "GET /combat - Combat state (hand, enemies, energy)",
                            "GET /deck - Current deck",
                            "GET /shop - Shop inventory (when in shop)",
                            "GET /card-reward - Card reward options (when picking cards)",
                            "GET /relics - Current relics",
                            "GET /map - Map state and options",
                            "GET /event - Event choices (when in event)"
                        }
                    });
                    break;
                    
                case "/state":
                    HandleGetState(request, response);
                    break;
                    
                case "/combat":
                    HandleGetCombat(response);
                    break;
                    
                case "/deck":
                    HandleGetDeck(response);
                    break;
                    
                case "/shop":
                    HandleGetShop(response);
                    break;
                    
                case "/card-reward":
                    HandleGetCardReward(response);
                    break;
                    
                case "/relics":
                    HandleGetRelics(response);
                    break;
                    
                case "/map":
                    HandleGetMap(response);
                    break;
                    
                case "/event":
                    HandleGetEvent(response);
                    break;
                
                case "/advice":
                    if (request.HttpMethod == "POST")
                        HandlePostAdvice(request, response);
                    else if (request.HttpMethod == "GET")
                        HandleGetAdvice(request, response);
                    else
                        SendError(response, 405, "Use POST to submit advice, GET to poll for it");
                    break;
                    
                default:
                    SendError(response, 404, "Endpoint not found");
                    break;
            }
        }
        catch (Exception ex)
        {
            try { SendError(context.Response, 500, $"Internal error: {ex.Message}"); }
            catch { /* response may already be closed */ }
        }
    }

    private static void HandleGetState(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildFullState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Advisor] HandleGetState: {ex}");
            SendError(response, 500, $"Failed to read game state: {ex.Message}");
        }
    }

    private static void HandleGetCombat(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildCombatState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read combat state: {ex.Message}");
        }
    }

    private static void HandleGetDeck(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildDeckState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read deck state: {ex.Message}");
        }
    }

    private static void HandleGetShop(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildShopState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read shop state: {ex.Message}");
        }
    }

    private static void HandleGetCardReward(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildCardRewardState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read card reward state: {ex.Message}");
        }
    }

    private static void HandleGetRelics(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildRelicsState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read relics state: {ex.Message}");
        }
    }

    private static void HandleGetMap(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildMapState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read map state: {ex.Message}");
        }
    }

    private static void HandleGetEvent(HttpListenerResponse response)
    {
        try
        {
            var stateTask = RunOnMainThread(() => StateBuilder.BuildEventState());
            var state = stateTask.GetAwaiter().GetResult();
            SendJson(response, state);
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to read event state: {ex.Message}");
        }
    }

    // ==================== Advice Callback System ====================
    
    /// <summary>
    /// Register a pending advice request. Returns a request ID.
    /// The overlay calls this, then sends request to OpenClaw with the ID.
    /// OpenClaw POSTs back to /advice with the response.
    /// </summary>
    internal static string RegisterAdviceRequest()
    {
        var requestId = Guid.NewGuid().ToString("N")[..12];
        var tcs = new TaskCompletionSource<string>();
        _pendingAdvice[requestId] = tcs;
        
        // Auto-expire after 60 seconds
        Task.Delay(60000).ContinueWith(_ => {
            if (_pendingAdvice.TryRemove(requestId, out var expired))
            {
                expired.TrySetResult("[Request timed out - no response from OpenClaw]");
            }
        });
        
        return requestId;
    }
    
    /// <summary>
    /// Wait for advice response (called by overlay after sending to OpenClaw)
    /// </summary>
    internal static async Task<string> WaitForAdvice(string requestId, int timeoutMs = 30000)
    {
        if (_pendingAdvice.TryGetValue(requestId, out var tcs))
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == tcs.Task)
            {
                _pendingAdvice.TryRemove(requestId, out _);
                return await tcs.Task;
            }
            else
            {
                _pendingAdvice.TryRemove(requestId, out _);
                return "[Request timed out]";
            }
        }
        
        // Check if response already arrived
        if (_adviceResponses.TryRemove(requestId, out var cached))
        {
            return cached;
        }
        
        return "[Request not found]";
    }
    
    /// <summary>
    /// POST /advice - OpenClaw posts advice response here
    /// Body: { "request_id": "xxx", "advice": "..." }
    /// </summary>
    private static void HandlePostAdvice(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            
            var requestId = json.TryGetProperty("request_id", out var rid) ? rid.GetString() : null;
            var advice = json.TryGetProperty("advice", out var adv) ? adv.GetString() : null;
            
            if (string.IsNullOrEmpty(requestId) || advice == null)
            {
                SendError(response, 400, "Missing request_id or advice");
                return;
            }
            
            // Complete the pending request if it exists
            if (_pendingAdvice.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(advice);
                GD.Print($"[STS2 Advisor] Received advice for request {requestId}");
            }
            else
            {
                // Store it in case the poll comes after
                _adviceResponses[requestId] = advice;
                GD.Print($"[STS2 Advisor] Cached advice for request {requestId} (no pending waiter)");
            }
            
            SendJson(response, new { status = "ok", request_id = requestId });
        }
        catch (Exception ex)
        {
            SendError(response, 500, $"Failed to process advice: {ex.Message}");
        }
    }
    
    /// <summary>
    /// GET /advice?request_id=xxx - Poll for advice (alternative to callback)
    /// </summary>
    private static void HandleGetAdvice(HttpListenerRequest request, HttpListenerResponse response)
    {
        var requestId = request.QueryString["request_id"];
        if (string.IsNullOrEmpty(requestId))
        {
            SendError(response, 400, "Missing request_id query parameter");
            return;
        }
        
        if (_adviceResponses.TryRemove(requestId, out var advice))
        {
            SendJson(response, new { status = "ready", request_id = requestId, advice });
        }
        else if (_pendingAdvice.ContainsKey(requestId))
        {
            SendJson(response, new { status = "pending", request_id = requestId });
        }
        else
        {
            SendJson(response, new { status = "not_found", request_id = requestId });
        }
    }

    internal static void SendJson(HttpListenerResponse response, object data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    internal static void SendError(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        SendJson(response, new Dictionary<string, object?> { ["error"] = message });
    }
}
