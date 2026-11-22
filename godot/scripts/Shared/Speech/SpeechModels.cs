using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Speech;

// D307: text to speech and speech to text on the client (D16), opt-in. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). The models the client downloads when the player turns a setting on, the languages the player picks,
// the language of a line and the character's voice.

/// <summary>
/// One file of a model: its name in the model's folder, its SHA-256 (D319: or, 40 hex digits, git's blob SHA-1, all Hugging Face gives
/// for a file kept outside LFS), its size and (D319) the folder it is in under the model's URL.
/// </summary>
public sealed record SpeechModelFile(string Name, string Sha256, long Bytes, string From = "");

/// <summary>A model the client downloads file by file from <see cref="BaseUrl"/> into its own folder under the user folder.</summary>
public sealed record SpeechModel(string Folder, string Title, string BaseUrl, IReadOnlyList<SpeechModelFile> Files)
{
    public long Bytes => Files.Sum(f => f.Bytes);

    /// <summary>
    /// Supertone's Supertonic 3 in sherpa-onnx's int8 export (OpenRAIL-M, commercial use allowed; its LICENSE is downloaded beside it):
    /// one model for 31 languages, ten speakers (sid 0-4 women, 5-9 men).
    /// </summary>
    public static readonly SpeechModel Tts = new("supertonic-3", "Supertonic 3 (text to speech)",
        "https://huggingface.co/csukuangfj2/sherpa-onnx-supertonic-3-tts-int8-2026-05-11/resolve/main/", new[]
        {
            new SpeechModelFile("duration_predictor.int8.onnx", "c3eb91414d5ff8a7a239b7fe9e34e7e2bf8a8140d8375ffb14718b1c639325db", 3700147),
            new SpeechModelFile("text_encoder.int8.onnx", "c7befd5ea8c3119769e8a6c1486c4edc6a3bc8365c67621c881bbb774b9902ff", 36416150),
            new SpeechModelFile("vector_estimator.int8.onnx", "20cd86fa5c6effedfda0e7cffe5b0569ca401c440a0c3a1d72bf39286c0db3fd", 78400833),
            new SpeechModelFile("vocoder.int8.onnx", "e923d60f53f95eb1ce235f1dc33ec56d9c057823c96fa6f8acf98f32b0da6152", 25991073),
            new SpeechModelFile("tts.json", "42078d3aef1cd43ab43021f3c54f47d2d75ceb4e75f627f118890128b06a0d09", 8253),
            new SpeechModelFile("unicode_indexer.bin", "8402ca48e5189a8950138580b0fff64db6f072f24ac07cd54ba8b2fbb9883b30", 262144),
            new SpeechModelFile("voice.bin", "67d5209b0ee8ce6c74105ffbe12fe6a7628aea3b4ba2fcb308a4a67938a93ce8", 517168),
            new SpeechModelFile("LICENSE", "0dfe0d0ba84416fe3879d9a34f4909d8d0137c78d1e95834177b0414ac096fa2", 1070),
        });

    /// <summary>OpenAI's multilingual Whisper base in sherpa-onnx's int8 export (MIT): 99 languages.</summary>
    public static readonly SpeechModel Stt = new("whisper-base", "Whisper base (speech to text)",
        "https://huggingface.co/csukuangfj/sherpa-onnx-whisper-base/resolve/main/", new[]
        {
            new SpeechModelFile("base-encoder.int8.onnx", "0b8fb1304b6109976038efff5ace81720e00386f3ff6b54ee8c75291ca0a1e11", 29120534),
            new SpeechModelFile("base-decoder.int8.onnx", "9759d217388a01b3a4c7c15533201067b48ae819c4daafc8624e64b9409dc02d", 130672026),
            new SpeechModelFile("base-tokens.txt", "b34b360dbb493e781e479794586d661700670d65564001f23024971d1f2fa126", 816730),
        });
}

