using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M9;

/// <summary>
/// M9 slice D2: tg's station name (__HELPERS/names.dm:87-160): written once on first use from its prefixes, names, suffixes and
/// an ID numeral, which may be written out in words (convert_integer_to_words, __HELPERS/text.dm:940), and it becomes the
/// world's name.
/// </summary>
public class StationNameTests
{
    [Theory]
    [InlineData(0, false, "zero")]
    [InlineData(13, false, "thirteen")]
    [InlineData(99, true, "Ninety-Nine")]
    [InlineData(-1, true, "Negative One")]
    [InlineData(250, false, "two hundred and fifty")]
    [InlineData(1000, false, "one thousand")]
    [InlineData(1099, true, "One Thousand And Ninety-Nine")]
    [InlineData(12345678, false, "twelve million three hundred and forty-five thousand six hundred and seventy-eight")] // DM numbers are 32-bit floats: past 2^24 the last digits round, in BYOND too
    public void Integers_are_written_in_words_as_tg_writes_them(int number, bool capitalise, string words)
    {
        var r = new AudioRoom(4, 4, seed: 1);
        Assert.Equal(words, r.In(() => convert_integer_to_words(number, capitalise)));
    }

    [Fact]
    public void The_station_is_named_Anomaly_Station_by_default()
    {
        var r = new AudioRoom(4, 4, seed: 7);
        Assert.Equal("Anomaly Station", r.In(() => Dm.str(station_name())));
    }

    [Fact]
    public void An_empty_STATIONNAME_still_names_the_station_Anomaly_Station_and_the_world()
    {
        var r = new AudioRoom(4, 4, seed: 7);
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_string_stationname)]).config_entry_value = "";
            GLOB.station_name = null; // the ticker's lobby welcome (ticker.dm:127) may have named the station during the boot
        });
        Assert.Equal("Anomaly Station", r.In(() => Dm.str(station_name())));
        Assert.Equal("Anomaly Station", r.In(() => Dm.str(Dm.world.name)));
    }
}
