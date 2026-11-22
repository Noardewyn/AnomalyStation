using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

public class HandBoxesTests
{
    private const uint Hand = 0xFF0000FF, Letter = 0x00FF00FF;
    private const string Json = @"{""version"":""4.0"",""width"":32,""height"":32,""sheetWidth"":32,""sheetHeight"":32,""states"":[
        {""name"":""hand_r"",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":0,""rects"":[[0,0,32,32]]}]}";

    private static (IconSheet Sheet, Raster Pixels) RightHand()
    {
        var pixels = new Raster(32, 32);
        pixels.Set(10, 20, Hand); // tg's right hand has its fingers low on the box's left
        pixels.Set(4, 5, Letter); // the R's left column
        return (IconSheet.Parse(Json, pixels), pixels);
    }

    [Fact]
    public void TheHandTurnsFingersUpAndOutwardAndItsLetterStaysReadableInTheOtherCorner()
    {
        (IconSheet sheet, Raster pixels) = RightHand();
        HandBoxes.Mirror("icons/hud/screen_midnight.dmi", sheet, pixels);
        Assert.Equal(Hand, pixels.Get(21, 12)); // now high on the right
        Assert.Equal(0u, pixels.Get(10, 20));
        Assert.Equal(Letter, pixels.Get(23, 5)); // the letter's columns keep their order in the mirrored corner (23 to 27)
        Assert.Equal(0u, pixels.Get(4, 5));
    }

    [Fact]
    public void OtherSheetsStayAsTgDrewThem()
    {
        (IconSheet sheet, Raster pixels) = RightHand();
        HandBoxes.Mirror("icons/obj/doors/airlock.dmi", sheet, pixels);
        Assert.Equal(Hand, pixels.Get(10, 20));
    }
}
