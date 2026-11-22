// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/priority_announce.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the announcements): the priority, minor and security level announcements and their dispatch, and the
// newscaster articles an announcement files (silicons SI6b). Left out with a comment: the command report's printout at the
// communications consoles (the communications console, M10). DEVIATION(DV47): the announcement sound preference is read as
// its default, on (the preferences are not translated).

public static partial class GlobalProcs
{
    // please don't use these defines outside of this file in order to ensure a unified framework. unless you have a really good reason to make them global, then whatever

    // these four are just text spans that furnish the TEXT itself with the appropriate CSS classes
    // tg: code/__HELPERS/priority_announce.dm:4  #define MAJOR_ANNOUNCEMENT_TITLE(string) (file-local)
    private static string MAJOR_ANNOUNCEMENT_TITLE(object @string) => "<span class='major_announcement_title'>" + Dm.str(@string) + "</span>";
    // tg: code/__HELPERS/priority_announce.dm:5  #define SUBHEADER_ANNOUNCEMENT_TITLE(string) (file-local)
    private static string SUBHEADER_ANNOUNCEMENT_TITLE(object @string) => "<span class='subheader_announcement_text'>" + Dm.str(@string) + "</span>";
    // tg: code/__HELPERS/priority_announce.dm:6  #define MAJOR_ANNOUNCEMENT_TEXT(string) (file-local)
    private static string MAJOR_ANNOUNCEMENT_TEXT(object @string) => "<span class='major_announcement_text'>" + Dm.str(@string) + "</span>";
    // tg: code/__HELPERS/priority_announce.dm:7  #define MINOR_ANNOUNCEMENT_TITLE(string) (file-local)
    private static string MINOR_ANNOUNCEMENT_TITLE(object @string) => "<span class='minor_announcement_title'>" + Dm.str(@string) + "</span>";
    // tg: code/__HELPERS/priority_announce.dm:8  #define MINOR_ANNOUNCEMENT_TEXT(string) (file-local)
    private static string MINOR_ANNOUNCEMENT_TEXT(object @string) => "<span class='minor_announcement_text'>" + Dm.str(@string) + "</span>";

    // tg: code/__HELPERS/priority_announce.dm:10  #define ANNOUNCEMENT_HEADER(string) (file-local)
    private static string ANNOUNCEMENT_HEADER(object @string) => "<span class='announcement_header'>" + Dm.str(@string) + "</span>";

    // these two are the ones that actually give the striped background
    // tg: code/__HELPERS/priority_announce.dm:13  #define CHAT_ALERT_DEFAULT_SPAN(string) (file-local)
    private static string CHAT_ALERT_DEFAULT_SPAN(object @string) => "<div class='chat_alert_default'>" + Dm.str(@string) + "</div>";
    // tg: code/__HELPERS/priority_announce.dm:14  #define CHAT_ALERT_COLORED_SPAN(color, string) (file-local)
    private static string CHAT_ALERT_COLORED_SPAN(object color, object @string) => "<div class='chat_alert_" + Dm.str(color) + "'>" + Dm.str(@string) + "</div>";

