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

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            
            // Try to bind to all interfaces for remote access (requires admin or URL reservation)
            try
            {
                _listener.Prefixes.Add($"http://*:{port}/");
            }
            catch (Exception ex)
            {
                GD.Print($"[STS2 Advisor] Could not bind to all interfaces: {ex.Message}");
                GD.Print($"[STS2 Advisor] Remote access unavailable. Run as admin or: netsh http add urlacl url=http://*:{port}/ user=Everyone");
            }
            
            _listener.Start();

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "STS2_Advisor_Server"
            };
            _serverThread.Start();

            GD.Print($"[STS2 Advisor] v{Version} started on http://localhost:{port}/");
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
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // Only allow GET requests - this is a read-only API
            if (request.HttpMethod != "GET")
            {
                SendError(response, 405, "Method not allowed. This is a read-only API.");
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
                            "GET /map - Map state and options"
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
