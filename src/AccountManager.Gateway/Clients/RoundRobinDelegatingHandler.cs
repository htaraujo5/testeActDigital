using AccountManager.Gateway.Options;
using Microsoft.Extensions.Options;

namespace AccountManager.Gateway.Clients;

public sealed class RoundRobinBaseAddressSelector
{
    private readonly string[] _addresses;
    private long _counter;

    public RoundRobinBaseAddressSelector(IOptions<UpstreamApiOptions> options)
    {
        _addresses = options.Value.BaseAddresses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.TrimEnd('/'))
            .ToArray();

        if (_addresses.Length == 0)
        {
            throw new InvalidOperationException("UpstreamApis:BaseAddresses must contain at least one URL.");
        }
    }

    public Uri Next()
    {
        var index = Interlocked.Increment(ref _counter);
        var address = _addresses[(int)((index - 1) % _addresses.Length)];
        if (index == long.MaxValue)
        {
            Interlocked.Exchange(ref _counter, 0);
        }

        return new Uri(address + "/");
    }
}

public sealed class RoundRobinDelegatingHandler : DelegatingHandler
{
    private readonly RoundRobinBaseAddressSelector _selector;

    public RoundRobinDelegatingHandler(RoundRobinBaseAddressSelector selector)
    {
        _selector = selector;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var baseUri = _selector.Next();
        var relative = request.RequestUri is { IsAbsoluteUri: true }
            ? request.RequestUri.PathAndQuery
            : request.RequestUri?.OriginalString ?? "/";

        request.RequestUri = new Uri(baseUri, relative.TrimStart('/'));
        return base.SendAsync(request, cancellationToken);
    }
}
