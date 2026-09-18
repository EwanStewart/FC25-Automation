using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Automation.Setup;

public sealed class CdpClient : IDisposable
{
    private const int RECEIVE_BUFFER_BYTES = 65536;

    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode?>> _pending = new();
    private readonly CancellationTokenSource _cancellation = new();
    private int _nextId;

    public event Action<string, JsonNode>? EventReceived;

    public static string? FindPageSocket(string debuggerHttp, string urlFragment)
    {
        string? result = null;

        try
        {
            using HttpClient client = new();
            var targets = JsonNode.Parse(client.GetStringAsync($"{debuggerHttp}/json").GetAwaiter().GetResult());
            var pages = targets?.AsArray().Where(target => target?["type"]?.GetValue<string>() == "page").ToList() ?? [];
            var page = pages.FirstOrDefault(target =>
                           (target?["url"]?.GetValue<string>() ?? string.Empty).Contains(urlFragment, StringComparison.Ordinal)) ??
                       pages.FirstOrDefault();

            result = page?["webSocketDebuggerUrl"]?.GetValue<string>();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Could not list DevTools targets: {exception.Message}");
        }

        return result;
    }

    public void Connect(string webSocketUrl)
    {
        _socket.ConnectAsync(new Uri(webSocketUrl), _cancellation.Token).GetAwaiter().GetResult();
        Task.Run(ReceiveLoop);
    }

    public Task<JsonNode?> SendAsync(string method, JsonObject parameters)
    {
        var id = Interlocked.Increment(ref _nextId);
        TaskCompletionSource<JsonNode?> completion = new();
        var message = new JsonObject { ["id"] = id, ["method"] = method, ["params"] = parameters }.ToJsonString();

        _pending[id] = completion;
        _socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, _cancellation.Token)
            .GetAwaiter().GetResult();

        return completion.Task;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _socket.Dispose();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[RECEIVE_BUFFER_BYTES];

        try
        {
            while (_socket.State == WebSocketState.Open && !_cancellation.IsCancellationRequested)
                Dispatch(await ReceiveMessage(buffer));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.WriteLine($"DevTools socket closed: {exception.Message}");
        }
    }

    private async Task<string> ReceiveMessage(byte[] buffer)
    {
        using MemoryStream stream = new();
        WebSocketReceiveResult result;

        do
        {
            result = await _socket.ReceiveAsync(buffer, _cancellation.Token);
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void Dispatch(string text)
    {
        var message = JsonNode.Parse(text);
        var id = message?["id"]?.GetValue<int>();
        var method = message?["method"]?.GetValue<string>();

        if (id.HasValue && _pending.TryRemove(id.Value, out var completion))
            completion.TrySetResult(message?["result"]);
        else if (method != null && message?["params"] is JsonNode parameters)
            EventReceived?.Invoke(method, parameters);
    }
}
