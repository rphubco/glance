namespace Glance.Core;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class RateLimitHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    const int MaxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        for (var i = 0; i <= MaxRetries; i++)
        {
            var res = await base.SendAsync(req, ct);
            if (res.StatusCode != HttpStatusCode.TooManyRequests || i == MaxRetries)
                return res;

            var delay = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
            delay += TimeSpan.FromMilliseconds(Random.Shared.Next(100, 500)); // jitter
            res.Dispose();

            await Task.Delay(delay, ct);
        }

        throw new HttpRequestException("retry limit exceeded lol");
    }
}
