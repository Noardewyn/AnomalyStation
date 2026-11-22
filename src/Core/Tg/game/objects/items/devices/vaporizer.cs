// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/vaporizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole but its screentips (add_context: a live-partial row, D291). The hydro-vaporizer: a cell-powered
// device that wets its holder's gills through the wet stacks component (wet_stacks_clothing.dm), its charge shown as a progress bar
// over it while held, the cell screwed out or put in, and an EMP that overloads it into a cloud of hot steam that jitters and blurs
// those near (with tg's steam particles). The `cell` var holds the cell itself (the converter typed it as a path): hand-declared here,
// so hand mode keeps the generated constructor here too. The var block is generated (D111).

/*
 *	Device to let water breathers breathe. TRAIT_WET_FOR_LONGER is required for it to actually function
 *	Because this is a Cerulean device, after all, intended to be used on fish scales
*/
// tg: code/game/objects/items/devices/vaporizer.dm:5
public partial class obj_item_vaporizer : obj_item
{
    /// the cell which will be used to power the item
    // tg: code/game/objects/items/devices/vaporizer.dm:24  var/obj/item/stock_parts/power_store/cell/cell
    public obj_item_stock_parts_power_store_cell cell;

    // tg: code/game/objects/items/devices/vaporizer.dm:6  (the generated constructor, kept here: hand mode, see the header)
    public obj_item_vaporizer()
    {
        name = "hydro-vaporizer";
        desc = "An ingenious little device manufactured for supporting an alternative method for respiration. Relying on a removable cell, the coil mechanism synthesizes a hydrogen oxygen mixture, which can then be used to moisturize the wearer's gills. \nThe rate at which liquid is applied seems to be intended for skin which excells at retaining moisture. \nThere is a picture of planet Marina stamped onto it. \n\nA label on its back warns about the potential dangers of electro-magnetic pulses.";
        icon = Dm.resource("icons/obj/devices/tool.dmi");
        icon_state = "vaporizer";
        slot_flags = 512;
        pickup_sound = "generic_device_pickup";
        drop_sound = "generic_device_drop";
        w_class = 2;
        resistance_flags = 34;
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:20  (the generated var initializer, kept here: hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 45), Dm.kv(typeof(datum_material_gold), 150), Dm.kv(typeof(datum_material_diamond), 18)); // tg: code/game/objects/items/devices/vaporizer.dm:20
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:34
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell == null)
            return dot;
        if (!in_range(this, user) && !isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("If you want any more information you'll need to get closer.")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("The LED display reads its []% charged.", cell.percent())));
        return dot;
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_wet_stacks_granting), Dm.@new<datum_callback>(this, nameof(use_cell)), Dm.named("must_be_worn", FALSE)));
        return dot;
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:51
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:54
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        create_charge_bar(user as mob_living);
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:58
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        QDEL_NULL(ref charge_bar);
        return dot;
    }

    /// create a visual for how much power is left in the cell of the item
    // tg: code/game/objects/items/devices/vaporizer.dm:63
    public virtual void create_charge_bar(mob_living user = null)
    {
        if (cell == null || Dm.truthy(charge_bar))
            return;
        atom charge_bar_target = loc == user ? this : loc;
        charge_bar = Dm.@new<datum_progressbar>(user, 100/*%*/, charge_bar_target, cell.percent());
    }

    // remove the cell with a screwdriver
    // tg: code/game/objects/items/devices/vaporizer.dm:70
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (cell == null)
            return FALSE;
        tool.play_tool_sound(this);
        QDEL_NULL(ref charge_bar);
        balloon_alert(user, I18n.T("removed []", cell));
        await cell.forceMove(get_turf(this));
        cell = null;
        return TRUE;
    }

    // proc to feed the machine a new cell
    // tg: code/game/objects/items/devices/vaporizer.dm:81
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_stock_parts_power_store_cell new_cell = tool as obj_item_stock_parts_power_store_cell;
        if (new_cell == null)
            return NONE;
        if (cell != null)
        {
            balloon_alert(user, I18n.T("slot occupied!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(new_cell, this)))
            return NONE;
        cell = new_cell;
        balloon_alert(user, I18n.T("installed []", new_cell));
        create_charge_bar(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /// proc we forward to the callback the wet stack component uses every time it ticks
    // tg: code/game/objects/items/devices/vaporizer.dm:95
    public virtual async DmTask<object> use_cell()
    {
        if (cell == null || !Dm.truthy(await cell.use(power_cost, TRUE)))
            return FALSE;
        charge_bar?.update(cell.percent());
        return TRUE;
    }

    /// overload and spew hot steam on EMP
    // tg: code/game/objects/items/devices/vaporizer.dm:102
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)) && cell != null)
            await cell.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        turf tile = get_turf(this);
        if (isclosedturf(tile))
            return dot;
        to_chat(this, span_warning(I18n.T("[] overloads, spewing out a cloud of hot steam!", this)));
        playsound(this, Dm.resource("sound/effects/spray.ogg"), rand(50, 80), TRUE);
        tile.atmos_spawn_air(Dm.text("[]=[];[]", GAS_WATER_VAPOR, clamp(cell.maxcharge * 0.1f, 1, 100), TURF_TEMPERATURE(1000)));
        foreach (mob_living nearby_mob in Dm.each<mob_living>(Builtins.range(4, tile)))
        {
            await nearby_mob.set_jitter_if_lower(rand(5 * SECONDS, 15 * SECONDS));
            await nearby_mob.set_eye_blur_if_lower(rand(3 * SECONDS, 7 * SECONDS));
        }
        await forceMove(drop_location() as atom);
        particles = Dm.@new<particles_smoke_steam>();
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_particles)), 5 * SECONDS, TIMER_DELETE_ME, file: "code/game/objects/items/devices/vaporizer.dm", line: 119);
        return dot;
    }

    // tg: code/game/objects/items/devices/vaporizer.dm:121
    public virtual void remove_particles()
    {
        if (particles == null)
            return;
        ((particles)particles).spawning = 0;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 3 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF((datum)particles) : particles), 3 * SECONDS, file: "code/game/objects/items/devices/vaporizer.dm", line: 125); // QDEL_IN(particles, 3 SECONDS), inline (rules 11.1 (f))
    }
}

// tg: code/game/objects/items/devices/vaporizer.dm:47
public partial class obj_item_vaporizer_with_cell : obj_item_vaporizer
{
    // tg: code/game/objects/items/devices/vaporizer.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell>(this);
        return dot;
    }
}
