// Translated from tgstation (snapshot 1d99bd7f): code/modules/keybindings/bindings_client.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer): the keysend flood check (:5-36) and the movement branches (:43-53, :93-99)
// are translated; the dispatch to the keybinding datums of the player's preferences (:70-77, :100-109) is Dm.Deferred
// (client preferences and keybindings, M9; docs/ai/specs/not-translated.md), and the chat focus failsafe's winset is
// Dm.NotTranslated (hotkeys stays TRUE without preferences).

// Clients aren't datums so we have to define these procs indpendently.
// These verbs are called for all key press and release events
// The [DmPath] of /client, a BYOND builtin type, is on its engine part Builtin/client.builtin.cs (rules 2.3).
public partial class client : datum
{
    // tg: code/modules/keybindings/bindings_client.dm:3
    // GAME_VERB_NATIVE_INSTANT(/client, keyDown, "keyDown", null, _key as text, mousepos_x as num, mousepos_y as num, sizex as num, sizey as num)
    // expands (__DEFINES/verb.dm:73-86) to the type /datum/verb_metadata/client/keyDown (the verb registry, not declared
    // until the command layer, M9-M10), the verb below, which calls __gvb_keyDown(arglist(args)), and the proc
    // __gvb_keyDown with the body (rules 7.10: the verb settings name, hidden and instant leave no C#).
    public virtual async DmTask keyDown(object _key = null, object mousepos_x = null, object mousepos_y = null, object sizex = null, object sizey = null, params object[] __extra)
    {
        DmList args = Dm.args(_key, mousepos_x, mousepos_y, sizex, sizey, __extra);
        await Dm.Call(this, nameof(__gvb_keyDown), Dm.arglist(args));
    }

    // tg: code/modules/keybindings/bindings_client.dm:3 (the body of keyDown, :5-81)
    public virtual async DmTask __gvb_keyDown(object _key = null, object mousepos_x = null, object mousepos_y = null, object sizex = null, object sizey = null)
    {
        client_keysend_amount += 1;

        float cache = client_keysend_amount;

        if (Dm.truthy(keysend_tripped) && next_keysend_trip_reset <= world.time)
            keysend_tripped = FALSE;

        if (next_keysend_reset <= world.time)
        {
            client_keysend_amount = 0;
            next_keysend_reset = world.time + (1 * SECONDS);
        }

        //The "tripped" system is to confirm that flooding is still happening after one spike
        //not entirely sure how byond commands interact in relation to lag
        //don't want to kick people if a lag spike results in a huge flood of commands being sent
        if (cache >= MAX_KEYPRESS_AUTOKICK)
        {
            if (!Dm.truthy(keysend_tripped))
            {
                keysend_tripped = TRUE;
                next_keysend_trip_reset = world.time + (2 * SECONDS);
            }
            else
            {
                to_chat(this, span_userdanger(I18n.T("Flooding keysends! This could have been caused by lag, or due to a plugged-in game controller. You have been disconnected from the server automatically.")));
                log_admin(Dm.text("Client [] was just autokicked for flooding keysends; likely abuse but potentially lagspike.", ckey));
                message_admins(Dm.text("Client [] was just autokicked for flooding keysends; likely abuse but potentially lagspike.", ckey));
                qdel(this);
                return;
            }
        }

        ///Check if the key is short enough to even be a real key
        if (LAZYLEN(_key) > MAX_KEYPRESS_COMMANDLENGTH)
        {
            to_chat(this, span_userdanger(I18n.T("Invalid KeyDown detected! You have been disconnected from the server automatically.")));
            log_admin(Dm.text("Client [] just attempted to send an invalid keypress. Keymessage was over [] characters, autokicking due to likely abuse.", ckey, MAX_KEYPRESS_COMMANDLENGTH));
            message_admins(Dm.text("Client [] just attempted to send an invalid keypress. Keymessage was over [] characters, autokicking due to likely abuse.", ckey, MAX_KEYPRESS_COMMANDLENGTH));
            qdel(this);
            return;
        }

        //Focus Chat failsafe. Overrides movement checks to prevent WASD.
        if (!Dm.truthy(hotkeys) && length(_key) == 1 && !Dm.eq(_key, "Alt") && !Dm.eq(_key, "Ctrl") && !Dm.eq(_key, "Shift"))
        {
            // GAP(lead): winset (the UI seam, Appendix C, planned) and url_encode's caller; hotkeys is TRUE until the client
            // preferences (M9) can turn it off.
            Dm.NotTranslated("code/modules/keybindings/bindings_client.dm:40", "keyDown: the chat focus failsafe (winset input.focus and input.text; the BYOND skin, not ported)");
            return;
        }

        if (length(keys_held) >= HELD_KEY_BUFFER_LENGTH && !Dm.truthy(keys_held[_key]))
            await keyUp(keys_held[1], mousepos_x, mousepos_y, sizex, sizey); //We are going over the number of possible held keys, so let's remove the first one.

        //the time a key was pressed isn't actually used anywhere (as of 2019-9-10) but this allows easier access usage/checking
        keys_held[_key] = world.time;
        object movement = movement_keys[_key];
        if (Dm.truthy(movement))
        {
            calculate_move_dir();
            if (!Dm.truthy(movement_locked) && !Dm.truthy(Dm.bitand(next_move_dir_sub, movement)))
                Dm.combine(ref next_move_dir_add, movement);
        }

        // Client-level keybindings are ones anyone should be able to do at any time
        // Things like taking screenshots, hitting tab, and adminhelps.
        string AltMod = Dm.truthy(keys_held["Alt"]) ? "Alt" : "";
        string CtrlMod = Dm.truthy(keys_held["Ctrl"]) ? "Ctrl" : "";
        string ShiftMod = Dm.truthy(keys_held["Shift"]) ? "Shift" : "";
        object full_key = null;
        object sw__1 = _key;
        if (Dm.eq(sw__1, "Alt") || Dm.eq(sw__1, "Ctrl") || Dm.eq(sw__1, "Shift"))
            full_key = Dm.text("[][][]", AltMod, CtrlMod, ShiftMod);
        else
        {
            if (Dm.truthy(AltMod) || Dm.truthy(CtrlMod) || Dm.truthy(ShiftMod))
            {
                full_key = Dm.text("[][][][]", AltMod, CtrlMod, ShiftMod, _key);
                key_combos_held[_key] = full_key;
            }
            else
                full_key = _key;
        }

        // tg: :70-77  var/list/click_data = get_loc_from_mousepos(mousepos_x, mousepos_y, sizex, sizey, src); var/keycount = 0;
        // for(var/kb_name in prefs.key_bindings_by_key[full_key]) { keycount++; var/datum/keybinding/kb =
        // GLOB.keybindings_by_name[kb_name]; if(kb.can_use(src) && kb.down(src, click_data[1], click_data[2], click_data[3])
        // && keycount >= MAX_COMMANDS_PER_KEY) break }
        Dm.Deferred("code/modules/keybindings/bindings_client.dm:70", "keyDown: the keybinding datums bound to the key (prefs.key_bindings_by_key, GLOB.keybindings_by_name and their down(), which send the COMSIG_KB_* signals; client preferences and keybindings, M10+)");

        holder?.key_down(_key, this, full_key);
        mob.focus?.key_down(_key, this, full_key);
        mob.update_mouse_pointer();
    }

