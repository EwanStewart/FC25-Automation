using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Automation.Trading;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools;

namespace Automation.Setup;

public sealed record Capture(DateTime Time, CaptureKind Kind, string Method, string Url, int Status, string Body);

public sealed class NetworkObserver
{
    private const int BUFFER_SIZE = 200;
    private const string NETWORK_DOMAIN = "Network";

    private readonly ConcurrentDictionary<string, (CaptureKind kind, string method, string url, int status)> _requests = new();
    private readonly ConcurrentQueue<Capture> _captures = new();
    private DevToolsSession? _session;

    public bool Enabled => _session != null;

    public void Start(ChromeDriver driver)
    {
        try
        {
            _session = driver.GetDevToolsSession();
            _session.DevToolsEventReceived += OnEvent;
            _session.SendCommand("Network.enable", new JsonObject()).GetAwaiter().GetResult();
            Console.WriteLine("Network observer started.");
        }
        catch (Exception exception)
        {
            _session = null;
            Console.WriteLine($"Network observer unavailable: {exception.Message}");
        }
    }

    public IReadOnlyList<Capture> Since(DateTime time, CaptureKind kind)
    {
        return _captures.Where(capture => capture.Time >= time && capture.Kind == kind).ToList();
    }

    private void OnEvent(object? sender, DevToolsEventReceivedEventArgs arguments)
    {
        if (arguments.DomainName == NETWORK_DOMAIN && arguments.EventName == "requestWillBeSent")
            RecordRequest(arguments.EventData);
        else if (arguments.DomainName == NETWORK_DOMAIN && arguments.EventName == "responseReceived")
            RecordStatus(arguments.EventData);
        else if (arguments.DomainName == NETWORK_DOMAIN && arguments.EventName == "loadingFinished")
            FetchBody(arguments.EventData);
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
            var response = _session?.SendCommand("Network.getResponseBody", new JsonObject { ["requestId"] = id })
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
