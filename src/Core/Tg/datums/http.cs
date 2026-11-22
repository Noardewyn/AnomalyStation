// Translated from tgstation (snapshot 1d99bd7f): code/datums/http.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10x slice G6: the admin help's Discord webhook): prepare, fire_and_forget and build_options, rust-g's request through the
// host's HttpSeam. Not translated: the blocking and async requests and their responses (execute_blocking, begin_async, is_complete,
// into_response, /datum/http_response): nothing ported waits for an answer.

// tg: code/datums/http.dm:1
public partial class datum_http_request : datum
{
    // tg: code/datums/http.dm:17
    public virtual void prepare(object method = null, object url = null, object body = null, DmList headers = null, object output_file = null, object timeout_seconds = null)
    {
        body ??= "";
        object encoded_headers;
        if (!Dm.truthy(Dm.length(headers)))
            encoded_headers = "";
        else
            encoded_headers = Dm.json_encode(headers);

        this.method = method;
        this.url = url;
        this.body = body;
        this.headers = encoded_headers;
        this.output_file = output_file;
        this.timeout_seconds = timeout_seconds;
    }

    // tg: code/datums/http.dm:30
    public virtual void fire_and_forget()
    {
        // rustg_http_request_fire_and_forget(method, url, body, headers, build_options()): the host's HttpSeam, which answers "ok"
        IHttpRequests requests = HttpSeam.Current;
        if (requests == null)
            return; // no HTTP in this world (rust-g's HTTP is the host's)
        requests.FireAndForget(Dm.str(method), Dm.str(url), Dm.str(body), Dm.str(headers));
    }

    // tg: code/datums/http.dm:50
    public virtual string build_options()
    {
        return Dm.json_encode(Dm.list(
            Dm.kv("output_filename", Dm.truthy(output_file) ? output_file : null),
            Dm.kv("body_filename", null),
            Dm.kv("timeout_seconds", Dm.truthy(timeout_seconds) ? timeout_seconds : null)));
    }
}
