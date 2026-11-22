// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/tts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (D307): only tts_speech_filter translated. tts_gibberish_speech_filter and tts_filter_encode build the TTS server's requests
// (DV262: the clients synthesize).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/tts.dm:1
    // DEVIATION(DV262): letters of every script stay (tg keeps [a-zA-Z0-9 ,?.!'&-]: the text went into a shell to its English-only server)
    public static string tts_speech_filter(string text)
    {
        // Only allow alphanumeric characters and whitespace
        var scrubbed = new System.Text.StringBuilder(text.Length);
        foreach (char ch in text)
            scrubbed.Append(char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || ",?.!'&-".IndexOf(ch) >= 0 ? ch : ' ');
        return scrubbed.ToString();
    }
}