/// <summary>The languages the player can pick for speech: Supertonic 3's, which Whisper recognizes too.</summary>
public static class SpeechLanguages
{
    public static readonly IReadOnlyList<(string Code, string Name)> All = new[]
    {
        ("en", "English"), ("ru", "Russian"), ("uk", "Ukrainian"), ("de", "German"), ("fr", "French"), ("es", "Spanish"),
        ("it", "Italian"), ("pt", "Portuguese"), ("pl", "Polish"), ("cs", "Czech"), ("sk", "Slovak"), ("sl", "Slovenian"),
        ("hr", "Croatian"), ("bg", "Bulgarian"), ("ro", "Romanian"), ("hu", "Hungarian"), ("nl", "Dutch"), ("da", "Danish"),
        ("sv", "Swedish"), ("fi", "Finnish"), ("et", "Estonian"), ("lv", "Latvian"), ("lt", "Lithuanian"), ("el", "Greek"),
        ("tr", "Turkish"), ("ar", "Arabic"), ("hi", "Hindi"), ("id", "Indonesian"), ("vi", "Vietnamese"), ("ja", "Japanese"),
        ("ko", "Korean"),
    };

    public static readonly IReadOnlyList<string> Defaults = new[] { "en", "ru" };

    public static bool IsKnown(string code) => All.Any(l => l.Code == code);

    private enum Script { None, Latin, Cyrillic, Greek, Arabic, Devanagari, Kana, Han, Hangul }

    private static readonly Dictionary<Script, string[]> ScriptLanguages = new()
    {
        [Script.Cyrillic] = new[] { "ru", "uk", "bg" },
        [Script.Greek] = new[] { "el" },
        [Script.Arabic] = new[] { "ar" },
        [Script.Devanagari] = new[] { "hi" },
        [Script.Kana] = new[] { "ja" },
        [Script.Han] = new[] { "ja" },
        [Script.Hangul] = new[] { "ko" },
    };

    // D312: Whisper's Slavic and Baltic languages and the others written in Cyrillic (Russian speech is heard as Polish, say), and its
    // languages in neither Cyrillic nor Latin
    private static readonly HashSet<string> WhisperSlavic = new()
    {
        "ru", "uk", "be", "bg", "sr", "mk", "kk", "mn", "tt", "ba", "tg", "pl", "cs", "sk", "sl", "hr", "bs", "lt", "lv",
    };
    private static readonly HashSet<string> WhisperOtherScripts = new()
    {
        "zh", "yue", "ja", "ko", "ar", "fa", "ur", "ps", "he", "yi", "el", "hi", "mr", "ne", "sa", "bn", "as", "pa", "gu", "ta", "te", "kn", "ml",
        "si", "th", "lo", "km", "my", "bo", "ka", "hy", "am",
    };

    /// <summary>
    /// D312 (the owner, 2026-11-03: a phrase came out in Chinese or French with Russian and English picked): the language a phrase is
    /// recognized in, out of the <paramref name="picked"/> ones, from the language Whisper <paramref name="heard"/>: that one when picked,
    /// else the one picked of its kind (Slavic or Cyrillic, other Latin), else the language of the <paramref name="last"/> phrase, else
    /// the first picked.
    /// </summary>
    // shortcut: a phrase heard in another script (a short "да" heard as Japanese) goes to the last phrase's language; upgrade to scoring
    // each picked language's decode if players often switch languages in short phrases.
    public static string Closest(string heard, IReadOnlyList<string> picked, string last)
    {
        if (picked.Contains(heard))
            return heard;
        bool Slavic(string code) => WhisperSlavic.Contains(code);
        bool Latin(string code) => !Slavic(code) && !WhisperOtherScripts.Contains(code);
        Func<string, bool> family = heard == null || WhisperOtherScripts.Contains(heard) ? null : Slavic(heard) ? Slavic : Latin;
        if (family != null && picked.Where(family).ToList() is [string only])
            return only;
        return picked.Contains(last) ? last : picked[0];
    }

    // Letters one language of a script has and the others mostly do not.
    private static readonly (string Code, string Letters)[] Marks =
    {
        ("uk", "іїєґ"), ("ru", "ыэё"), ("de", "äöüß"), ("es", "ñ¿¡"), ("fr", "çéèêàùœ"), ("pt", "ãõç"), ("pl", "łąęśżźń"),
        ("cs", "řěů"), ("sk", "ľĺŕô"), ("tr", "ğşı"), ("hu", "őű"), ("sv", "åä"), ("da", "æøå"), ("ro", "țșăâî"),
        ("lv", "āēīūļņķģ"), ("lt", "ėįųū"), ("et", "õäöü"), ("hr", "đćč"), ("vi", "ơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữự"),
        ("fi", "äö"), ("it", "àèìòù"), ("sl", "čšž"),
    };

