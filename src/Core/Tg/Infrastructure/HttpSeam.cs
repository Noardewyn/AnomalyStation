using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// M10x slice G6. A host seam, not tg code: tg's /datum/http_request sends through rust-g's HTTP (rustg_http_request_fire_and_forget,
// __DEFINES/rust_g.dm), which is not translated (rules 13); the server sends the request (the admin help's Discord webhook) off the
// world's thread. A world without one (the Core tests) sends nothing, as a server without rust-g's HTTP.

/// <summary>The HTTP requests a host sends for its world.</summary>
public interface IHttpRequests
{
    /// <summary>rust-g's fire-and-forget request: <paramref name="headers"/> is tg's JSON object of headers, or "".</summary>
    void FireAndForget(string method, string url, string body, string headers);
}

/// <summary>The world's HTTP requests (none by default).</summary>
public static class HttpSeam
{
    /// <summary>Installs the world's HTTP sender (the host, before the boot; null removes it).</summary>
    public static void Install(World world, IHttpRequests requests)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.GetExtension(static _ => new Holder()).Requests = requests;
    }

    /// <summary>The current world's HTTP sender, or null.</summary>
    public static IHttpRequests Current => World.CurrentOrNull?.GetExtension(static _ => new Holder()).Requests;

    private sealed class Holder
    {
        internal IHttpRequests Requests;
    }
}
