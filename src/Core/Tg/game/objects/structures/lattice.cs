// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lattice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4 (walls, girders, floors, lattice): the lattice and the catwalk (their init, the turf traits and catwalk steps,
// the nearstation area of a lattice built in deep space, the hand-off of items to the turf, wirecutters, deconstruction,
// replace_with_catwalk, the catwalk's cables). Generated live-partial stubs (docs/ai/specs/live-partial.txt, "M5 slice 4"):
// the shuttle moves (M10); E13: rcd_vals and rcd_act (the RCD); gaps ST5: blob_act. The var
// blocks are generated. The mining, lava and boulder catwalks keep their own procs' rows (lavaland, M10+).

// tg: code/game/objects/structures/lattice.dm:1
[DmPath("/obj/structure/lattice")]
public partial class obj_structure_lattice : obj_structure
{
    // tg: code/game/objects/structures/lattice.dm:21  (the first of tg's two Initialize definitions: the second one's ..())
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(length(give_turf_traits)))
        {
            give_turf_traits = string_list(give_turf_traits) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), give_turf_traits));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep_override), Dm.named("footstep", FOOTSTEP_CATWALK)));
        // We check for objects in non-nearspace space in both linters and tests, so we can ignore these checks on mapload for performance
        if (Dm.truthy(mapload) || !isspaceturf(loc))
            return dot;

        area new_turf_area = get_area(loc) as area;
        if (istype(new_turf_area, typeof(area_space)) && !istype(new_turf_area, typeof(area_space_nearstation)))
            set_turf_to_area(loc as turf, GLOB.areas_by_type[typeof(area_space_nearstation)] as area);
        return dot;
    }

    // tg: code/game/objects/structures/lattice.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, deconstruction_hints(user));
        return dot;
    }

    // tg: code/game/objects/structures/lattice.dm:44  (so items on the lattice fall when the lattice is destroyed)
    public override object Destroy(float? force = null)
    {
        turf turfloc = loc as turf;
        object dot = base.Destroy(force);
        if (isturf(turfloc))
        {
            foreach (object thing_that_falls in Dm.each(turfloc))
                Waiting.NoSleep(turfloc.zFall(thing_that_falls as atom_movable));
        }

        area turf_area = get_area(turfloc) as area;
        if (isspaceturf(turfloc) && istype(turf_area, typeof(area_space_nearstation)))
            set_turf_to_area(turfloc, GLOB.areas_by_type[typeof(area_space)] as area);
        return dot;
    }

    // tg: code/game/objects/structures/lattice.dm:55
    public virtual object deconstruction_hints(mob user = null)
    {
        return span_notice(I18n.T("The rods look like they could be <b>cut</b>. There's space for more <i>rods</i> or a <i>tile</i>."));
    }

    // tg: code/game/objects/structures/lattice.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        foreach (obj_structure_lattice LAT in Dm.each<obj_structure_lattice>(loc))
        {
            if (LAT == this)
                continue;
            log_mapping(Dm.text("multiple lattices found in ([], [], [], [])", loc.x, loc.y, loc.z, get_area(LAT)));
            return INITIALIZE_HINT_QDEL;
        }
        return dot;
    }

    // tg: code/game/objects/structures/lattice.dm:69
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        turf underneath = get_turf(this);
        return await underneath.item_interaction(user, tool); //hand this off to the turf instead (for building plating, catwalks, etc)
    }

    // tg: code/game/objects/structures/lattice.dm:73
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return NONE;
        to_chat(user, span_notice(I18n.T("Slicing [] joints ...", name)));
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/lattice.dm:80
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (build_material != null && number_of_mats >= 1)
            Dm.@new(build_material, get_turf(this), number_of_mats);
    }

    // tg: code/game/objects/structures/lattice.dm:107
    public virtual async DmTask replace_with_catwalk()
    {
        DmList post_replacement_callbacks = Dm.list();
        if (Dm.truthy(_listen_lookup?[COMSIG_LATTICE_PRE_REPLACE_WITH_CATWALK]))
            _SendSignal(COMSIG_LATTICE_PRE_REPLACE_WITH_CATWALK, Dm.list(this, post_replacement_callbacks));
        turf turf = loc as turf;
        qdel(this);
        object new_catwalk = Dm.@new<obj_structure_lattice_catwalk>(turf);
        foreach (datum_callback callback in Dm.each<datum_callback>(post_replacement_callbacks))
            await callback.Invoke(new_catwalk);
    }
}

// tg: code/game/objects/structures/lattice.dm:117
public partial class obj_structure_lattice_catwalk : obj_structure_lattice
{
    // tg: code/game/objects/structures/lattice.dm:129
    public override object deconstruction_hints(mob user = null)
    {
        return span_notice(I18n.T("The supporting rods look like they could be <b>cut</b>."));
    }

    // tg: code/game/objects/structures/lattice.dm:132
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T))
            await C.deconstruct();
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    // tg: code/game/objects/structures/lattice.dm:138
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        await base.atom_deconstruct(disassembled);
        turf T = loc as turf;
        foreach (obj_structure_cable C in Dm.each<obj_structure_cable>(T))
            await C.deconstruct();
    }
}

// tg: code/game/objects/structures/lattice.dm:103 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_structure_lattice : obj_structure
{
    // tg: code/game/objects/structures/lattice.dm:103
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        if (Dm.num(current_size) >= STAGE_FOUR)
            await deconstruct();
        return null;
    }
}

