namespace Tm2020Mcp.Tests.EditorBridge;

/// <summary>
/// Serves canned responses so bridge-client tests never need a running game.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : this((request, _) => Task.FromResult(responseFactory(request)))
    {
    }

    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _responseFactory(request, cancellationToken);
    }
}
