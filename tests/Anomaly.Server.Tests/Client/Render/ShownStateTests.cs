using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// M3 I1d: BYOND's display rule for an icon state (OpenDream <c>GetStateOrDefault</c>): a state the icon lacks shows the
/// icon's default ("") state; an icon without one shows nothing. tg's canisters rely on it (their GAGS icon has only "").
/// </summary>
public class ShownStateTests
{
    private const string WithDefault = @"{""version"":""4.0"",""width"":1,""height"":1,""sheetWidth"":2,""sheetHeight"":1,""states"":[
        {""name"":"""",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":0,""rects"":[[0,0,1,1]]},
        {""name"":""lit"",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":1,""rects"":[[1,0,1,1]]}]}";

    private const string WithoutDefault = @"{""version"":""4.0"",""width"":1,""height"":1,""sheetWidth"":1,""sheetHeight"":1,""states"":[
        {""name"":""lit"",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":0,""rects"":[[0,0,1,1]]}]}";

    private static Raster Sheet(int width)
    {
        var sheet = new Raster(width, 1);
        for (int x = 0; x < width; x++)
            sheet.Set(x, 0, (uint)(x + 1) << 24 | 0xFF);
        return sheet;
    }

    [Fact]
    public void AMissingStateShowsTheDefaultState()
    {
        IconSheet icon = IconSheet.Parse(WithDefault, Sheet(2));
        Assert.True(icon.TryGetShown("/obj/machinery/portable_atmospherics/canister", out IIconState shown));
        Assert.Equal(1u << 24 | 0xFF, shown.Get(0, 0).Get(0, 0));
        Assert.True(icon.TryGetShown("lit", out IIconState lit));
        Assert.Equal(2u << 24 | 0xFF, lit.Get(0, 0).Get(0, 0));
        Assert.False(icon.TryGetState("/obj/machinery/portable_atmospherics/canister", out _)); // the exact lookup is unchanged
    }

    [Fact]
    public void WithoutADefaultStateAMissingStateShowsNothing()
    {
        IconSheet icon = IconSheet.Parse(WithoutDefault, Sheet(1));
        Assert.False(icon.TryGetShown("missing", out _));
        Assert.False(icon.TryGetShown(null, out _));
        Assert.True(icon.TryGetShown("lit", out _));
    }
}
