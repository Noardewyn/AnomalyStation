using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Speech;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Speech;

/// <summary>D307: the client's speech catalogue: the line's language out of the player's, the voices, tg's falloff, and the options.</summary>
public class SpeechModelsTests
{
    private static readonly string[] EnRu = { "en", "ru" };

    [Theory]
    [InlineData("Get the captain to the bridge!", "en")]
    [InlineData("Позовите врача в медотсек!", "ru")]
    [InlineData("Hey, капитан!", "ru")] // more Cyrillic than Latin
    [InlineData("123 !!", null)] // no letters
    [InlineData("Ελάτε εδώ", null)] // Greek is not picked
    public void TheLanguageIsOneThePlayerPicked(string text, string expected) => Assert.Equal(expected, SpeechLanguages.Detect(text, EnRu));

    [Fact]
    public void TelltaleLettersChooseAmongTheScriptsLanguages()
    {
        Assert.Equal("uk", SpeechLanguages.Detect("Привіт, як справи? Їжа є", new[] { "ru", "uk" }));
        Assert.Equal("ru", SpeechLanguages.Detect("Привет, как дела? Это мы", new[] { "uk", "ru" }));
        Assert.Equal("de", SpeechLanguages.Detect("Schöne Grüße aus der Küche", new[] { "en", "de" }));
        Assert.Equal("en", SpeechLanguages.Detect("Plain words", new[] { "en", "de" }));
        Assert.Null(SpeechLanguages.Detect("Plain words", new[] { "ru" }));
    }

    [Theory]
    // D310: no telltale letter, the common words decide
    [InlineData("Ich bin hier und das ist mein Platz", "de")]
    [InlineData("Je suis ici avec mon ami", "fr")]
    [InlineData("Yo no tengo una para el capitan", "es")]
    [InlineData("Ik ben hier met mijn vriend", "nl")]
    [InlineData("I am here with my friend", "en")]
    public void CommonWordsChooseAmongLatinLanguages(string text, string expected) =>
        Assert.Equal(expected, SpeechLanguages.Detect(text, new[] { "en", "de", "fr", "es", "nl" }));

    [Theory]
    // D312: speech to text decodes only a picked language: the heard one, one of its script, the last phrase's or the first
    [InlineData("ru", null, "ru")]
    [InlineData("uk", null, "ru")] // Cyrillic
    [InlineData("pl", "en", "ru")] // Slavic: Russian heard as Polish
    [InlineData("fr", "ru", "en")] // Latin
    [InlineData("zh", "ru", "ru")] // the last phrase's
    [InlineData("zh", null, "en")] // the first picked
    [InlineData(null, null, "en")]
    public void SpeechIsRecognizedInAPickedLanguage(string heard, string last, string expected) =>
        Assert.Equal(expected, SpeechLanguages.Closest(heard, EnRu, last));

    [Fact]
    public void TheVoicesAreTheServersInTheModelsOrder()
    {
        Assert.Equal(datum_controller_subsystem_tts.PortSpeakers, SpeechVoices.Names);
        Assert.Equal(0, SpeechVoices.Sid("Woman 1"));
        Assert.Equal(9, SpeechVoices.Sid("Man 5"));
        int unknown = SpeechVoices.Sid("Male 01"); // a VCTK name from a tg save: a steady voice
        Assert.InRange(unknown, 0, 9);
        Assert.Equal(unknown, SpeechVoices.Sid("Male 01"));
    }

    [Fact]
    public void PitchAndFalloffAreTgs()
    {
        Assert.Equal(2f, SpeechVoices.PitchScale(12), 3);
        Assert.Equal(0.5f, SpeechVoices.PitchScale(-12), 3);
        Assert.Equal(0.85f, SpeechVoices.Gain(85, 0), 3);
        Assert.Equal(0f, SpeechVoices.Gain(85, 15), 3); // SOUND_RANGE
        float three = SpeechVoices.Gain(85, 3);
        Assert.Equal(0.85f * (1f - MathF.Pow(0.2f, 0.4f)), three, 4); // 85 * (1 - (3/15)^(1/2.5))
        // D312: a whisper fades out at 4 tiles, a shout carries twice as far as a line
        Assert.Equal(0f, SpeechVoices.Gain(50, 4, SpeechVoices.RangeOf(TtsFlags.Whisper)), 3);
        Assert.True(SpeechVoices.Gain(100, 14, SpeechVoices.RangeOf(TtsFlags.Yell)) > 0.2f);
        Assert.Equal(SpeechVoices.SoundRange, SpeechVoices.RangeOf(TtsFlags.Radio));
    }

    [Fact]
    public void TheOptionsSaveAndLoad()
    {
        var options = new PlayOptions { TextToSpeech = true, SpeechToText = true, SpeechVolumePercent = 40, SpeechLanguageCodes = new() { "ru", "de" } };
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(options.ToText(), errors);
        Assert.Empty(errors);
        Assert.True(back.TextToSpeech);
        Assert.True(back.SpeechToText);
        Assert.Equal(40, back.SpeechVolumePercent);
        Assert.Equal(new[] { "ru", "de" }, back.SpeechLanguageCodes);
        Assert.Equal(new[] { "en", "ru" }, new PlayOptions().SpeechLanguageCodes);
        Assert.False(new PlayOptions().TextToSpeech); // opt-in: no model is downloaded unasked
        Assert.Equal(new[] { "N" }, KeyBindings.Defaults().KeysOf(PlayAction.PushToTalk));
    }

    [Fact]
    public void SpeechToTextKeepsToTheChosenLanguage()
    {
        // D313: one chosen language while it is picked; Auto (empty) or a language no longer picked: every picked one
        var options = new PlayOptions { SpeechLanguageCodes = new() { "en", "ru" } };
        Assert.Equal(new[] { "en", "ru" }, options.RecognitionLanguages());
        options.SpeechToTextLanguage = "ru";
        Assert.Equal(new[] { "ru" }, options.RecognitionLanguages());
        var errors = new List<string>();
        Assert.Equal("ru", PlayOptions.Parse(options.ToText(), errors).SpeechToTextLanguage);
        Assert.Empty(errors);
        options.SpeechLanguageCodes.Remove("ru");
        Assert.Equal(new[] { "en" }, options.RecognitionLanguages());
        options.SpeechLanguageCodes.Clear();
        Assert.Equal(SpeechLanguages.Defaults, options.RecognitionLanguages());
    }

    [Fact]
    public void TheModelsAreCompleteWithTheirHashes()
    {
        foreach (SpeechModel model in new[] { SpeechModel.Tts, SpeechModel.Stt })
        {
            Assert.All(model.Files, f => Assert.Matches("^[0-9a-f]{64}$", f.Sha256));
            Assert.StartsWith("https://", model.BaseUrl);
        }
        Assert.Contains(SpeechModel.Tts.Files, f => f.Name == "LICENSE"); // OpenRAIL-M travels with the model
    }
}
