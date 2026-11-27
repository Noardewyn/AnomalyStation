// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/passworddoor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the password door, a blast door that opens to its password typed into tg's text prompt or, for the voice
// door, said nearby (its opening and closing states, the blast door's sound and the deny buzz). The var blocks are generated.

// tg: code/game/machinery/doors/passworddoor.dm:1
public partial class obj_machinery_door_password : obj_machinery_door
{
    // tg: code/game/machinery/doors/passworddoor.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(voice_activated))
            become_hearing_sensitive();
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:44
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(password));
        return dot;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:47
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (!Dm.truthy(density) || !Dm.truthy(voice_activated) || Dm.truthy(radio_freq))
            return dot;
        if (Dm.truthy(findtext(raw_message, password)))
            await open(opener: speaker as mob_living);
        return dot;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:54
    public override object Bumped(atom_movable AM = null)
    {
        if (Dm.truthy(density))
            return FALSE;
        return base.Bumped(AM);
    }

    // tg: code/game/machinery/doors/passworddoor.dm:57
    public override async DmTask<object> try_to_activate_door(mob user = null, float? access_bypass = null, float? bumped = null)
    {
        access_bypass ??= FALSE;
        bumped ??= FALSE;
        add_fingerprint(user);
        if (Dm.truthy(operating))
            return null;
        if (Dm.truthy(density))
        {
            if (Dm.truthy(access_bypass) || Dm.truthy(await ask_for_pass(user)))
                await open(opener: user as mob_living);
            else
                await run_animation(DOOR_DENY_ANIMATION);
        }
        return null;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:67
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        //Deny animation would be nice to have.
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            icon_state = "opening";
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            icon_state = "closing";
        else
            icon_state = Dm.truthy(density) ? "closed" : "open";
        return dot;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:78
    public override object animation_length(object animation = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_ANIMATION))
            return 1.1f * SECONDS;
        if (Dm.eq(animation, DOOR_CLOSING_ANIMATION))
            return 1.1f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:85
    public override object animation_segment_delay(object animation = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_PASSABLE))
            return 0.5f * SECONDS;
        if (Dm.eq(animation, DOOR_OPENING_FINISHED))
            return 1.1f * SECONDS;
        if (Dm.eq(animation, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        if (Dm.eq(animation, DOOR_CLOSING_FINISHED))
            return 1.1f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:96
    public override async DmTask animation_effects(object animation = null, float? force_type = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_ANIMATION))
            playsound(this, door_open, 50, TRUE);
        else if (Dm.eq(animation, DOOR_CLOSING_ANIMATION))
            playsound(this, door_close, 50, TRUE);
        else if (Dm.eq(animation, DOOR_DENY_ANIMATION))
            playsound(this, door_deny, 30, TRUE);
        await DmTask.CompletedTask;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:105
    public virtual async DmTask<object> ask_for_pass(mob user = null)
    {
        object guess = await tgui_input_text(user, I18n.T("Enter the password"), I18n.T("Password"), max_length: MAX_MESSAGE_LEN);
        if (Dm.eq(guess, password))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/doors/passworddoor.dm:111
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}
