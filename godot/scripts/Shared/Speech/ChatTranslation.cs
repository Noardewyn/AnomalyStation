using System.Linq;
using System.Text.RegularExpressions;

namespace Anomaly.Client.Shared.Speech;

// D319 (the owner, 2026-11-05): the chat translator, opt-in. Another player's words in a chat line (a say or radio line's quote, an OOC
// line's message) are translated on the client into the language the player picked, with Opus-MT models (CC BY 4.0) downloaded like
// the speech models: any language to English, then English to the picked language. The line shows the translation, the original line
// under it a little smaller. Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
public static class ChatTranslation
{
    /// <summary>The Opus-MT models in Xenova's ONNX export, by their language pair; the files are kept flat in the model's folder.</summary>
    public static readonly IReadOnlyDictionary<string, SpeechModel> Models = new Dictionary<string, SpeechModel>
    {
        ["mul-en"] = new("opus-mt-mul-en", "Opus-MT many languages to English (translation)",
            "https://huggingface.co/Xenova/opus-mt-mul-en/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "5ce609a524375dbdd9c66b62b82b41abc667799022667ce424f20c245bd56925", 52475294, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "8ac053f1ef3db2dd9013fe66bfc9e30c0a762457ec11610f1e6b0a5cab9ac8d7", 59414338, "onnx/"),
            new SpeechModelFile("source.spm", "aa22d72fb84b99dc78fd866d2dd986a9178a7f8d", 706917, ""),
            new SpeechModelFile("vocab.json", "a0e6e376ced0aa3e25c3d0da290dd03728f7f817", 1552293, ""),
            new SpeechModelFile("config.json", "8eb78d90e099333fa5871c9f1deb99b65a837524", 1390, ""),
        }),
        ["en-zle"] = new("opus-mt-tc-big-en-zle", "Opus-MT big English to Russian and Ukrainian (translation)",
            "https://huggingface.co/TigreGotico/opus-mt-tc-big-en-zle-onnx/resolve/5d49b6e3c63c3eddeb6ad2190ec7e2f661b6bd49/", new[]
        {
            new SpeechModelFile("encoder_model.onnx", "1361c4babcffa5245986d27a2e67f075132f95c2453b6c56fec1216d6c4d5271", 140063720, "int8/"),
            new SpeechModelFile("decoder_model.onnx", "9138d3b26a755bf685cb3424b8c9e739ca8c7306e85fc708edf87ca801724cdf", 228889401, "int8/"),
            new SpeechModelFile("source.spm", "3612abfe04bf08344ba91115f0e15e228a7a15a621ea856bfd548097dbaeb43c", 802747, "int8/"),
            new SpeechModelFile("vocab.json", "e7f1f7698021594ff6cf4dc766b8644a97a9fdbc", 2510527, "int8/"),
            new SpeechModelFile("config.json", "7f7a1ec8d7a8185c60c1ab57ca3072b02bea2e8f", 1018, "int8/"),
        }),
        ["zle-en"] = new("opus-mt-tc-big-zle-en", "Opus-MT big Russian and Ukrainian to English (translation)",
            "https://huggingface.co/TigreGotico/opus-mt-tc-big-zle-en-onnx/resolve/8e66bdc2a181d7f21f65100fd215c7755a6a76b2/", new[]
        {
            new SpeechModelFile("encoder_model.onnx", "fde62b663c3bf7ffe62e761b540e10cc4870b7b40ee5545b8df01320140a0a2e", 139490280, "int8/"),
            new SpeechModelFile("decoder_model.onnx", "c45b1075a7a18e471cb03dde762bc1cd83c277f4f0a2f452411966f9ab866d32", 227740288, "int8/"),
            new SpeechModelFile("source.spm", "a982dbb9362861151e36b0db1595b324cd1ce09acf46ce1f4d6d624e11c5807f", 1016851, "int8/"),
            new SpeechModelFile("vocab.json", "30bdda6dc10fd40950b95485dd05ec4178c11f69", 2492486, "int8/"),
            new SpeechModelFile("config.json", "44c38b6bd5cc7f0b7e76611553077bbd7d4a6331", 1018, "int8/"),
        }),
        ["en-de"] = new("opus-mt-en-de", "Opus-MT English to German (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-de/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "15834b45fabd2dfb8c6c029b3ca3e7289aeefd90ece798ce42bcf548d1bd3b8d", 49366942, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "75ef79aa9bde9e3dce9ca584c29507be5f464973f6c600c89ff419bc8de29ebc", 56281702, "onnx/"),
            new SpeechModelFile("source.spm", "e73d58f99c702eb1746e3dc55bc76255d7aa4a94", 768489, ""),
            new SpeechModelFile("vocab.json", "13d464a333d4582c869c0a46a69b78064c6eb418", 1389436, ""),
            new SpeechModelFile("config.json", "188a79d8e9befa9bce0a1e4648a5a21db0ac34f3", 1411, ""),
        }),
        ["en-fr"] = new("opus-mt-en-fr", "Opus-MT English to French (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-fr/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "0a81bdba62f53223740a8c6c6f58e716eba8ea1f92dfa569caf27ed5e3b6a0f7", 50090398, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "bdacc5f38e4d2b805a2fc9f38cf43844cdea56666e735137dbacf2a54a785257", 57010810, "onnx/"),
            new SpeechModelFile("source.spm", "5b989d2ad3cc6596bef819b2ad755f3e06377476", 778395, ""),
            new SpeechModelFile("vocab.json", "80a0a4d0f2a21df372ac2595bbb348444d9fbb07", 1458196, ""),
            new SpeechModelFile("config.json", "0384b144d7dd05ba622f17ffb3e77a76f0dba743", 1411, ""),
        }),
        ["en-es"] = new("opus-mt-en-es", "Opus-MT English to Spanish (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-es/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "03434edd1147e6c49e22066e9dc5888235e99cc35a6711d1c4ea5b06455d5da9", 52899742, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "2b45074cc819f1d901e40510d7bc0781d99327d16c7eca04518c4ee0c3d6ba10", 59842102, "onnx/"),
            new SpeechModelFile("source.spm", "6dc92ba66940f622779f82c376066c2c46bbad12", 801636, ""),
            new SpeechModelFile("vocab.json", "b69bfd1b1a407f74476e0cb94ba67ed2aa4f6282", 1720044, ""),
            new SpeechModelFile("config.json", "1a12ace130d96e926d1d3ca34ba11c83e51cbcde", 1468, ""),
        }),
        ["en-it"] = new("opus-mt-en-it", "Opus-MT English to Italian (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-it/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "b51c5110c7ba356ef01ee400d427afe162e657de1d7a2e9a40deedd811b49e6f", 60597150, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "d5c77fd7f60eebdf81d3f6f6fecd8e6804529cbcbecc2bb742fd60f225e2cf3e", 67599646, "onnx/"),
            new SpeechModelFile("source.spm", "219b146e0c09ea818d574ca669819a3b06a6077f", 789468, ""),
            new SpeechModelFile("vocab.json", "69de29022e9c9761167e15089f6037d4a079b73d", 2511647, ""),
            new SpeechModelFile("config.json", "493415a1cf0731e06c5fddda4dfe00012d03e9ab", 1376, ""),
        }),
        ["en-cs"] = new("opus-mt-en-cs", "Opus-MT English to Czech (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-cs/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "5ca546c92abb310fc14e5dcfc421252a83b1595ca91d117cac9afda7ab7d7c38", 51623838, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "d6b2a757e26d10cc8f1a290174ab6655c623eeb11bdf71bdf44429f30a1cc164", 58556230, "onnx/"),
            new SpeechModelFile("source.spm", "a8dd5a57b64047544faa3dafb48b0a6e33fcf364", 788721, ""),
            new SpeechModelFile("vocab.json", "53c6ed4948f95a1cc87c5ee615537b5051ad4807", 1721470, ""),
            new SpeechModelFile("config.json", "65c95d9d5426f7401e735e7a96ee6b302389adb5", 1376, ""),
        }),
        ["en-ro"] = new("opus-mt-en-ro", "Opus-MT English to Romanian (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-ro/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "843d42689aeba847c71e88863d9d6fbba43cac21d115f375e90afc9922ceadc6", 50105246, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "750ae52709f955bc95aec9cf7a4ea3d9e5525fc164e58b36a3a6424bfafd921f", 57025774, "onnx/"),
            new SpeechModelFile("source.spm", "c372f65a89abd625c51df53e46a759d904bb9e55", 788572, ""),
            new SpeechModelFile("vocab.json", "da4e638f4161189b34097b7769008a8f80eb56fd", 1511210, ""),
            new SpeechModelFile("config.json", "63ba472726bf98250b69f7344a37d4e611d390cf", 1376, ""),
        }),
        ["en-hu"] = new("opus-mt-en-hu", "Opus-MT English to Hungarian (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-hu/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "f78d4af0a91fc534b0d0267e85fc7164d326a7b8fb18f46472e2f70d443230af", 51630494, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "e3ca003d6491c71a1b4d3682d642723e23debb5611e4ab194156e9fd05e8a7c0", 58562938, "onnx/"),
            new SpeechModelFile("source.spm", "8b386c97b4a0943aae225c2f72a6fe74abd87021", 792289, ""),
            new SpeechModelFile("vocab.json", "46b553a910c119d2f4cd2ebace2672081c4d29f5", 1690844, ""),
            new SpeechModelFile("config.json", "5e0452da14359c177878514e821610ac92075e22", 1376, ""),
        }),
        ["en-nl"] = new("opus-mt-en-nl", "Opus-MT English to Dutch (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-nl/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "0febb990f8ca8ed9fd3e0017dfcb55ec5293999f20ead81d2bb4370e9e3120ff", 53937565, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "e9d1474106c1bdf8aaf236eb7ace8a29edf6fee655c583132f521e5b63ccbf97", 60888033, "onnx/"),
            new SpeechModelFile("source.spm", "35d1d085b445c5450c96ca6cbccbfabc5d52a54d", 789525, ""),
            new SpeechModelFile("vocab.json", "a38d8fda948f5a6756041e1aae4c309d8b481fb2", 1794274, ""),
            new SpeechModelFile("config.json", "3eeb93a3605e2e166c3d4d47865cb4b744685bbf", 1376, ""),
        }),
        ["en-da"] = new("opus-mt-en-da", "Opus-MT English to Danish (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-da/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "0b607addd3d4eb4ab6560cbcdac6f0018bf90b52eee2069412ba9f4a2e00f2f6", 49791390, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "1a6f5af760f33efd9038bcb805165bf18945cc5eaa547cc69f80d94e627ff282", 56709466, "onnx/"),
            new SpeechModelFile("source.spm", "79d2e978f859f6a02c5e227551bfb9eea2033951", 788462, ""),
            new SpeechModelFile("vocab.json", "a1a9f9ed459e5f7c820ec27907181e84981b592b", 1499868, ""),
            new SpeechModelFile("config.json", "145bc0f88b9ae454e44b008a40bc9ab3aa7a0f3d", 1376, ""),
        }),
        ["en-sv"] = new("opus-mt-en-sv", "Opus-MT English to Swedish (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-sv/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "2477c172f2001ee486f2d21e452c4d74838af973dfb58d00cf53632c2e3f3dba", 48513438, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "953946225fb335da55b2d0568ae55cc30bacaf598920055c3cacf199e785cb6c", 55421530, "onnx/"),
            new SpeechModelFile("source.spm", "9d5442970b683e25b2186d84308ac46f690497af", 790085, ""),
            new SpeechModelFile("vocab.json", "8d44dc27bbb2d470366b64ffc70103c80a29e7e0", 1404356, ""),
            new SpeechModelFile("config.json", "4cbf1f4b2c5ad5f1deceea69d28e277bfc3df645", 1376, ""),
        }),
        ["en-fi"] = new("opus-mt-en-fi", "Opus-MT English to Finnish (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-fi/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "874d962be7b3d5dadae3e98a156b2df0344febaa17809f6f0411ce23a59ff4c3", 52899742, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "4c8c3a0502f385dce8dd2a6b4546aaa69423c7097b5f0b0c77c3f097ce68ddde", 59842102, "onnx/"),
            new SpeechModelFile("source.spm", "0c7b9a1cc89a058b47b5dada34e0fb1558ee46f9", 803110, ""),
            new SpeechModelFile("vocab.json", "20fd0a6b71cf3a49582bcf82d89e15329493ff2e", 1722260, ""),
            new SpeechModelFile("config.json", "8a4e923dbb7e0bb3f08899b9743a957be07e1f55", 1356, ""),
        }),
        ["en-ar"] = new("opus-mt-en-ar", "Opus-MT English to Arabic (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-ar/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "53c354d87ced44b39085fe8a46a25f09ea4578d442dbce9663f3977228f8a04e", 51773854, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "94e6bdf90866151787d014b7b77be0adb0f036418ee04d235ec01238aa8426b9", 58707418, "onnx/"),
            new SpeechModelFile("source.spm", "8349b4b7a60eb336d5993893ba39b4faa5866926", 801074, ""),
            new SpeechModelFile("vocab.json", "5688ada4beced5dad087c52ac37c7a8cbd364816", 2243581, ""),
            new SpeechModelFile("config.json", "c4756a24ae0f5ceae268ac6c4d43ee8d4ffdd259", 1389, ""),
        }),
        ["en-hi"] = new("opus-mt-en-hi", "Opus-MT English to Hindi (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-hi/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "3045cd6eaf06db96cff853204d6f0ffeb0c5af238a4f5a87a19fcaa94f7493c4", 51337630, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "f563f658721046b17aa3f3ec33d12efdb1497c962876ff7d7a5ca513f6b49f4e", 58267786, "onnx/"),
            new SpeechModelFile("source.spm", "0a43eed0013baaa1da214d0b130fe64857f13f58", 812240, ""),
            new SpeechModelFile("vocab.json", "5305c4f55b1cf13c320f4c303e728f40c12e7da2", 2221738, ""),
            new SpeechModelFile("config.json", "47a20aef8837b30a1cf9e653bf4c0af0bea8f0d1", 1389, ""),
        }),
        ["en-id"] = new("opus-mt-en-id", "Opus-MT English to Indonesian (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-id/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "9bfc9cf0f400f86ec11da24b2f56380fcfabab17fb2dbcf7bb4c1754b7076e24", 47674781, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "d46dea5d13bec5ed7552baa5c0917e0d3dfa32e14fb7f09eae534c50d0cfb42f", 54576321, "onnx/"),
            new SpeechModelFile("source.spm", "eface9e96b0165a12b8e484ea7398d2d6c799747", 795925, ""),
            new SpeechModelFile("vocab.json", "324d302d0e75c102d14d227f175c87df56819009", 1366984, ""),
            new SpeechModelFile("config.json", "78aaa978a38001054c7250a874bd4f5cec630967", 1376, ""),
        }),
        ["en-vi"] = new("opus-mt-en-vi", "Opus-MT English to Vietnamese (translation)",
            "https://huggingface.co/Xenova/opus-mt-en-vi/resolve/main/", new[]
        {
            new SpeechModelFile("encoder_model_quantized.onnx", "dfcd7a32be3fdd21cbcf5d354520d626f1768fd6e205ca62a6a11a4641d31f36", 47105950, "onnx/"),
            new SpeechModelFile("decoder_model_quantized.onnx", "cde3e02aaca72cfaf2267f6c31c4dcc1b323d769bf90c273f2a1c1d56198ddba", 54003046, "onnx/"),
            new SpeechModelFile("source.spm", "d577dd343bb74b9ef8f85676df0ccc52e2f2b9ee", 808612, ""),
            new SpeechModelFile("vocab.json", "2b8dc2b4537510467ef422017be93f30fdf09435", 1299764, ""),
            new SpeechModelFile("config.json", "86ba342d2fcd025ca82afec6169c2e96dd1f761c", 1389, ""),
        }),
    };

    // The owner (2026-11-05: the base models translated Russian and English badly): Russian and Ukrainian go through Helsinki-NLP's big
    // East Slavic models, which take the target language's token in front of the text
    private static readonly Dictionary<string, string> EastSlavic = new() { ["ru"] = ">>rus<<", ["uk"] = ">>ukr<<" };

    private static string FromEnglish(string target) => EastSlavic.ContainsKey(target) ? "en-zle" : "en-" + target;

    /// <summary>The token a language pair needs in front of the text for <paramref name="target"/>, or null.</summary>
    public static string TargetToken(string pair, string target) => pair == "en-zle" ? EastSlavic[target] : null;

    /// <summary>The languages chat can be translated into: English and each with an English-to model, in the speech languages' order.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> Targets =
        SpeechLanguages.All.Where(l => l.Code == "en" || Models.ContainsKey(FromEnglish(l.Code))).ToList();

    public static bool IsTarget(string code) => Targets.Any(t => t.Code == code);

    /// <summary>
    /// The models <paramref name="target"/> needs: any language to English, the big Russian and Ukrainian to English unless the target is
    /// one of them, and English to the target unless it is English.
    /// </summary>
    public static IReadOnlyList<SpeechModel> ModelsFor(string target)
    {
        var pairs = new List<string> { "mul-en" };
        if (!EastSlavic.ContainsKey(target))
            pairs.Add("zle-en");
        if (target != "en")
            pairs.Add(FromEnglish(target));
        return pairs.Select(p => Models[p]).ToList();
    }

    private static readonly IReadOnlyList<string> Codes = SpeechLanguages.All.Select(l => l.Code).ToList();

    /// <summary>
    /// The language pairs that take <paramref name="text"/> into <paramref name="target"/>: none when it is already in it (or has no
    /// letters), else to English unless it is English (Russian and Ukrainian by the big model, but between the two by the any-language
    /// one, which the target already has), then from English unless the target is English.
    /// </summary>
    public static IReadOnlyList<string> Route(string text, string target)
    {
        string source = SpeechLanguages.Detect(text, Codes);
        if (source == null || source == target)
            return Array.Empty<string>();
        var pairs = new List<string>();
        if (source != "en")
            pairs.Add(EastSlavic.ContainsKey(source) && !EastSlavic.ContainsKey(target) ? "zle-en" : "mul-en");
        if (target != "en")
            pairs.Add(FromEnglish(target));
        return pairs;
    }

    private static readonly Regex SentenceEnd = new(@"(?<=[.!?…])\s+", RegexOptions.CultureInvariant);

    /// <summary>The sentences of a line: an Opus-MT model translates one sentence and drops the rest.</summary>
    public static string[] Sentences(string text) => SentenceEnd.Split(text.Trim()).Where(s => s.Length > 0).ToArray();

    /// <summary>Two texts with the same letters and digits, case aside (a translation that changed nothing is not shown).</summary>
    public static bool Same(string a, string b) => Letters(a) == Letters(b);

    /// <summary>
    /// <paramref name="quote"/> is one of the player's own lines: its letters are in one they <paramref name="sent"/> (which still holds the
    /// say prefix: ";", ":h", "#").
    /// </summary>
    public static bool Mine(string quote, IEnumerable<string> sent)
    {
        string heard = Letters(quote);
        return heard.Length > 0 && sent.Any(s => Letters(s).Contains(heard, StringComparison.Ordinal));
    }

    private static string Letters(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// The words a player wrote in a chat line of <paramref name="type"/>: a local, radio or dead chat line's quote (tg's say_quote:
    /// <c>Name says, "words"</c>) or an OOC line's message (<c>OOC: Key: words</c>).
    /// </summary>
    public static bool TryWords(string type, string plain, out int start, out int length)
    {
        start = length = 0;
        if (string.IsNullOrEmpty(plain))
            return false;
        if (type is ChatLog.TypeLocalChat or ChatLog.TypeRadio or ChatLog.TypeDeadChat)
        {
            int open = plain.IndexOf('"'), close = plain.LastIndexOf('"');
            if (open < 0 || close <= open + 1)
                return false;
            (start, length) = (open + 1, close - open - 1);
            return true;
        }
        if (type == ChatLog.TypeOoc)
        {
            int prefix = plain.IndexOf(": ", StringComparison.Ordinal);
            int key = prefix < 0 ? -1 : plain.IndexOf(": ", prefix + 2, StringComparison.Ordinal);
            if (key < 0 || key + 2 >= plain.Length)
                return false;
            (start, length) = (key + 2, plain.Length - key - 2);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The words in a runechat's maptext (tg's chatmessage: <c>&lt;span style='color: …'&gt;&lt;span class='center …'&gt;words&lt;/span&gt;&lt;/span&gt;</c>,
    /// the words HTML with tg's emphasis tags in them); an emote's is not taken.
    /// </summary>
    public static bool TryMaptextWords(string maptext, out int start, out int length)
    {
        start = length = 0;
        int classes = maptext?.IndexOf("class='center", StringComparison.Ordinal) ?? -1;
        int open = classes < 0 ? -1 : maptext.IndexOf('>', classes);
        if (open < 0 || maptext.AsSpan(classes, open - classes).Contains("emote", StringComparison.Ordinal))
            return false;
        int close = maptext.IndexOf("</span>", open, StringComparison.Ordinal);
        if (close <= open + 1)
            return false;
        (start, length) = (open + 1, close - open - 1);
        return true;
    }

    /// <summary>How big and how opaque the original line under a translation is drawn (the owner, 2026-11-05: set apart from the translation).</summary>
    public const float OriginalScale = 0.7f, OriginalAlpha = 0.55f;

    /// <summary>A style of the original line: smaller and fainter.</summary>
    public static ChatStyle Faded(ChatStyle style) =>
        style with { Scale = style.Scale * OriginalScale, Rgba = (style.Rgba & 0xFFFFFF00) | (uint)MathF.Round((style.Rgba & 0xFF) * OriginalAlpha) };

    /// <summary>
    /// The translated line: <paramref name="plain"/> with its words (<paramref name="start"/>, <paramref name="length"/>) replaced by
    /// <paramref name="translated"/>, its styles and links moved to fit (a link in the words is gone), then on the next line the original,
    /// in its styles <see cref="Faded"/> and without links.
    /// </summary>
    public static (string Plain, List<ChatSpan> Spans, List<ChatLink> Links) Compose(string plain, IReadOnlyList<ChatSpan> spans,
        IReadOnlyList<ChatLink> links, int start, int length, string translated)
    {
        int end = start + length, delta = translated.Length - length;
        string main = plain[..start] + translated + plain[end..];
        // an edge before the words stays, one after them moves by the change in length, one inside them goes to their start or new end
        int Move(int at, bool opening) => at <= start ? at : at >= end ? at + delta : opening ? start : start + translated.Length;
        var styled = new List<ChatSpan>();
        foreach (ChatSpan span in spans ?? Array.Empty<ChatSpan>())
        {
            int from = Move(span.Start, true), to = Move(span.Start + span.Length, false);
            if (to > from)
                styled.Add(span with { Start = from, Length = to - from });
        }
        var linked = (links ?? Array.Empty<ChatLink>()).Where(l => l.Start + l.Length <= start || l.Start >= end)
            .Select(l => l.Start >= end ? l with { Start = l.Start + delta } : l).ToList();
        int origin = main.Length + 1;
        int at = 0;
        foreach (ChatSpan span in (spans ?? Array.Empty<ChatSpan>()).OrderBy(s => s.Start))
        {
            if (span.Start > at)
                styled.Add(new ChatSpan(origin + at, span.Start - at, Faded(ChatStyles.Base)));
            styled.Add(new ChatSpan(origin + span.Start, span.Length, Faded(span.Style)));
            at = Math.Max(at, span.Start + span.Length);
        }
        if (at < plain.Length)
            styled.Add(new ChatSpan(origin + at, plain.Length - at, Faded(ChatStyles.Base)));
        return (main + "\n" + plain, styled, linked);
    }
}
