// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/vendor/_vending.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2 (docs/ai/specs/gaps.md, D291): the whole of vendor/ (this file, inventory.dm, interaction.dm, throwing.dm, tilting.dm,
// ui_data.dm). The var blocks are generated. Deferred leaves (rows in live-partial.txt and not-translated.md section 103): the screentips
// (add_context), view variables (vv_edit_var), the text-to-speech voice (SStts), the fishing spot (fishing), the light mask's emissive
// (DV12), the admin logs. The payment component and the prices came with the economy (gaps P4a, D327; DV225 ended).

// tg: code/modules/vending/vendor/_vending.dm:7
[DmPath("/datum/data/vending_product")]
public partial class datum_data_vending_product : datum_data
{
    // tg: code/modules/vending/vendor/_vending.dm:8  name = "generic" (the hand-written class's constructor)
    public datum_data_vending_product()
    {
        name = "generic";
    }

    // tg: code/modules/vending/vendor/_vending.dm:16
    public object price;

    /// The category the product was in, if any. Sourced directly from product_categories.
    // tg: code/modules/vending/vendor/_vending.dm:23  (hand-declared: generated as a Type, but it holds a product_categories entry, a list)
    public DmList category;

    // tg: code/modules/vending/vendor/_vending.dm:27
    public override object Destroy(float? force = null)
    {
        returned_products = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/vending/vendor/_vending.dm:37
public partial class obj_machinery_vending : obj_machinery
{
    // tg: code/modules/vending/vendor/_vending.dm:2  #define CREDITS_DUMP_THRESHOLD 50 (file-local, #undef at the end of the folder)
    public const float CREDITS_DUMP_THRESHOLD = 50;

    /// Used to check for discounting and for displaying discounted prices in the UI
    // tg: code/modules/vending/vendor/_vending.dm:195  var/static/datum/id_trim/job/big_boss_trim = /datum/id_trim/job/captain (a typepath, never written)
    public static Type big_boss_trim => typeof(datum_id_trim_job_captain);

    /**
     * Initialize the vending machine
     *
     * Builds the vending machine inventory, sets up slogans and other such misc work
     */
    // tg: code/modules/vending/vendor/_vending.dm:210
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //means we produce products with fixed amounts
        if (refill_canister == null)
        {
            circuit = null;
            Waiting.NoSleep(RefreshParts());
        }

        object dot = base.Initialize(mapload, __extra);

        set_wires(Dm.@new<datum_wires_vending>(this));

        if (Dm.truthy(SStts?.tts_enabled))
        {
            // tg: :221  var/static/list/vendor_voice_by_type
            DmList vendor_voice_by_type = TgState.Current.obj_machinery_vending__vendor_voice_by_type ??= Dm.list();
            if (!Dm.truthy(vendor_voice_by_type[type]))
                vendor_voice_by_type[type] = SStts.random_tts_voice();
            voice = vendor_voice_by_type[type];
        }

        slogan_list = splittext(product_slogans, ";");
        ad_list = splittext(product_ads, ";");
        // So not all machines speak at the exact same time.
        // The first time this machine says something will be at slogantime + this random value,
        // so if slogantime is 10 minutes, it will say it at somewhere between 10 and 20 minutes after the machine is crated.
        last_slogan = world.time + rand(0, slogan_delay);
        power_change();

        if (Dm.truthy(mapload)) //check if it was initially created off station during mapload.
        {
            if (!Dm.truthy(is_station_level(z)))
            {
                if (!Dm.truthy(onstation_override))
                {
                    onstation = FALSE;
                    if (all_products_free == null) // Only auto-set the free products var if we haven't explicitly assigned a value to it yet.
                        all_products_free = TRUE;
                }
                if (circuit_value != null)
                {
                    circuit_value.all_products_free = Dm.num(all_products_free); //sync up the circuit so the pricing schema is carried over if it's reconstructed.
                    circuit_value.desc = "This board's card reader component has been cut out, along with its brand selection dial.";
                }
            }
        }
        else if (circuit_value != null)
            all_products_free = circuit_value.all_products_free; //if it was constructed outside mapload, sync the vendor up with the circuit's var so you can't bypass price requirements by moving / reconstructing it off station.

        if (!Dm.truthy(all_products_free)) // (SSeconomy?: a test harness world that makes machines without Master has none, rules 10.6)
            Waiting.NoSleep(AddComponent(typeof(datum_component_payment), 0, SSeconomy?.get_dep_account(payment_department), PAYMENT_VENDING));
        register_context();

        if (fish_source_path != null)
            Dm.Deferred("code/modules/vending/vendor/_vending.dm:248", "Initialize: AddComponent(/datum/component/fishing_spot, fish_source_path): fishing in a vendor (fishing, 0/787)");

        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:252
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(dot))
            return dot;

        float dump_amount = 0;
        bool found_anything = true;
        while (found_anything)
        {
            found_anything = false;
            foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(shuffle(product_records)))
            {
                //first dump any of the items that have been returned, in case they contain the nuke disk or something
                for (float i = 1, i__end = LAZYLEN(record.returned_products); i <= i__end; i++)
                {
                    obj_item returned_obj_to_dump = await dispense(record, get_turf(this), dispense_returned: TRUE) as obj_item;
                    await step(returned_obj_to_dump, Dm.num(pick(GLOB.alldirs)));
                }

                if (Dm.num(record.amount) <= 0) //Try to use a record that actually has something to dump.
                    continue;
                object dump_path = record.product_path;
                if (!Dm.truthy(dump_path))
                    continue;
                // busting open a vendor will destroy some of the contents
                if (found_anything && prob(80))
                {
                    record.amount--;
                    continue;
                }

                obj obj_to_dump = await dispense(record, loc) as obj;
                await step(obj_to_dump, Dm.num(pick(GLOB.alldirs)));
                found_anything = true;
                dump_amount++;
                if (dump_amount >= 16)
                    return dot;
            }
        }
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:284
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_item_vending_refill installed_refill = Dm.locate<obj_item_vending_refill>(component_parts);
        if (installed_refill == null)
            return null;

