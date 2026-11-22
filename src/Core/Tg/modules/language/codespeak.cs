// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/codespeak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the overwatch agent's language, and the codespeak manuals). The var block is generated (D111).

// tg: code/modules/language/codespeak.dm:1
public partial class datum_language_codespeak : datum_language
{
    // tg: code/modules/language/codespeak.dm:10
    public override object scramble_sentence(object input = null, DmList mutual_languages = null)
    {
        object sentence = read_word_cache(input);
        if (Dm.truthy(sentence))
            return sentence;

        sentence = "";
        DmList words = Dm.list();
        while (Dm.num(length_char(sentence)) < Dm.num(length_char(input)))
        {
            Dm.append(ref words, generate_code_phrase(return_list: TRUE));
            sentence = jointext(words, ", ");
        }

        sentence = capitalize(sentence);

        sentence = Dm.add(sentence, find_last_punctuation(input));

        write_word_cache(input, sentence);

        return sentence;
    }
}
