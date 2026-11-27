// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/festival/festival_violin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the festival god's violin, which tells its player a song's length against the length a finishing effect needs.
// The var block is generated (D111).

// tg: code/modules/religion/festival/festival_violin.dm:1
public partial class obj_item_instrument_violin_festival : obj_item_instrument_violin
{
    // tg: code/modules/religion/festival/festival_violin.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_INSTRUMENT_START, nameof(on_instrument_start));
        return dot;
    }

    /// signal fired when the festival instrument starts to play.
    // tg: code/modules/religion/festival/festival_violin.dm:12
    [SignalHandler]
    public virtual void on_instrument_start(datum source = null, datum_song starting_song = null, atom player = null)
    {
        if (starting_song == null || !isliving(player))
            return;
        analyze_song(starting_song, player as mob_living);
    }

    ///Reports some relevant information when the song begins playing.
    // tg: code/modules/religion/festival/festival_violin.dm:20
    public virtual void analyze_song(datum_song song = null, mob_living playing_song = null)
    {
        DmList analysis = Dm.list();
        //check tempo and lines
        float song_length = song.lines.len * song.tempo;
        Dm.append(ref analysis, span_revenbignotice(I18n.T("[] speaks to you...", this)));
        Dm.append(ref analysis, span_revennotice(I18n.T("\"This song has <b>[]</b> lines and a tempo of <b>[]</b>.\"", song.lines.len, song.tempo)));
        Dm.append(ref analysis, span_revennotice(I18n.T("\"Multiplying these together gives a song length of <b>[]</b>.\"", song_length)));
        Dm.append(ref analysis, span_revennotice(I18n.T("\"To get a finishing effect from [], your songs must conclude with a length of <b>[]</b>.\"", GLOB.deity, FESTIVAL_SONG_LONG_ENOUGH)));

        to_chat(playing_song, analysis.Join("\n"));
    }
}