    // D310: each language's commonest short words, so a line with no telltale letter still finds its language
    private static readonly Dictionary<string, HashSet<string>> Words = new()
    {
        ["en"] = Set("the a an and is are was to of in it you i he she we they that this what not have has do don't with for on at my your me be can will"),
        ["de"] = Set("der die das und ist ein eine ich du nicht zu mit den dem sie wir es auf was ja nein bitte hier mein dein ihr sind"),
        ["fr"] = Set("le la les un une et est je tu il elle nous vous pas de du des que qui en avec pour sur mon ton oui non c'est ici"),
        ["es"] = Set("el la los las un una y es yo tu él ella no de que en con por para mi su sí aquí está estoy hola gracias"),
        ["it"] = Set("il lo la gli le un una e è io tu lui lei non di che in con per mio sono sì qui ciao grazie questo"),
        ["pt"] = Set("o a os as um uma e é eu tu ele ela não de que em com por para meu sim aqui está você obrigado"),
        ["nl"] = Set("de het een en is ik jij je hij zij wij niet van dat in met voor op mijn ja nee hier wat zijn"),
        ["pl"] = Set("i w na nie to jest się że do z jak co ja ty on ona my tak już tu mnie"),
        ["cs"] = Set("a v na ne to je se že do z jak co já ty on ona my ano už tady mě"),
        ["sk"] = Set("a v na nie to je sa že do z ako čo ja ty on ona my áno už tu ma"),
        ["sl"] = Set("in v na ne to je se da do z kako kaj jaz ti on ona mi ja že tukaj"),
        ["hr"] = Set("i u na ne to je se da do s kako što ja ti on ona mi da već ovdje"),
        ["ro"] = Set("și în nu este un o eu tu el ea noi de la cu pe ce da aici sunt"),
        ["hu"] = Set("a az és egy nem van én te ő mi ez az hogy igen itt meg de"),
        ["da"] = Set("og i er en et jeg du han hun vi ikke at det til med på ja nej her min"),
        ["sv"] = Set("och i är en ett jag du han hon vi inte att det till med på ja nej här min"),
        ["fi"] = Set("ja on ei se minä sinä hän me että mitä tämä kyllä täällä olen"),
        ["et"] = Set("ja on ei see mina sina tema meie et mis jah siin olen"),
        ["lv"] = Set("un ir ne es tu viņš viņa mēs ka kas jā šeit esmu"),
        ["lt"] = Set("ir yra ne aš tu jis ji mes kad kas taip čia esu"),
        ["tr"] = Set("ve bir bu ne ben sen o biz değil evet hayır burada için ile var"),
        ["id"] = Set("dan yang di ini itu saya kamu dia kami tidak ya ada untuk dengan"),
        ["vi"] = Set("và là của tôi bạn anh chị không có này đó được"),
        ["ru"] = Set("и в не на я ты он она мы что это как да нет здесь все так меня"),
        ["uk"] = Set("і в не на я ти він вона ми що це як так ні тут все мене"),
        ["bg"] = Set("и в не на аз ти той тя ние че това как да не тук всичко съм"),
    };

    private static HashSet<string> Set(string words) => new(words.Split(' '));

