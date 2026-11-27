// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/concrete_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the MetaStation leftovers: concrete): the concrete, hexacrete and pavement floors a concrete reagent pours over a
// catwalk, curing over a minute (a grey colour filter while wet, footprints while soft), shaped with a chisel while wet, smashed
// with a mining tool.

// tg: code/game/turfs/open/floor/concrete_floor.dm:1
public partial class turf_open_floor_concrete : turf_open_floor
{
    // tg: code/game/turfs/open/floor/concrete_floor.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(has_variation))
            icon_state = Dm.text("[]_[]", base_icon_state, rand(1, 4));
        check_harden();
        Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        return dot;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:40
    public override object broken_states()
    {
        return Dm.list("concdam_1", "concdam_2", "concdam_3", "concdam_4");
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] look[] like you could smash [] with <b>[]</b>.", p_They(), p_s(), p_them(), tool_behaviour_name(TOOL_MINING))));
        // DM's switch ranges, first match: 0.8 to 0.99, 0.4 to 0.8, 0 to 0.4
        if (harden_lvl >= 0.8f && harden_lvl <= 0.99f)
            Dm.append(ref dot, I18n.T("[] look[] nearly dry.", p_They(), p_s()));
        else if (harden_lvl >= 0.4f && harden_lvl <= 0.8f)
            Dm.append(ref dot, I18n.T("[] look[] a little wet.", p_They(), p_s()));
        else if (harden_lvl >= 0f && harden_lvl <= 0.4f)
            Dm.append(ref dot, I18n.T("[] look[] freshly poured.", p_They(), p_s()));
        return dot;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:55
    public override void add_footprint(mob_living_carbon_human walker = null, object movement_direction = null)
    {
        if (harden_lvl <= 0.8f)
            base.add_footprint(walker, movement_direction);
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:59
    public override void clear_footprints()
    {
        if (harden_lvl <= 0.8f)
            base.clear_footprints();
        else
            update_appearance();
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:65
    public override async DmTask<object> attackby(obj_item I = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(I, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(I.tool_behaviour, TOOL_MINING))
        {
            to_chat(user, span_notice(I18n.T("You start smashing []...", this)));
            float adj_time = (Dm.truthy(broken) || Dm.truthy(burnt)) ? smash_time / 2 : smash_time;
            float cell_x = x, cell_y = y, cell_z = z;
            if (Dm.truthy(await I.use_tool(this, user as mob_living, adj_time, volume: 30)))
            {
                // TURF-IDENTITY: tg's src after the sleep is the turf here; a floor replaced meanwhile is not this one
                if (QDELETED(this))
                    return TRUE;
                Dm.@new<obj_effect_decal_cleanable_concrete_dust>(this);
                to_chat(user, span_notice(I18n.T("You break [].", this)));
                playsound(this, Dm.resource("sound/effects/break_stone.ogg"), 30, TRUE);
                await remove_tile(silent: TRUE);
                return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:79
    public override async DmTask<object> try_replace_tile(obj_item_stack_tile T = null, mob user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:82
    public override async DmTask<object> pry_tile(obj_item I = null, mob user = null, float? silent = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:85
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        if (!Dm.truthy(dot))
            return dot;
        remove_filter("harden");
        if (harden_lvl == 1)
            return dot;

        float offset = (1 - harden_lvl) * 0.3f;
        float @base = 1 - offset;
        DmList col_filter = Dm.list(@base, 0, 0, 0, @base, 0, 0, 0, @base, offset, offset, offset);
        add_filter("harden", 1, color_matrix_filter(col_filter, FILTER_COLOR_RGB) as DmList);
        return dot;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:97
    public virtual void check_harden()
    {
        harden_lvl = clamp(harden_lvl, 0, 1);
        if (harden_lvl < 1)
        {
            START_PROCESSING(SSobj, this);
            Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        }
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:103
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float time_per_tick = Dm.num(seconds_per_tick) * SECONDS;
        harden_lvl = Math.Min(harden_lvl + (time_per_tick / time_to_harden), 1);
        if (harden_lvl == 1)
            STOP_PROCESSING(SSobj, this);
        Waiting.NoSleep(update_icon(UPDATE_ICON_STATE));
        return null;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:110
    public virtual async DmTask<object> handle_shape(mob user = null)
    {
        if (harden_lvl >= 0.8f)
            return FALSE;
        DmList opts = Dm.list();

        foreach (Type conc in Dm.each<Type>(shape_types))
            opts[conc] = image(icon: Dm.initial(conc, nameof(icon)), icon_state: Dm.initial(conc, nameof(icon_state)));

        float cell_x = x, cell_y = y, cell_z = z;
        object choice = await show_radial_menu(
            user,
            this,
            opts,
            uniqueid: Dm.text("concmenu_[]", REF(user)),
            radius: 48,
            custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user),
            require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        // TURF-IDENTITY: tg's src after the menu's sleep is the concrete here (a floor replaced meanwhile is not)
        if (QDELETED(this) || locate(cell_x, cell_y, cell_z) != this)
            return FALSE;
        to_chat(user, I18n.T("You reshape [].", this));
        float old_harden = harden_lvl;
        turf_open_floor_concrete newconc = await ChangeTurf(choice, flags: CHANGETURF_INHERIT_AIR) as turf_open_floor_concrete;
        newconc.harden_lvl = old_harden;
        newconc.check_harden();
        newconc.update_appearance();
        return TRUE;
    }

    // tg: code/game/turfs/open/floor/concrete_floor.dm:136
    public virtual async DmTask<object> check_menu(mob user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(((mob_living)user).incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }
}
