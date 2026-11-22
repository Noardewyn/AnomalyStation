// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/radio/electropack.dm, code/modules/assembly/shock_kit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/devices/radio/electropack.dm:1
public partial class obj_item_electropack : obj_item
{
    // tg: code/game/objects/items/devices/radio/electropack.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(frequency);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:26
    public override object Destroy(float? force = null)
    {
        SSradio.remove_object(this, frequency);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:30
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] hooks []self to the electropack and spams the trigger! It looks like [] trying to commit suicide!", user, user.p_them(), user.p_theyre())));
        return FIRELOSS;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/devices/radio/electropack.dm:35
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user.get_item_by_slot(ITEM_SLOT_BACK) == this)
        {
            to_chat(user, span_warning(I18n.T("You need help taking this off!")));
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:41
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_clothing_head_helmet)
            return NONE;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand, you cannot attach it to []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_assembly_shock_kit torture_device = Dm.@new<obj_item_assembly_shock_kit>(user);
        torture_device.icon = Dm.resource("icons/obj/devices/assemblies.dmi");

        await tool.forceMove(torture_device); // Forcemove() because we've already seen we can move it with the check above and the check above forces us to use forceMove() next

        tool.master = torture_device;
        torture_device.helmet_part = tool as obj_item_clothing_head_helmet;

        await user.transferItemToLoc(this, torture_device, TRUE);
        master = torture_device;
        torture_device.electropack_part = this;

        await user.put_in_hands(torture_device);
        torture_device.add_fingerprint(user);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:65
    public override object receive_signal(datum_signal signal = null)
    {
        if (signal == null || !Dm.eq(signal.data["code"], code))
            return null;
        if (isliving(loc) && Dm.truthy(on))
        {
            if (Dm.truthy(shock_cooldown))
                return null;
            shock_cooldown = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_shock_cooldown)), 10 * SECONDS, file: "code/game/objects/items/devices/radio/electropack.dm", line: 71);
            mob_living L = loc as mob_living;
            Waiting.Detach(step(L, Dm.num(pick(GLOB.cardinals))));

            to_chat(L, span_danger(I18n.T("You feel a sharp shock!")));
            do_sparks(3, TRUE, L);
            Waiting.Detach(L.Paralyze(100));
        }

        if (master != null)
        {
            if (master is obj_item_assembly)
            {
                obj_item_assembly master_as_assembly = master as obj_item_assembly;
                master_as_assembly.pulsed();
            }
            master.receive_signal();
        }
        return null;
    }

    // tg: electropack.dm:71  VARSET_CALLBACK(src, shock_cooldown, FALSE)
    public virtual void reset_shock_cooldown() => shock_cooldown = FALSE;

    // tg: code/game/objects/items/devices/radio/electropack.dm:88
    public virtual void set_frequency(object new_frequency = null)
    {
        SSradio.remove_object(this, frequency);
        frequency = Dm.num(new_frequency);
        SSradio.add_object(this, frequency, RADIO_SIGNALER);
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:93
    public override object ui_state(mob user = null) => GLOB.hands_state;

    // tg: code/game/objects/items/devices/radio/electropack.dm:96
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Electropack", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:102
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["power"] = on;
        data["frequency"] = frequency;
        data["code"] = code;
        return data;
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:109
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["minFrequency"] = MIN_FREE_FREQ;
        data["maxFrequency"] = MAX_FREE_FREQ;
        return data;
    }

    // tg: code/game/objects/items/devices/radio/electropack.dm:115
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                on = Dm.truthy(on) ? FALSE : TRUE;
                icon_state = Dm.text("electropack[]", on);
                dot = TRUE;
                break;
            case "freq":
                {
                    object value = unformat_frequency(@params["freq"]);
                    if (Dm.truthy(value))
                    {
                        frequency = Dm.num(sanitize_frequency(value, TRUE));
                        set_frequency(frequency);
                        dot = TRUE;
                    }
                }
                break;
            case "code":
                {
                    object value = text2num(@params["code"]);
                    if (Dm.truthy(value))
                    {
                        code = clamp(round(Dm.num(value)), 1, 100);
                        dot = TRUE;
                    }
                }
                break;
            case "reset":
                if (Dm.eq(@params["reset"], "freq"))
                {
                    frequency = Dm.num(Dm.initial(this, nameof(frequency)));
                    dot = TRUE;
                }
                else if (Dm.eq(@params["reset"], "code"))
                {
                    code = Dm.num(Dm.initial(this, nameof(code)));
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }
}

// tg: code/modules/assembly/shock_kit.dm:1
public partial class obj_item_assembly_shock_kit : obj_item_assembly
{
    // tg: code/modules/assembly/shock_kit.dm:10
    public override object Destroy(float? force = null)
    {
        qdel(helmet_part);
        helmet_part = null;
        qdel(electropack_part);
        electropack_part = null;
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/shock_kit.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (helmet_part == null)
        {
            helmet_part = Dm.@new<obj_item_clothing_head_helmet>(this);
            helmet_part.master = this;
        }
        if (electropack_part == null)
        {
            electropack_part = Dm.@new<obj_item_electropack>(this);
            electropack_part.master = this;
        }
        return dot;
    }

    // tg: code/modules/assembly/shock_kit.dm:24
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        await base.wrench_act(user, I);
        to_chat(user, span_notice(I18n.T("You disassemble [].", this)));
        if (helmet_part != null)
        {
            await helmet_part.forceMove(drop_location() as atom);
            helmet_part.master = null;
            helmet_part = null;
        }
        if (electropack_part != null)
        {
            await electropack_part.forceMove(drop_location() as atom);
            electropack_part.master = null;
            electropack_part = null;
        }
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/assembly/shock_kit.dm:38
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await helmet_part.attack_self(user);
        await electropack_part.attack_self(user);
        add_fingerprint(user);
        return null;
    }
}