    /**
     * Make a big red text announcement to
     *
     * Formatted like:
     *
     * " Message from sender "
     *
     * " Title "
     *
     * " Text "
     *
     * Arguments
     * * text - required, the text to announce
     * * title - optional, the title of the announcement.
     * * sound - optional, the sound played accompanying the announcement
     * * type - optional, the type of the announcement, for some "preset" announcement templates. See __DEFINES/announcements.dm
     * * sender_override - optional, modifies the sender of the announcement
     * * has_important_message - is this message critical to the game (and should not be overridden by station traits), or not
     * * players - a list of all players to send the message to. defaults to all players (not including new players)
     * * encode_title - if TRUE, the title will be HTML encoded
     * * encode_text - if TRUE, the text will be HTML encoded
     */
    // tg: code/__HELPERS/priority_announce.dm:40
    public static void priority_announce(object text = null, object title = null, object sound = null, object type = null, object sender_override = null, float? has_important_message = null, DmList players = null, float? encode_title = null, float? encode_text = null, object color_override = null)
    {
        title ??= "";
        has_important_message ??= FALSE;
        players ??= GLOB.player_list;
        encode_title ??= TRUE;
        encode_text ??= TRUE;
        if (!Dm.truthy(text))
            return;

        if (Dm.truthy(encode_title) && Dm.truthy(title) && Dm.num(length(title)) > 0)
            title = Dm.html_encode(title);
        if (Dm.truthy(encode_text))
        {
            text = Dm.html_encode(text);
            if (!Dm.truthy(length(text)))
                return;
        }

        DmList announcement_strings = Dm.list();

        datum_centcom_announcer announcer = SSstation.announcer as datum_centcom_announcer;
        if (!Dm.truthy(sound))
            sound = announcer.get_rand_alert_sound();
        else if (Dm.truthy(announcer.event_sounds[sound]))
            sound = announcer.event_sounds[sound];

        object header = null;
        object sw__1 = type;
        if (Dm.eq(sw__1, ANNOUNCEMENT_TYPE_PRIORITY))
        {
            header = MAJOR_ANNOUNCEMENT_TITLE(I18n.T("Priority Announcement"));
            if (Dm.num(length(title)) > 0)
                header = Dm.add(header, SUBHEADER_ANNOUNCEMENT_TITLE(title));
        }
        else if (Dm.eq(sw__1, ANNOUNCEMENT_TYPE_CAPTAIN))
        {
            header = MAJOR_ANNOUNCEMENT_TITLE(I18n.T("Captain's Announcement"));
            GLOB.news_network.submit_article(text, "Captain's Announcement", NEWSCASTER_STATION_ANNOUNCEMENTS, null); // silicons SI6b
        }
        else if (Dm.eq(sw__1, ANNOUNCEMENT_TYPE_SYNDICATE))
            header = MAJOR_ANNOUNCEMENT_TITLE(I18n.T("Syndicate Captain's Announcement"));
        else
            header = Dm.add(header, generate_unique_announcement_header(title, sender_override));

        Dm.append(ref announcement_strings, ANNOUNCEMENT_HEADER(header));

        ///If the announcer overrides alert messages, use that message.
        if (Dm.truthy(announcer.custom_alert_message) && !Dm.truthy(has_important_message))
            Dm.append(ref announcement_strings, MAJOR_ANNOUNCEMENT_TEXT(announcer.custom_alert_message));
        else
            Dm.append(ref announcement_strings, MAJOR_ANNOUNCEMENT_TEXT(text));

        string finalized_announcement;
        if (Dm.truthy(color_override))
            finalized_announcement = CHAT_ALERT_COLORED_SPAN(color_override, jointext(announcement_strings, ""));
        else
            finalized_announcement = CHAT_ALERT_DEFAULT_SPAN(jointext(announcement_strings, ""));

        dispatch_announcement_to_players(finalized_announcement, players, sound);

        if (sender_override == null && players == GLOB.player_list)
        {
            // silicons SI6b
            if (Dm.num(length(title)) > 0)
                GLOB.news_network.submit_article(Dm.add(Dm.add(title, "<br><br>"), text), Dm.text("[]", command_name()), NEWSCASTER_STATION_ANNOUNCEMENTS, null);
            else
                GLOB.news_network.submit_article(text, Dm.text("[] Update", command_name()), NEWSCASTER_STATION_ANNOUNCEMENTS, null);
        }
    }

    /**
     * Print a report to all the communications consoles, and optionally send an announcement to players about it. This is used for the roundstart report, but can also be used for other reports in the future.
     *
     * * text - the text of the report to print
     * * title - the title of the report, which is also the name of the printed paper.
     * If null, defaults to "Classified [command_name()] Update"
     * * announce - whether or not to send an announcement to players about the report being printed.
     * Defaults to TRUE.
     * * contains_advanced_html - whether or not the text contains advanced HTML that should be rendered on the paper.
     * Advanced HTML (currently) only includes <img> tags, but may include other tags in the future.
     * Do not allow player inputted reports to contain advanced HTML.
     * Defaults to FALSE, which means only basic HTML will be rendered.
     */
    // tg: code/__HELPERS/priority_announce.dm:107
    public static void print_command_report(object text = null, object title = null, float? announce = null, float? contains_advanced_html = null)
    {
        text ??= "";
        announce ??= TRUE;
        contains_advanced_html ??= FALSE;
        if (!Dm.truthy(title))
            title = I18n.T("Classified [] Update", command_name());

        if (Dm.truthy(announce))
        {
            priority_announce(
                text: I18n.T("A report has been downloaded and printed out at all communications consoles."),
                title: I18n.T("Incoming Classified Message"),
                sound: ((datum_centcom_announcer)SSstation.announcer).get_rand_report_sound(),
                has_important_message: TRUE
            );
        }

        var message = Dm.@new<datum_comm_message>();
        message.title = title;
        message.content = text;

        GLOB.communications_controller.send_message(message, contains_advanced_html: contains_advanced_html);
    }

