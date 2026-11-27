// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/monkeyrecycler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the var overrides are generated.

// tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:1  GLOBAL_LIST_EMPTY(monkey_recyclers)
    public DmList monkey_recyclers;

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:1  (GLOBAL_MANAGED(monkey_recyclers, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmonkey_recyclers()
    {
        monkey_recyclers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "monkey_recyclers");
    }
}

// tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:4
public partial class obj_machinery_monkey_recycler : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Dm.append(ref GLOB.monkey_recyclers, this);
        add_overlay("grinder_monkey");
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:23
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.monkey_recyclers, this);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:27
    public override async DmTask<object> RefreshParts() //Ranges from 0.2 to 0.8 per monkey recycled
    {
        object dot = await base.RefreshParts();
        cube_production = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            cube_production += Dm.num(servo.tier) * 0.1f;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            cube_production += Dm.num(matter_bin.tier) * 0.1f;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Producing <b>[]</b> cubes for every monkey inserted.", cube_production)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:40
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            power_change();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:46
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:49
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:52
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : base_icon_state as string;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:56
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        if (dropped is not mob_living target)
            return null;
        if (ismonkey(target))
            await stuff_monkey_in(target as mob_living_carbon_human, user as mob_living);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:62
    public virtual async DmTask stuff_monkey_in(mob_living_carbon_human target = null, mob_living user = null)
    {
        if (target == null)
            return;
        if (!IS_UNCONSCIOUS_OR_CRIT(target))
        {
            to_chat(user, span_warning("The monkey is struggling far too much to put it in the recycler."));
            return;
        }
        if (target.buckled != null || Dm.truthy(target.has_buckled_mobs()))
        {
            to_chat(user, span_warning("The monkey is attached to something."));
            return;
        }
        qdel(target);
        to_chat(user, span_notice("You stuff the monkey into the machine."));
        playsound(loc, Dm.resource("sound/machines/juicer.ogg"), 50, TRUE);
        float offset = prob(50) ? -2 : 2;
        animate(this, Dm.named("pixel_x", pixel_x + offset), Dm.named("time", 0.2f), Dm.named("loop", 200)); //start shaking
        await use_energy(active_power_usage);
        stored_matter += cube_production;
        _addtimer(Dm.@new<datum_callback>(this, nameof(pixel_x__varset), base_pixel_x), 0, file: "code/modules/food_and_drinks/machinery/monkeyrecycler.dm", line: 80);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), user, span_notice(I18n.T("The machine now has [] monkey[] worth of material stored.", stored_matter, Dm.num(stored_matter) == 1 ? "" : "s"))), 0, file: "code/modules/food_and_drinks/machinery/monkeyrecycler.dm", line: 81);
    }

    /// VARSET_CALLBACK(src, pixel_x, base_pixel_x)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for this var.
    public virtual void pixel_x__varset(object var_value = null)
    {
        pixel_x = Dm.num(var_value);
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:83
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (stored_matter >= 1)
        {
            to_chat(user, span_notice("The machine hisses loudly as it condenses the ground monkey meat. After a moment, it dispenses a brand new monkey cube."));
            playsound(loc, Dm.resource("sound/machines/hiss.ogg"), 50, TRUE);
            float count = floor(stored_matter);
            for (int i = 1; i <= count; i++)
            {
                Dm.@new<obj_item_food_monkeycube>(loc);
                stored_matter--;
            }
            to_chat(user, span_notice(I18n.T("The machine's display flashes that it has [] monkeys worth of material left.", stored_matter)));
        }
        else
            to_chat(user, span_danger(I18n.T("The machine needs at least 1 monkey worth of material to produce a monkey cube. It currently has [].", stored_matter)));
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/monkeyrecycler.dm:94
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        if (tool is obj_item_multitool I)
        {
            I.set_buffer(this);
            balloon_alert(user, "saved to multitool buffer");
            return TRUE;
        }
        return null;
    }
}
