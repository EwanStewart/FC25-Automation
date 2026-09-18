using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Automation.Trading;

namespace Automation.Setup;

public sealed record Capture(DateTime Time, CaptureKind Kind, string Method, string Url, int Status, string Body);

public sealed class NetworkObserver : IDisposable
{
    private const int BUFFER_SIZE = 200;
    private const string PAGE_URL_FRAGMENT = "ea.com";

    private readonly ConcurrentDictionary<string, (CaptureKind kind, string method, string url, int status)> _requests = new();
    private readonly ConcurrentQueue<Capture> _captures = new();
    private CdpClient? _client;
    private int _events;

    public bool Enabled => _client != null;

    public void Start(string debuggerHttp)
    {
        var socketUrl = CdpClient.FindPageSocket(debuggerHttp, PAGE_URL_FRAGMENT);

        try
        {
            if (socketUrl == null) throw new InvalidOperationException("no web app page target");

            _client = new CdpClient();
            _client.EventReceived += OnEvent;
            _client.Connect(socketUrl);
            _client.SendAsync("Network.enable", new JsonObject()).GetAwaiter().GetResult();
            Console.WriteLine("Network observer started.");
        }
        catch (Exception exception)
        {
            _client?.Dispose();
            _client = null;
            Console.WriteLine($"Network observer unavailable: {exception.Message}");
        }
    }

    public string Summary()
    {
        var kinds = _captures.GroupBy(capture => capture.Kind).Select(group => $"{group.Key} {group.Count()}");

        return $"Network observer saw {_events} events and captured [{string.Join(", ", kinds)}].";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    public IReadOnlyList<Capture> Since(DateTime time, CaptureKind kind)
    {
        return _captures.Where(capture => capture.Time >= time && capture.Kind == kind).ToList();
    }

    private void OnEvent(string method, JsonNode data)
    {
        Interlocked.Increment(ref _events);

        if (method == "Network.requestWillBeSent") RecordRequest(data);
        else if (method == "Network.responseReceived") RecordStatus(data);
        else if (method == "Network.loadingFinished") FetchBody(data);
    }

    private void RecordRequest(JsonNode data)
    {
        var method = Text(data, "request", "method");
        var url = Text(data, "request", "url");
        var kind = UtasPayloads.Classify(method, url);

        if (kind != CaptureKind.Other) _requests[RequestId(data)] = (kind, method, url, 0);
    }

    private void RecordStatus(JsonNode data)
    {
        var id = RequestId(data);

        if (_requests.TryGetValue(id, out var request))
            _requests[id] = request with { status = data["response"]?["status"]?.GetValue<int>() ?? 0 };
    }

    private void FetchBody(JsonNode data)
    {
        var id = RequestId(data);

        if (_requests.TryRemove(id, out var request)) Task.Run(() => StoreBody(id, request));
    }

    private void StoreBody(string id, (CaptureKind kind, string method, string url, int status) request)
    {
        try
        {
            var response = _client?.SendAsync("Network.getResponseBody", new JsonObject { ["requestId"] = id })
                .GetAwaiter().GetResult();
            var body = response?["body"]?.GetValue<string>() ?? string.Empty;

            _captures.Enqueue(new Capture(DateTime.UtcNow, request.kind, request.method, request.url, request.status, body));
            while (_captures.Count > BUFFER_SIZE) _captures.TryDequeue(out _);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Network observer could not read a response body: {exception.Message}");
        }
    }

    private static string RequestId(JsonNode data)
    {
        return data["requestId"]?.GetValue<string>() ?? string.Empty;
    }

    private static string Text(JsonNode data, string parent, string name)
    {
        return data[parent]?[name]?.GetValue<string>() ?? string.Empty;
    }
}
