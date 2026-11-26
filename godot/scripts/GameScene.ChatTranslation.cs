using System.Net;
using System.Threading.Tasks;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Speech;
using Anomaly.Client.Speech;
using Anomaly.Protocol;

namespace Anomaly.Client;

// D319: the chat translator (Shared/Speech/ChatTranslation): another player's words are translated off the main thread, then the line
// in the chat window and in the feed shows the translation with the original under it, the text over the speaker's head shows the
// translation alone, and text to speech voices the translation alone (the owner, 2026-11-05).
public partial class GameScene
{
    private ChatTranslator _translator;
    private readonly List<string> _sentLines = new();

    // the player's own lines, kept a while so their echo in the chat is not translated
    private void Say(SayOut message)
    {
        if (!string.IsNullOrEmpty(message.Entry))
        {
            _sentLines.Add(message.Entry);
            if (_sentLines.Count > 8)
                _sentLines.RemoveAt(0);
        }
        _session?.Say(message);
    }

    // DEVIATION(DV598): another player's OOC line pings softly (tg's bloop, the OOC announcement's sound), at the Audio tab's volume
    private void PingOoc(ChatEntry entry)
    {
        if (entry?.Type != ChatLog.TypeOoc || _options.OocPingVolumePercent <= 0)
            return;
        if (ChatTranslation.TryWords(entry.Type, entry.Plain, out int start, out int length) && ChatTranslation.Mine(entry.Plain.Substring(start, length), _sentLines))
            return;
        _sound?.PlayOwn("sound/misc/bloop.ogg", 25f * _options.OocPingVolumePercent / 100f, vary: false);
    }

    /// <summary>The translator is on and <paramref name="words"/> are not in its language (nor the player's own line).</summary>
    private bool WillTranslate(string words) =>
        _options?.ChatTranslation == true && !ChatTranslation.Mine(words, _sentLines) && ChatTranslation.Route(words, _options.ChatTranslationLanguage).Count > 0;

    /// <summary>The words in the translator's language, or null (no translation: the models are not there yet, it failed, nothing changed).</summary>
    private async Task<string> Translated(string words)
    {
        string target = _options.ChatTranslationLanguage;
        foreach (SpeechModel model in ChatTranslation.ModelsFor(target))
        {
            if (!SpeechStore.IsInstalled(model))
            {
                SpeechDownloads.Ensure(model); // the option is on and the files are not there (yet): fetch them
                return null;
            }
        }
        _translator ??= new ChatTranslator();
        string translated;
        try
        {
            translated = await _translator.Translate(words, target);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[anomaly] chat translation failed: {e.Message}");
            return null;
        }
        return string.IsNullOrWhiteSpace(translated) || ChatTranslation.Same(translated, words) || !IsInstanceValid(this) ? null : translated;
    }

    private async void TranslateChat(ChatEntry entry, ChatFeed.Line feedLine)
    {
        if (entry == null || !ChatTranslation.TryWords(entry.Type, entry.Plain, out int start, out int length))
            return;
        string words = entry.Plain.Substring(start, length);
        if (!WillTranslate(words) || await Translated(words) is not string translated)
            return;
        var (plain, spans, links) = ChatTranslation.Compose(entry.Plain, entry.Spans, entry.Links, start, length, translated);
        _chatLog.Retext(entry, plain, spans, links);
        _chat.Retext(feedLine, plain, links, spans);
    }

    // the text over another speaker's head waits for its translation (a fraction of a second) and shows it alone
    private async void OnRunechat(RunechatMessage message)
    {
        string maptext = message.Maptext;
        if (message.Target != _feed?.OwnEntity && ChatTranslation.TryMaptextWords(maptext, out int start, out int length))
        {
            string words = HtmlText.ToPlain(maptext.Substring(start, length));
            if (WillTranslate(words) && await Translated(words) is string translated)
                maptext = maptext[..start] + WebUtility.HtmlEncode(translated) + maptext[(start + length)..];
        }
        _runechat?.Add(message.Target, maptext, message.Lifespan, App.NowSeconds);
    }

    // text to speech voices another speaker's translated line alone, in the translator's language; untranslated, the original as before
    private async void SpeakTranslated(TtsMessage message)
    {
        if (await Translated(message.Text) is string translated)
            Speak(message.Speaker, translated, _options.ChatTranslationLanguage, message.Voice, message.Pitch, message.Filter, message.Flags, message.Volume);
        else if (SpeechLanguages.Detect(message.Text, _options.SpeechLanguageCodes) is string language)
            Speak(message.Speaker, message.Text, language, message.Voice, message.Pitch, message.Filter, message.Flags, message.Volume);
    }

    private void TeardownChatTranslation()
    {
        _translator?.Dispose();
        _translator = null;
    }
}