    /// <summary>
    /// The language to speak <paramref name="text"/> in, out of the player's <paramref name="enabled"/> languages (D75: TTS speaks in the
    /// language of the text), or null when the text's script is none of theirs.
    /// </summary>
    // shortcut: the script, a few telltale letters and the commonest words pick the language (a word counts twice a letter); a line with
    // none of them goes to the first language the player picked; upgrade to a trained language identifier if lines are often misvoiced.
    public static string Detect(string text, IReadOnlyList<string> enabled)
    {
        if (string.IsNullOrWhiteSpace(text) || enabled == null || enabled.Count == 0)
            return null;
        Script script = MainScript(text);
        if (script == Script.None)
            return null;
        string[] candidates = script == Script.Latin
            ? enabled.Where(c => !ScriptLanguages.Values.Any(l => l.Contains(c))).ToArray()
            : enabled.Where(c => ScriptLanguages[script].Contains(c)).ToArray();
        if (candidates.Length == 0)
            return null;
        string lower = text.ToLowerInvariant();
        string[] words = lower.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('.', ',', '!', '?', ';', ':', '"', '(', ')', '«', '»', '¿', '¡')).ToArray();
        string best = candidates[0];
        int bestScore = 0;
        foreach (string code in candidates)
        {
            int score = Marks.Where(m => m.Code == code).Sum(m => lower.Count(ch => m.Letters.Contains(ch)))
                + 2 * (Words.TryGetValue(code, out HashSet<string> common) ? words.Count(common.Contains) : 0);
            if (score > bestScore)
            {
                best = code;
                bestScore = score;
            }
        }
        return best;
    }

    private static Script MainScript(string text)
    {
        var counts = new Dictionary<Script, int>();
        foreach (char ch in text)
        {
            Script s = Of(ch);
            if (s != Script.None)
                counts[s] = counts.GetValueOrDefault(s) + 1;
        }
        if (counts.Count == 0)
            return Script.None;
        // kana among Han characters is Japanese
        if (counts.ContainsKey(Script.Kana))
            return Script.Kana;
        return counts.MaxBy(kv => kv.Value).Key;
    }

    private static Script Of(char ch)
    {
        if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= 'À' and <= 'ɏ' or >= 'Ḁ' and <= 'ỿ')
            return Script.Latin;
        if (ch is >= 'Ѐ' and <= 'ӿ')
            return Script.Cyrillic;
        if (ch is >= 'Ͱ' and <= 'Ͽ')
            return Script.Greek;
        if (ch is >= '؀' and <= 'ۿ')
            return Script.Arabic;
        if (ch is >= 'ऀ' and <= 'ॿ')
            return Script.Devanagari;
        if (ch is >= '぀' and <= 'ヿ')
            return Script.Kana;
        if (ch is >= '一' and <= '鿿')
            return Script.Han;
        if (ch is >= '가' and <= '힯' or >= 'ᄀ' and <= 'ᇿ')
            return Script.Hangul;
        return Script.None;
    }
}

/// <summary>
/// The characters' voices: tg's <c>SStts.available_speakers</c> are the TTS server's (VCTK's 109 speakers); the port's are the ten of
/// Supertonic 3, named so that tg's <c>random_tts_voice</c> tells them by "Man" and "Woman" (DV262).
/// </summary>
public static class SpeechVoices
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Woman 1", "Woman 2", "Woman 3", "Woman 4", "Woman 5", "Man 1", "Man 2", "Man 3", "Man 4", "Man 5",
    };

    /// <summary>The model's speaker id of a voice; an unknown voice (an old save, a name from elsewhere) gets a steady one from its name.</summary>
    public static int Sid(string voice)
    {
        if (string.IsNullOrEmpty(voice))
            return 0;
        int index = Names.ToList().IndexOf(voice);
        if (index >= 0)
            return index;
        uint hash = 2166136261;
        foreach (char ch in voice)
            hash = (hash ^ ch) * 16777619;
        return (int)(hash % (uint)Names.Count);
    }

    /// <summary>tg's SOUND_RANGE: a line's sound fades out there.</summary>
    public const float SoundRange = 15f;

    /// <summary>D312: a whisper is heard 2 tiles off (tg's whisper range 1 and eavesdrop 1): it fades out at 4.</summary>
    public const float WhisperRange = 4f;

    /// <summary>D312: a shout is heard 14 tiles off (the port's SHOUT_RANGE, twice the say range): it fades out at twice tg's sound range.</summary>
    public const float ShoutRange = 2 * SoundRange;

    /// <summary>D312: a line's falloff range by its flags.</summary>
    public static float RangeOf(TtsFlags flags) =>
        (flags & TtsFlags.Whisper) != 0 ? WhisperRange : (flags & TtsFlags.Yell) != 0 ? ShoutRange : SoundRange;

    /// <summary>
    /// tg's pitch preference (-12 to 12 semitones, rubberband on the TTS server) as the playback rate; the line is synthesized slower by the
    /// same factor so that it keeps its length.
    /// </summary>
    public static float PitchScale(int semitones) => MathF.Pow(2f, Math.Clamp(semitones, -12, 12) / 12f);

    /// <summary>
    /// tg's <c>playsound_local</c> volume of a line (play_tts: max_distance SOUND_RANGE 15, falloff distance 0, exponent 2.5) at
    /// <paramref name="tiles"/> from the speaker, as a gain (tg's volume 100 is 1). D312: a whisper fades out within
    /// <see cref="WhisperRange"/> tiles and a shout within <see cref="ShoutRange"/>.
    /// </summary>
    public static float Gain(int volume, float tiles, float range = SoundRange)
    {
        float distance = Math.Max(tiles, 0f);
        float ratio = MathF.Pow(distance / Math.Max(range, distance), 1f / 2.5f);
        return Math.Max(0f, volume * (1f - ratio)) / 100f;
    }
}
