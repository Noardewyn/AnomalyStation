// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/client_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /client/Click (:866-943) translated (M2 P4, scope 2f: the click entry point of a player), with the click
// limiter's file-local defines (:14-19); BYOND's own /client/Click that its ..() calls is the engine part's Click__d0
// (Builtin/client.builtin.cs). The rest of client_procs.dm is client_procs.cs (M1 P1).

// tg: code/modules/client/client_procs.dm:866
public partial class client : datum
{
    // tg: code/modules/client/client_procs.dm:14  #define LIMITER_SIZE 5 (file-local, #undef at the end of the file; the
    // topic limiter of client_procs.cs, not translated yet, uses the same five)
    private const int LIMITER_SIZE = 5;
    // tg: code/modules/client/client_procs.dm:15  #define CURRENT_SECOND 1
    private const int CURRENT_SECOND = 1;
    // tg: code/modules/client/client_procs.dm:16  #define SECOND_COUNT 2
    private const int SECOND_COUNT = 2;
    // tg: code/modules/client/client_procs.dm:17  #define CURRENT_MINUTE 3
    private const int CURRENT_MINUTE = 3;
    // tg: code/modules/client/client_procs.dm:18  #define MINUTE_COUNT 4
    private const int MINUTE_COUNT = 4;
    // tg: code/modules/client/client_procs.dm:19  #define ADMINSWARNED_AT 5
    private const int ADMINSWARNED_AT = 5;

    // tg: code/modules/client/client_procs.dm:866
    public virtual async DmTask Click(atom @object = null, atom location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(click_intercept_time))
        {
            if (click_intercept_time >= world.time)
            {
                click_intercept_time = 0; //Reset and return. Next click should work, but not this one.
                return;
            }
            click_intercept_time = 0; //Just reset. Let's not keep re-checking forever.
        }

        float ab = FALSE;
        DmList modifiers = params2list(@params);

        object button_clicked = LAZYACCESS(modifiers, "button");

        object dragged = LAZYACCESS(modifiers, DRAG);
        if (Dm.truthy(dragged) && !Dm.eq(button_clicked, dragged))
            return;

        if (Dm.truthy(@object) && IS_WEAKREF_OF(@object, middle_drag_atom_ref) && Dm.eq(button_clicked, LEFT_CLICK))
            ab = max(0, 5 * SECONDS - (world.time - middragtime) * 0.1f);

        object mcl = config.Get(typeof(datum_config_entry_number_minute_click_limit));
        if (!Dm.truthy(holder) && Dm.truthy(mcl))
        {
            float minute = round(world.time, 600);

            if (!Dm.truthy(clicklimiter))
                clicklimiter = Dm.list_n(LIMITER_SIZE);

            if (!Dm.eq(minute, clicklimiter[CURRENT_MINUTE]))
            {
                clicklimiter[CURRENT_MINUTE] = minute;
                clicklimiter[MINUTE_COUNT] = 0;
            }

            Dm.append_at(clicklimiter, MINUTE_COUNT, 1 + (ab));

            if (Dm.gt(clicklimiter[MINUTE_COUNT], mcl))
            {
                string msg = I18n.T("Your previous click was ignored because you've done too many in a minute.");
                if (!Dm.eq(minute, clicklimiter[ADMINSWARNED_AT])) //only one admin message per-minute. (if they spam the admins can just boot/ban them)
                {
                    clicklimiter[ADMINSWARNED_AT] = minute;

                    msg += I18n.T(" Administrators have been informed.");
                    // tg: :904-909  the aimbot notes and the admins' click-limit notice (key_name, ADMIN_LOOKUPFLW, ADMIN_KICK,
                    // add_system_note): admin tooling, M10
                    Dm.NotTranslated("code/modules/client/client_procs.dm:904", "client/Click: the admins' notice of a player over the per-minute click limit (ADMIN_LOOKUPFLW, ADMIN_KICK, add_system_note; admin tooling, M10+)");
                }
                to_chat(this, span_danger(I18n.T("[]", msg)));
                return;
            }
        }

        object scl = config.Get(typeof(datum_config_entry_number_second_click_limit));
        if (!Dm.truthy(holder) && Dm.truthy(scl))
        {
            float second = round(world.time, 10);
            if (!Dm.truthy(clicklimiter))
                clicklimiter = Dm.list_n(LIMITER_SIZE);

            if (!Dm.eq(second, clicklimiter[CURRENT_SECOND]))
            {
                clicklimiter[CURRENT_SECOND] = second;
                clicklimiter[SECOND_COUNT] = 0;
            }

            Dm.append_at(clicklimiter, SECOND_COUNT, 1 + Dm.num(Dm.not(Dm.not(ab))));

            if (Dm.gt(clicklimiter[SECOND_COUNT], scl))
            {
                to_chat(this, span_danger(I18n.T("Your previous click was ignored because you've done too many in a second")));
                return;
            }
        }

        //check if the server is overloaded and if it is then queue up the click for next tick
        //yes having it call a wrapping proc on the subsystem is fucking stupid glad we agree unfortunately byond insists its reasonable
        if (!QDELETED(@object) && Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(@object, nameof(atom._Click), location, control, @params), VERB_HIGH_PRIORITY_QUEUE_THRESHOLD, SSinput, control)))
            return;

        if (Dm.truthy(hotkeys))
        {
            // If hotkey mode is enabled, then clicking the map will automatically
            // unfocus the text bar.
            Dm.Deferred("code/modules/client/client_procs.dm:937", "client/Click: winset of the text bar's focus (the BYOND skin, not ported; the 3D client has its own input)");
        }
        else
            Dm.Deferred("code/modules/client/client_procs.dm:939", "client/Click: winset of the text bar's focus (the BYOND skin, not ported; the 3D client has its own input)");

        if (Dm.truthy(this._listen_lookup?[COMSIG_CLIENT_CLICK]))
            this._SendSignal(COMSIG_CLIENT_CLICK, Dm.list(this, @object, location, control, @params, usr));

        await Click__d0(@object, location, control, @params);
    }
}
