// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/charter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The admins' reject link is admin tooling (a row); a name waits for its timer and is approved, as tg's with no admin about. The var blocks are generated.

// tg: code/game/objects/items/charter.dm:2
public partial class obj_item_station_charter : obj_item
{
    // tg: code/game/objects/items/charter.dm:15  var/static/regex/standard_station_regex
    private static ref regex standard_station_regex => ref TgState.Current.obj_item_station_charter__standard_station_regex;

    // tg: code/game/objects/items/charter.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (standard_station_regex == null)
        {
            object prefixes = jointext(GLOB.station_prefixes, "|");
            object names = jointext(GLOB.station_names, "|");
            object suffixes = jointext(GLOB.station_suffixes, "|");
            object numerals = jointext(GLOB.station_numerals, "|");
            string regexstr = Dm.text("^(([]) )?(([]) ?)([]) ([])$", prefixes, names, suffixes, numerals);
            standard_station_regex = Dm.regex(regexstr);
        }
        return dot;
    }

    // tg: code/game/objects/items/charter.dm:27
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(used))
        {
            to_chat(user, span_warning(I18n.T("The [] has already been named!", name_type)));
            return null;
        }
        if (!Dm.truthy(ignores_timeout) && (Dm.num(world.time) - SSticker.round_start_time > STATION_RENAME_TIME_LIMIT)) //5 minutes
        {
            to_chat(user, span_warning(I18n.T("The crew has already settled into the shift. It probably wouldn't be good to rename the [] right now.", name_type)));
            return null;
        }
        if (Dm.truthy(response_timer_id))
        {
            to_chat(user, span_warning(I18n.T("You're still waiting for approval from your employers about your proposed name change, it'd be best to wait for now.")));
            return null;
        }

        object new_name = await tgui_input_text(user, I18n.T("What do you want to name []? Keep in mind particularly terrible names may be rejected by your employers, while names using the standard format will be accepted automatically.", station_name()), I18n.T("Station Name"), max_length: MAX_CHARTER_LEN);

        if (Dm.truthy(response_timer_id))
        {
            to_chat(user, span_warning(I18n.T("You're still waiting for approval from your employers about your proposed name change, it'd be best to wait for now.")));
            return null;
        }

        if (!Dm.truthy(new_name))
            return null;
        user.log_message(Dm.text("has proposed to name the station as []", new_name), LOG_GAME);

        if (Dm.truthy(standard_station_regex.Find(new_name)))
        {
            to_chat(user, span_notice(I18n.T("Your name has been automatically approved.")));
            await rename_station(new_name, user.name, user.real_name, key_name(user));
            return null;
        }

        to_chat(user, span_notice(I18n.T("Your name has been sent to your employers for approval.")));
        // Autoapproves after a certain time
        response_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(rename_station), new_name, user.name, user.real_name, key_name(user)), approval_time, TIMER_STOPPABLE, file: "code/game/objects/items/charter.dm", line: 59);
        Dm.Deferred("code/game/objects/items/charter.dm:60", "attack_self: the message to the admins with the REJECT link and the adminhelp sound (admin tooling; the name is approved by its timer)");
        return null;
    }

    // tg: code/game/objects/items/charter.dm:68
    public virtual void reject_proposed(object user = null)
    {
        if (!Dm.truthy(user))
            return;
        if (!Dm.truthy(response_timer_id))
            return;
        turf T = get_turf(this);
        T.visible_message(span_warning(I18n.T("The proposed changes disappear from []; it looks like they've been rejected.", this)));
        string m = Dm.text("[] has rejected the proposed station name.", key_name(user));

        message_admins(m);
        log_admin(m);

        deltimer(response_timer_id);
        response_timer_id = null;
    }

    // tg: code/game/objects/items/charter.dm:84
    public virtual async DmTask rename_station(object designation = null, object uname = null, object ureal_name = null, object ukey = null)
    {
        set_station_name(designation);
        minor_announce(I18n.T("[] has designated your station as []", ureal_name, html_decode(station_name())), I18n.T("Captain's Charter")); //decode station_name to avoid minor_announce double encode
        log_game(Dm.text("[] has renamed the station as [].", ukey, station_name()));

        name = I18n.T("station charter for []", station_name());
        desc = I18n.T("An official document entrusting the governance of [] and surrounding space to Captain [].", station_name(), uname);
        SSblackbox.record_feedback("text", "station_renames", 1, Dm.text("[]", station_name()));
        if (!Dm.truthy(unlimited_uses))
            used = TRUE;
    }
}

// tg: code/game/objects/items/charter.dm:102
public partial class obj_item_station_charter_banner : obj_item_station_charter
{
    // tg: code/game/objects/items/charter.dm:113
    public override async DmTask rename_station(object designation = null, object uname = null, object ureal_name = null, object ukey = null)
    {
        set_station_name(designation);
        minor_announce(I18n.T("[] has designated the [] as []", ureal_name, name_type, html_decode(station_name())), I18n.T("Captain's Banner")); //decode station_name to avoid minor_announce double encode
        log_game(Dm.text("[] has renamed the [] as [].", ukey, name_type, station_name()));
        name = I18n.T("banner of []", station_name());
        desc = I18n.T("The banner bears the official coat of arms of Nanotrasen, signifying that [] has been claimed by Captain [] in the name of the company.", station_name(), uname);
        SSblackbox.record_feedback("text", "station_renames", 1, Dm.text("[]", station_name()));
        if (!Dm.truthy(unlimited_uses))
            used = TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/charter.dm:15
    internal regex obj_item_station_charter__standard_station_regex;
}