    /**
     * Sends a minor annoucement to players.
     * Minor announcements are large text, with the title in red and message in white.
     * Only mobs that can hear can see the announcements.
     *
     * message - the message contents of the announcement.
     * title - the title of the announcement, which is often "who sent it".
     * alert - whether this announcement is an alert, or just a notice. Only changes the sound that is played by default.
     * html_encode - if TRUE, we will html encode our title and message before sending it, to prevent player input abuse.
     * players - optional, a list mobs to send the announcement to. If unset, sends to all palyers.
     * sound_override - optional, use the passed sound file instead of the default notice sounds.
     * should_play_sound - Whether the notice sound should be played or not. This can also be a callback, if you only want mobs to hear the sound based off of specific criteria.
     * color_override - optional, use the passed color instead of the default notice color.
     */
    // tg: code/__HELPERS/priority_announce.dm:139
    public static void minor_announce(object message = null, object title = null, float? alert = null, float? html_encode = null, DmList players = null, object sound_override = null, object should_play_sound = null, object color_override = null)
    {
        title ??= "Attention:";
        alert ??= FALSE;
        html_encode ??= TRUE;
        should_play_sound ??= TRUE;
        if (!Dm.truthy(message))
            return;

        if (Dm.truthy(html_encode))
        {
            title = Dm.html_encode(title);
            message = Dm.html_encode(message);
        }

        DmList minor_announcement_strings = Dm.list();
        if (title != null && !Dm.eq(title, ""))
            Dm.append(ref minor_announcement_strings, ANNOUNCEMENT_HEADER(MINOR_ANNOUNCEMENT_TITLE(title)));
        Dm.append(ref minor_announcement_strings, MINOR_ANNOUNCEMENT_TEXT(message));

        string finalized_announcement;
        if (Dm.truthy(color_override))
            finalized_announcement = CHAT_ALERT_COLORED_SPAN(color_override, jointext(minor_announcement_strings, ""));
        else
            finalized_announcement = CHAT_ALERT_DEFAULT_SPAN(jointext(minor_announcement_strings, ""));

        object custom_sound = sound_override is var or__1 && Dm.truthy(or__1) ? or__1 : (Dm.truthy(alert) ? Dm.resource("sound/announcer/notice/notice1.ogg") : Dm.resource("sound/announcer/notice/notice2.ogg"));
        dispatch_announcement_to_players(finalized_announcement, players, custom_sound, should_play_sound);
    }

    /// Sends an announcement about the level changing to players. Uses the passed in datum and the subsystem's previous security level to generate the message.
    // tg: code/__HELPERS/priority_announce.dm:162
    public static void level_announce(datum_security_level selected_level = null, object previous_level_number = null)
    {
        object current_level_number = selected_level.number_level;
        object current_level_name = selected_level.name;
        object current_level_color = selected_level.announcement_color;
        object current_level_sound = selected_level.sound;

        string title;
        object message;

        if (Dm.gt(current_level_number, previous_level_number))
        {
            title = I18n.T("Attention! Security level elevated to []:", current_level_name);
            message = selected_level.elevating_to_announcement;
        }
        else
        {
            title = I18n.T("Attention! Security level lowered to []:", current_level_name);
            message = selected_level.lowering_to_announcement;
        }

        DmList level_announcement_strings = Dm.list();
        Dm.append(ref level_announcement_strings, ANNOUNCEMENT_HEADER(MINOR_ANNOUNCEMENT_TITLE(title)));
        Dm.append(ref level_announcement_strings, MINOR_ANNOUNCEMENT_TEXT(message));

        string finalized_announcement = CHAT_ALERT_COLORED_SPAN(current_level_color, jointext(level_announcement_strings, ""));

        dispatch_announcement_to_players(finalized_announcement, GLOB.player_list, current_level_sound);
    }

    /// Proc that just generates a custom header based on variables fed into `priority_announce()`
    /// Will return a string.
    // tg: code/__HELPERS/priority_announce.dm:188
    public static string generate_unique_announcement_header(object title = null, object sender_override = null)
    {
        DmList returnable_strings = Dm.list();
        if (sender_override == null)
            Dm.append(ref returnable_strings, MAJOR_ANNOUNCEMENT_TITLE(I18n.T("[] Update", command_name())));
        else
            Dm.append(ref returnable_strings, MAJOR_ANNOUNCEMENT_TITLE(sender_override));

        if (Dm.num(length(title)) > 0)
            Dm.append(ref returnable_strings, SUBHEADER_ANNOUNCEMENT_TITLE(title));

        return jointext(returnable_strings, "");
    }

    /// Proc that just dispatches the announcement to our applicable audience. Only the announcement is a mandatory arg.
    /// `should_play_sound` can also be a callback, if you want to only play the sound to specific players.
    // tg: code/__HELPERS/priority_announce.dm:202
    public static void dispatch_announcement_to_players(object announcement = null, DmList players = null, object sound_override = null, object should_play_sound = null)
    {
        players ??= GLOB.player_list;
        should_play_sound ??= TRUE;
        object sound_to_play = sound_override != null ? sound_override : Dm.resource("sound/announcer/notice/notice2.ogg");

        datum_callback should_play_sound_callback = should_play_sound as datum_callback;

        foreach (mob target in Dm.each<mob>(players))
        {
            if (isnewplayer(target) || HAS_TRAIT(target, TRAIT_DEAF))
                continue;

            to_chat(target, announcement);
            if (!Dm.truthy(should_play_sound) || (should_play_sound_callback != null && !Dm.truthy(Waiting.NoSleep(should_play_sound_callback.Invoke(target)))))
                continue;
            // DEVIATION(DV47): read_preference(/datum/preference/toggle/sound_announcements) is its default, on; a target without a
            // client reads null, as in tg
            if (target.client != null)
                Dm.output_to(target, sound(sound_to_play)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }
}