        DmList record_list = null;
        DmList canister_list = null;
        for (float i = 1; i <= 3; i++)
        {
            switch (i)
            {
                case 1:
                    record_list = product_records;
                    canister_list = installed_refill.products;
                    break;
                case 2:
                    record_list = hidden_records;
                    canister_list = installed_refill.contraband;
                    break;
                default:
                    record_list = coin_records;
                    canister_list = installed_refill.premium;
                    break;
            }

            canister_list.Cut();
            foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(record_list))
            {
                float stock = Dm.num(record.amount) - LAZYLEN(record.returned_products);
                if (stock != 0)
                    canister_list[record.product_path] = stock;
            }
        }
        return null;
    }

    // tg: code/modules/vending/vendor/_vending.dm:309
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(product_records);
        QDEL_LIST(hidden_records);
        QDEL_LIST(coin_records);
        return base.Destroy(force);
    }

    /**
     * Returns the total loaded & max amount of items i.e list(total_loaded, total_maximum) in the vending machine based on the product records and premium records
     *
     * Arguments
     * * contraband - should we count contrabrand as well
    */
    // tg: code/modules/vending/vendor/_vending.dm:347
    [ShouldNotOverride]
    public DmList total_stock(float? contrabrand = null)
    {
        contrabrand ??= TRUE;
        float total_loaded = 0;
        float total_max = 0;
        DmList stock = Dm.add(product_records, coin_records) as DmList;
        if (Dm.truthy(contrabrand))
            Dm.append(ref stock, hidden_records);
        foreach (datum_data_vending_product record in Dm.each_cast<datum_data_vending_product>(stock))
        {
            total_loaded += Dm.num(record.amount);
            total_max += record.max_amount;
        }
        return Dm.list(total_loaded, total_max);
    }

    // tg: code/modules/vending/vendor/_vending.dm:362
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (refill_canister == null)
            return dot; // you can add the comment here instead

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The machine may be [] apart.", EXAMINE_HINT("pried"))));

        DmList total_stock = this.total_stock();
        if (Dm.truthy(total_stock[2]))
        {
            if (Dm.num(total_stock[1]) < Dm.num(total_stock[2]))
                Dm.append(ref dot, span_notice(I18n.T("\\The [] can be restocked with [] with the panel open.", this,
                    span_boldnotice(I18n.T("\\a [] []", Dm.initial(refill_canister, (obj_item_vending_refill p) => p.machine_name), Dm.initial(refill_canister, (obj_item_vending_refill p) => p.name))))));
            else
                Dm.append(ref dot, span_notice(I18n.T("\\The [] is fully stocked.", this)));
        }
        if (credits_contained < CREDITS_DUMP_THRESHOLD && credits_contained > 0)
            Dm.append(ref dot, span_notice(I18n.T("It should have a handfull of [] stored based on the missing items.", MONEY_NAME)));
        else if (credits_contained > PAYCHECK_CREW)
            Dm.append(ref dot, span_notice(I18n.T("It should have at least a full paycheck worth of [] inside!", MONEY_NAME)));
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:382
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        Waiting.NoSleep(set_light(Dm.truthy(powered__proc()) ? MINIMUM_USEFUL_LIGHT_RANGE : 0));
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:389
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = Dm.text("[]-broken", Dm.initial(this, (atom p) => p.icon_state));
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", Dm.initial(this, (atom p) => p.icon_state), Dm.truthy(powered__proc()) ? null : "-off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/vending/vendor/_vending.dm:396
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, panel_type);
        // DEVIATION(DV12): if(light_mask && !(machine_stat & BROKEN) && powered__proc()) . += emissive_appearance(icon, light_mask, src), the glow on
        // the emissive plane, is not created (lighting planes and emissives are not ported); the client's models light the vendor's light
        // mask themselves (vending_light_layers.json).
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:412
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        // a zap that destroys the vendor first (machinery zap_act's take_damage, then emp_act) leaves it QDELING: tg's get_language_holder
        // CRASHes and the next line reads null.selected_language (a runtime in tg); the port stops here
        if (QDELETED(this))
            return dot;
        datum_language_holder vending_languages = get_language_holder();
        datum_wires_vending vending_wires = wires as datum_wires_vending;
        // if the language wire got pulsed during an EMP, this will make sure the language_iterator is synched correctly
        vending_languages.selected_language = vending_languages.spoken_languages[vending_wires.language_iterator];
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:419
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        force ??= FALSE;
        can_fall ??= TRUE;
        if (!Dm.truthy(force))
            return null;
        return await base.unbuckle_mob(buckled_mob, force, can_fall);
    }

    // tg: code/modules/vending/vendor/_vending.dm:424
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        balloon_alert(user, "product lock disabled");
        return TRUE;
    }

    // tg: code/modules/vending/vendor/_vending.dm:432
    public override object power_change()
    {
        object dot = base.power_change();
        if (Dm.truthy(powered__proc()))
            START_PROCESSING(SSmachines, this);
        return dot;
    }

    // tg: code/modules/vending/vendor/_vending.dm:437
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
            return PROCESS_KILL;

        if (seconds_electrified > MACHINE_NOT_ELECTRIFIED)
            seconds_electrified--;

        //Pitch to the people!  Really sell it!
        if (last_slogan + slogan_delay <= world.time && slogan_list.len > 0 && !Dm.truthy(shut_up) && SPT_PROB(2.5f, seconds_per_tick))
        {
            Waiting.NoSleep(say(pick(slogan_list)));
            last_slogan = world.time;
        }

        if (Dm.truthy(shoot_inventory) && SPT_PROB(shoot_inventory_chance, seconds_per_tick))
            Waiting.NoSleep(throw_item());
        return null;
    }

    //===============================SPEACH===================================================
    // tg: code/modules/vending/vendor/_vending.dm:454
    public override object can_speak__proc(float? allow_mimes = null)
    {
        return Dm.truthy(is_operational) && !Dm.truthy(shut_up) && Dm.truthy(base.can_speak__proc(allow_mimes)) ? TRUE : FALSE;
    }

    /**
     * Speak the given message verbally
     * Checks if the machine is powered and the message exists
     *
     * Arguments:
     * * message - the message to speak
     */
    // tg: code/modules/vending/vendor/_vending.dm:465
    public virtual async DmTask<object> speak(object message = null)
    {
        if (!Dm.truthy(is_operational))
            return null;
        if (!Dm.truthy(message))
            return null;

        await say(message);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/vending/vendor/_vending.dm:221  var/static/list/vendor_voice_by_type
    internal DmList obj_machinery_vending__vendor_voice_by_type;
}
