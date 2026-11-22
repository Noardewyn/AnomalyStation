using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// tg's <c>url_encode</c> (<c>__DEFINES/rust_g_overrides.dm:2</c>: <c>rustg_url_encode("[text]")</c>) and rust-g's native
/// <c>url_encode</c> (rust-g src/url.rs: <c>url::form_urlencoded::byte_serialize</c> of the UTF-8 bytes), written in C#
/// in <c>Builtin/rust_g.builtin.cs</c>; and <c>REF()</c> of a datum with <c>DF_USE_TAG</c> and a tag, which uses it
/// (<c>__HELPERS/ref.dm:13</c>).
/// </summary>
public class UrlEncodeTests
{
    [Fact]
    public void UrlEncodeIsFormUrlencodedOverUtf8Bytes()
    {
        // form_urlencoded::byte_serialize: '*', '-', '.', '_', digits and ASCII letters as they are, ' ' as '+', every
        // other byte %XX with upper-case hex (percent_encoding's table).
        Assert.Equal("AZaz09*-._", url_encode("AZaz09*-._"));
        Assert.Equal("a+b", url_encode("a b"));
        Assert.Equal("%7E%21%27%28%29%2B%2F%3F%26%3D%25%23%5B%5D%3A%40%2C%3B%22%5C%0A", url_encode("~!'()+/?&=%#[]:@,;\"\\\n"));
        Assert.Equal("%D0%BF%D1%80%D0%B8%D0%B2%D0%B5%D1%82", url_encode("привет"));
        Assert.Equal("%F0%9F%99%82", url_encode("\U0001F642"));                // one 4-byte UTF-8 sequence
        Assert.Equal("", url_encode(null));                                      // "[null]" is ""
        Assert.Equal("5", url_encode(5f));                                       // "[5]"
        Assert.Equal("%2Fdatum", url_encode(typeof(datum)));                    // "[/datum]"
        Assert.Equal(rustg_url_encode("x y"), url_encode("x y"));
    }

    [Fact]
    public void RefOfATaggedDatumIsItsUrlEncodedTag()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            d.tag = "pilot tag/1";
            d.datum_flags = DF_USE_TAG;
            // ref.dm:13: return "\[[url_encode(thing.tag)]\]"
            Assert.Equal("[pilot+tag%2F1]", REF(d));
            d.datum_flags = 0;
            Assert.Equal(Dm.@ref(d), REF(d));
            Assert.Empty(t.Runtimes);
        }
    }
}
