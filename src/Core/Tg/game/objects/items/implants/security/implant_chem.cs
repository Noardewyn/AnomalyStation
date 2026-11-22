// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/security/implant_chem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file; the remote signal (signaler_sync and receive_signal) came with SSradio and the signalers (M9 slice D). The prisoner management console that releases the reagents from afar is not ported; the
// implant releases them on its wearer's death and from an action button's cause as tg has it (M8 slice F, D186).

// tg: code/game/objects/items/implants/security/implant_chem.dm:1
public partial class obj_item_implant_chem : obj_item_implant
{
    // tg: code/game/objects/items/implants/security/implant_chem.dm:27
    public override object is_shown_on_console(obj_machinery_computer_prisoner_management console = null)
    {
        return !Dm.truthy(frequency) && Dm.truthy(is_valid_z_level(get_turf(console), get_turf(imp_in)));
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:30
    public override object get_management_console_data()
    {
        DmList info_shown = (DmList)base.get_management_console_data();
        info_shown["Volume"] = Dm.text("[]u", ((datum_reagents)reagents).total_volume);
        return info_shown;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:35
    public override object get_management_console_buttons()
    {
        DmList buttons = (DmList)base.get_management_console_buttons();
        foreach (object i in Dm.each(implant_sizes))
        {
            Dm.append(buttons, Dm.list(Dm.list( // UNTYPED_LIST_ADD: the button is one element
                Dm.kv("name", Dm.text("Inject []u", i)),
                Dm.kv("color", "good"),
                Dm.kv("action_key", "inject"),
                Dm.kv("action_params", Dm.list(Dm.kv("amount", i))))));
        }
        return buttons;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:46
    public override async DmTask<object> handle_management_console_action(mob user = null, DmList @params = null, obj_machinery_computer_prisoner_management console = null)
    {
        object dot = await base.handle_management_console_action(user, @params, console);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(@params["implant_action"], "inject"))
        {
            float amount = Dm.num(text2num(@params["amount"]));
            if (!Dm.@in(amount, implant_sizes))
                return TRUE;

            object reagents_inside = ((datum_reagents)reagents).get_reagent_log_string();
            await activate(amount);
            log_combat(user, imp_in, Dm.text("injected [] units of []", amount, reagents_inside), this);
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(50, OPENCONTAINER);
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:65
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (Dm.truthy(dot) && !Dm.truthy(frequency))
            RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:70
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (Dm.truthy(dot))
            UnregisterSignal(target, COMSIG_LIVING_DEATH);
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:75
    [SignalHandler]
    public virtual void on_death(mob_living source = null)
    {
        Waiting.Detach(activate(((datum_reagents)reagents).total_volume));
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:79
    [DmParams("cause")]
    public override async DmTask<object> activate(params object[] __extra)
    {
        object cause = Dm.arg(__extra, 0);
        object dot = await base.activate(__extra);
        if (!Dm.truthy(cause) || !Dm.truthy(imp_in))
            return dot;
        mob_living_carbon victim = (mob_living_carbon)imp_in;
        object injectamount = null;
        if (Dm.eq(cause, "action_button"))
            injectamount = ((datum_reagents)reagents).total_volume;
        else
            injectamount = cause;
        await ((datum_reagents)reagents).trans_to(victim, Dm.num(injectamount), methods: INJECT);
        to_chat(victim, span_hear(I18n.T("You hear a faint beep.")));
        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
        {
            to_chat(victim, span_hear(I18n.T("You hear a faint click from your chest.")));
            qdel(this);
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:95
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_assembly_signaler)))
        {
            signaler_sync((obj_item_assembly_signaler)tool, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:101
    public virtual void signaler_sync(obj_item_assembly_signaler syncing_signaler = null, mob_living user = null)
    {
        to_chat(user, I18n.T("You sync \\the [] to \\the []'s code & frequency[].", this, syncing_signaler, Dm.truthy(frequency) ? "" : ", disabling other methods of activation"));
        code = syncing_signaler.code;
        SSradio.remove_object(this, frequency);
        frequency = syncing_signaler.frequency;
        SSradio.add_object(this, frequency, RADIO_SIGNALER);
    }

    // tg: code/game/objects/items/implants/security/implant_chem.dm:108
    public override object receive_signal(datum_signal signal = null)
    {
        if (signal == null || !Dm.eq(signal.data["code"], code))
            return null;
        // (receive_signal cannot sleep, waiting-model.md 5.4: the release may, so it runs on its own)
        Waiting.Detach(activate(((datum_reagents)reagents).total_volume));
        return null;
    }
}
