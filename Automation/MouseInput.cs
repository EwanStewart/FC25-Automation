using System.Text.Json.Nodes;

namespace Automation.Setup;

public sealed class MouseInput : IDisposable
{
    private const string PAGE_URL_FRAGMENT = "ea.com";
    private const int PRESS_HOLD_MS = 60;

    private CdpClient? _client;

    public bool Enabled => _client != null;

    public void Start(string debuggerHttp)
    {
        var socketUrl = CdpClient.FindPageSocket(debuggerHttp, PAGE_URL_FRAGMENT);

        try
        {
            if (socketUrl == null) throw new InvalidOperationException("no web app page target");

            _client = new CdpClient();
            _client.Connect(socketUrl);
            Console.WriteLine("Mouse input started.");
        }
        catch (Exception exception)
        {
            _client?.Dispose();
            _client = null;
            Console.WriteLine($"Mouse input unavailable: {exception.Message}");
        }
    }

    public bool ClickAt(double x, double y)
    {
        var clicked = false;

        try
        {
            Dispatch("mouseMoved", x, y, "none", 0);
            Dispatch("mousePressed", x, y, "left", 1);
            Thread.Sleep(PRESS_HOLD_MS);
            Dispatch("mouseReleased", x, y, "left", 0);
            clicked = _client != null;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Mouse input could not click: {exception.Message}");
        }

        return clicked;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private void Dispatch(string type, double x, double y, string button, int buttons)
    {
        var parameters = new JsonObject
        {
            ["type"] = type,
            ["x"] = x,
            ["y"] = y,
            ["button"] = button,
            ["buttons"] = buttons,
            ["clickCount"] = 1
        };

        _client?.SendAsync("Input.dispatchMouseEvent", parameters).GetAwaiter().GetResult();
    }
}
