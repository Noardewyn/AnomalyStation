using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 25 (D155, docs/ai/specs/protocol.md section 34): an appearance's filters and its render target round trip in the
/// public and the private tables; an entry without them costs nothing more; an undefined filter kind is refused both ways.
/// </summary>
public class ProtocolV25Tests
{
    private static Delta WithAppearance(AppearanceEntry entry)
    {
        Delta d = Msg.Delta(10, 4, 5);
        d.FirstNewString = 1;
        d.NewStrings.Add("*MissGlove 0x2000001 R");
        d.FirstNewResource = 1;
        d.NewResources.Add("icons/effects/cranial_fissure.dmi");
        d.FirstNewAppearance = 1;
        d.NewAppearances.Add(entry);
        return d;
    }

    [Fact]
    public void TheVersionIsTwentyFive() => Assert.True(ProtocolVersion.Current >= 25);

    [Fact]
    public void FiltersAndARenderTargetRoundTrip()
    {
        var entry = new AppearanceEntry
        {
            RenderTarget = TableRef.Public(1),
            Filters = new List<FilterEntry>
            {
                new() { Kind = FilterKind.Alpha, RenderSource = TableRef.Public(1), Y = 2, Flags = FilterFlags.MaskInverse },
                new() { Kind = FilterKind.Outline, Size = 1.2f, Rgba = 0xFF0033FF },
                new() { Kind = FilterKind.Displace, Icon = TableRef.Public(1), IconState = TableRef.Public(1), Size = 3 },
                new() { Kind = FilterKind.Color, ColorMatrix = Enumerable.Range(0, 20).Select(i => i / 10f).ToArray() },
            },
        };
        Delta d = WithAppearance(entry);
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        AppearanceEntry e = back.NewAppearances[0];
        Assert.Equal(TableRef.Public(1), e.RenderTarget);
        Assert.Equal(4, e.Filters.Count);
        for (int i = 0; i < 4; i++)
            Assert.Equal(entry.Filters[i], e.Filters[i]);
    }

    [Fact]
    public void AnEntryWithoutFiltersCostsNothingMore()
    {
        byte[] plain = Codec.Encode(WithAppearance(new AppearanceEntry { Icon = TableRef.Public(1) }));
        Assert.True(Codec.TryDecode(plain, out Delta back, out _));
        Assert.Null(back.NewAppearances[0].Filters);
        Assert.True(back.NewAppearances[0].RenderTarget.IsNone);
        byte[] outlined = Codec.Encode(WithAppearance(new AppearanceEntry
        {
            Icon = TableRef.Public(1),
            Filters = new List<FilterEntry> { new() { Kind = FilterKind.Outline } }, // a default outline: kind and an empty field mask
        }));
        Assert.Equal(plain.Length + 2 + 3, outlined.Length); // presence varuint grows from 1 to 3 bytes; count, kind, fields
    }

    [Fact]
    public void AnUndefinedFilterKindIsRefused()
    {
        var entry = new AppearanceEntry { Filters = new List<FilterEntry> { new() { Kind = (FilterKind)99 } } }; // version 26 defines 1 to 15
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithAppearance(entry)));
    }
}