// tg: code/game/objects/structures/lattice.dm:84 (interleaved types, rules 1.9; E13: the RCD)
public partial class obj_structure_lattice : obj_structure
{
    // tg: code/game/objects/structures/lattice.dm:84
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF))
            return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", the_rcd.rcd_design_path == typeof(obj_structure_lattice_catwalk) ? 2 : 1));
        return FALSE;
    }

    // tg: code/game/objects/structures/lattice.dm:89
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF))
        {
            object design_structure = rcd_data[RCD_DESIGN_PATH];
            if (Dm.eq(design_structure, typeof(turf_open_floor_plating_rcd)))
            {
                turf T = this.loc as turf;
                if (Dm.truthy(isgroundlessturf(T)))
                {
                    await T.place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
                    qdel(this);
                    return TRUE;
                }
            }
            if (Dm.eq(design_structure, typeof(obj_structure_lattice_catwalk)))
            {
                await replace_with_catwalk();
                return TRUE;
            }
        }
        return FALSE;
    }
}

// tg: code/game/objects/structures/lattice.dm:144
public partial class obj_structure_lattice_catwalk : obj_structure_lattice
{
    // tg: code/game/objects/structures/lattice.dm:144
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("mode", RCD_DECONSTRUCT), Dm.kv("delay", 1 * SECONDS), Dm.kv("cost", 5));
        return FALSE;
    }

    // tg: code/game/objects/structures/lattice.dm:149  (tg names its third parameter passed_mode and compares the design list with
    // RCD_DECONSTRUCT, which never matches: the catwalk is never deconstructed by an RCD, as in tg)
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object passed_mode = rcd_data;
        if (Dm.eq(passed_mode, RCD_DECONSTRUCT))
        {
            turf turf = loc as turf;
            foreach (obj_structure_cable cable_coil in Dm.each<obj_structure_cable>(turf))
                await cable_coil.deconstruct();
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/objects/structures/lattice.dm:157
public partial class obj_structure_lattice_catwalk_mining : obj_structure_lattice_catwalk
{
    // tg: code/game/objects/structures/lattice.dm:162
    public override object deconstruction_hints(mob user = null)
    {
        return null;
    }
}

// tg: code/game/objects/structures/lattice.dm:165
public partial class obj_structure_lattice_catwalk_lava : obj_structure_lattice_catwalk
{
    // tg: code/game/objects/structures/lattice.dm:175
    public override object deconstruction_hints(mob user = null)
    {
        return span_notice("The rods look like they could be <b>cut</b>, but the <i>heat treatment will shatter off</i>. There's space for a <i>tile</i>.");
    }

    // tg: code/game/objects/structures/lattice.dm:178
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ismetaltile(tool))
            return await base.item_interaction(user, tool, modifiers, __extra);
        var attacking_tiles = (obj_item_stack_tile_iron)tool;
        if (!Dm.truthy(await attacking_tiles.use(1)))
        {
            to_chat(user, span_warning(Dm.text("You need one floor tile to build atop [].", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(Dm.text("You construct new plating with [] as support.", this)));
        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);

        var @base = get_turf(this);
        await @base.place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);

        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// Lavaland L2c (D304): the boulder platform. Its emissive blocker stays under DV12 and its warning smoke is not drawn (particles
// are not ported).
// tg: code/game/objects/structures/lattice.dm:195
public partial class obj_structure_lattice_catwalk_boulder : obj_structure_lattice_catwalk
{
    // tg: code/game/objects/structures/lattice.dm:207
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // DEVIATION(DV12): fast_emissive_blocker(src)
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 8)));
        return dot;
    }

    // tg: code/game/objects/structures/lattice.dm:212
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (ismetaltile(tool))
        {
            balloon_alert(user, I18n.T("too unstable!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/structures/lattice.dm:218
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (istype(mover, typeof(obj_structure_ore_box)))
        {
            await self_destruct();
            return TRUE;
        }
        return await base.CanAllowThrough(mover, border_dir);
    }

    // tg: code/game/objects/structures/lattice.dm:224
    public virtual void pre_self_destruct()
    {
        mutable_appearance cracks_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/ore.dmi"), istype(loc, typeof(turf_open_lava_plasma)) ? "plasma_cracks" : "lava_cracks", offset_spokesman: this);
        cracks_overlay.blend_mode = BLEND_INSET_OVERLAY;
        add_overlay(cracks_overlay);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 2 * SECONDS), Dm.named("pixel_y", -16), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(self_destruct)), 2 * SECONDS, file: "code/game/objects/structures/lattice.dm", line: 229);
    }

    /**
     * Handles platforms deleting themselves with a visual effect and message.
     */
    // tg: code/game/objects/structures/lattice.dm:234
    public virtual async DmTask self_destruct()
    {
        visible_message(span_notice(I18n.T("\\The [] sinks and dissapears!", this)));
        playsound(this, Dm.resource("sound/effects/gas_hissing.ogg"), 20);
        // remove_shared_particles(warning_particle): particles are not ported
        await deconstruct();
    }
}

// Gaps ST5 (D333): a blob passes a lattice by.
public partial class obj_structure_lattice : obj_structure
{
    // tg: code/game/objects/structures/lattice.dm:66
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}