    // tg: code/modules/keybindings/bindings_client.dm:83
    // GAME_VERB_NATIVE_INSTANT(/client, keyUp, "keyUp", null, _key as text, mousepos_x as num, mousepos_y as num, sizex as num, sizey as num)
    public virtual async DmTask keyUp(object _key = null, object mousepos_x = null, object mousepos_y = null, object sizex = null, object sizey = null, params object[] __extra)
    {
        DmList args = Dm.args(_key, mousepos_x, mousepos_y, sizex, sizey, __extra);
        await Dm.Call(this, nameof(__gvb_keyUp), Dm.arglist(args));
    }

    // tg: code/modules/keybindings/bindings_client.dm:83 (the body of keyUp, :85-112)
    public virtual async DmTask __gvb_keyUp(object _key = null, object mousepos_x = null, object mousepos_y = null, object sizex = null, object sizey = null)
    {
        object key_combo = key_combos_held[_key];
        if (Dm.truthy(key_combo))
        {
            Dm.remove(ref key_combos_held, _key);
            await keyUp(key_combo, mousepos_x, mousepos_y, sizex, sizey);
        }

        if (!Dm.truthy(keys_held[_key]))
            return;

        Dm.remove(ref keys_held, _key);

        object movement = movement_keys[_key];
        if (Dm.truthy(movement))
        {
            calculate_move_dir();
            if (!Dm.truthy(movement_locked) && !Dm.truthy(Dm.bitand(next_move_dir_add, movement)))
                Dm.combine(ref next_move_dir_sub, movement);
        }
        // tg: :100-109  var/list/click_data; //manual calls of keyup: if(mousepos_x && mousepos_y) click_data =
        // get_loc_from_mousepos(mousepos_x, mousepos_y, sizex, sizey, src); then (no full key for release, because for mod
        // keys you can hold different keys and releasing any should be handled by the key binding specifically)
        // for (var/kb_name in prefs.key_bindings_by_key[_key]) { var/datum/keybinding/kb = GLOB.keybindings_by_name[kb_name];
        // if(kb.can_use(src) && kb.up(src, click_data?[1])) break }
        Dm.Deferred("code/modules/keybindings/bindings_client.dm:100", "keyUp: the keybinding datums bound to the key (prefs.key_bindings_by_key, GLOB.keybindings_by_name and their up(); client preferences and keybindings, M10+)");
        holder?.key_up(_key, this);
        mob.focus?.key_up(_key, this);
        mob.update_mouse_pointer();
    }
}
