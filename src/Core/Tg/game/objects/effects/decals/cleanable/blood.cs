// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/blood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world): the blood decal (its blood DNA and colour, its drying on SSblood_drying and the drying
// colour, its merging, stepping into it), the old blood, the splatter and the splatter over a window, the wheel tracks, the
// trail holder and its trail pieces, the drip, the footprints and the hit splatter that flies from a wound; M6 slice 5
// (gibbing): the gibs and the old gibs (:514-665). Not here: the rot of dried gibs and the swabs of old gibs (diseases and
// cytology, M8), the reagents of a
// decal (lazy_init_reagents fills a holder whose add_reagent is M8's, the cleanable base's deferred leaf), the emissive
// glow of glowing blood (lighting, D24). The splattered-with-blood mood since gaps MS1. The var blocks are generated (D111).

// tg: code/game/objects/effects/decals/cleanable/blood.dm:1
[DmPath("/obj/effect/decal/cleanable/blood")]
public partial class obj_effect_decal_cleanable_blood : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:11  var/static/list/loc_connections (a type-level static)
    private static ref DmList loc_connections => ref TgState.Current.obj_effect_decal_cleanable_blood__loc_connections;

    /*
     * diseases - List of diseases to add to this decal on init
     * blood_or_dna - Either a blood type which will get added, or a full list of DNA
     */
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:44
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        DmList diseases = Dm.arg<DmList>(__extra, 0);
        object blood_or_dna = Dm.arg(__extra, 1) ?? get_default_blood_type();
        bool can_hold_viruses = true;
        if (blood_or_dna is datum_blood_type single_type)
            can_hold_viruses = Dm.truthy(Dm.bitand(single_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA));
        else if (islist(blood_or_dna))
        {
            can_hold_viruses = false;
            DmList dna_list = (DmList)blood_or_dna;
            foreach (object blood_key in Dm.each(dna_list))
            {
                datum_blood_type blood_type = dna_list[blood_key] as datum_blood_type;
                if (Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)))
                {
                    can_hold_viruses = true;
                    break;
                }
            }
        }
        object dot = base.Initialize(mapload, can_hold_viruses ? diseases : null);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        if (islist(blood_or_dna))
            Waiting.NoSleep(add_blood_DNA((DmList)blood_or_dna));
        else if (blood_or_dna is datum_blood_type default_type)
            Waiting.NoSleep(add_blood_DNA(Dm.list(Dm.kv(default_type.dna_string, default_type))));

        if (Dm.truthy(dried))
            Waiting.NoSleep(dry());
        else if (Dm.truthy(can_dry))
        {
            total_dry_time = drying_time;
            START_PROCESSING(SSblood_drying, this);
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));

        if (Dm.truthy(bloodiness) || Dm.truthy(GET_ATOM_BLOOD_DECAL_LENGTH(this)))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:77
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSblood_drying, this);
        return base.Destroy(force);
    }

    /// Returns the default blood type for this decal for maploaded decals
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:81
    public virtual object get_default_blood_type()
    {
        return random_human_blood_type();
    }

    // Add "bloodiness" of this blood's type to the human's shoes
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:85  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (Dm.truthy(dried))
            return;

        if (isliving(AM) && bloodiness >= 40 && Dm.truthy(AM._listen_lookup?[COMSIG_STEP_ON_BLOOD])) // SEND_SIGNAL's own check
            AM._SendSignal(COMSIG_STEP_ON_BLOOD, Dm.list(AM, this));
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:94
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.initial(this, (obj_effect_decal_cleanable_blood p) => p.name);
        if (Dm.truthy(base_name))
            name = Dm.text("[] []", base_name, get_blood_string());
        if (Dm.truthy(base_suffix))
            name = Dm.text("[] []", Dm.truthy(base_name) ? name : get_blood_string(), base_suffix);
        if (Dm.truthy(dried) && Dm.truthy(dry_prefix))
            name = Dm.text("[] []", dry_prefix, name);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:104
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.initial(this, (obj_effect_decal_cleanable_blood p) => p.desc);
        if (Dm.truthy(dried) && Dm.truthy(dry_desc))
            desc = dry_desc;
        return dot;
    }

    /// Returns a string of all the blood reagents in the blood
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:111
    public virtual object get_blood_string()
    {
        DmList blood_DNA = GET_ATOM_BLOOD_DECALS(this);
        DmList all_blood_names = Dm.list();
        foreach (object dna_sample in Dm.each(blood_DNA))
        {
            datum_blood_type blood_type = blood_DNA[dna_sample] as datum_blood_type;
            object names = all_blood_names;
            Dm.combine(ref names, LOWER_TEXT(blood_type.get_blood_name()));
        }
        return english_list(all_blood_names, nothing_text: "blood");
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:119
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(icon_state) && Dm.truthy(emissive_alpha) && emissive_alpha < alpha && !Dm.truthy(dried))
        {
            // . += blood_emissive(icon, icon_state): the glow of glowing blood (emissive_appearance, lighting, D24)
            Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:122", "update_overlays: blood_emissive, the glow of glowing blood (emissives, D24)");
        }
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:127
    public override async DmTask<object> lazy_init_reagents()
    {
        if (Dm.truthy(reagents))
            return reagents;

        DmList blood_DNA = GET_ATOM_BLOOD_DNA(this);
        DmList reagents_to_add = Dm.list();
        foreach (object dna_sample in Dm.each(blood_DNA))
        {
            datum_blood_type blood_type = blood_DNA[dna_sample] as datum_blood_type;
            Dm.append(ref reagents_to_add, blood_type.reagent_type);
        }

        create_reagents(round(bloodiness * BLOOD_TO_UNITS_MULTIPLIER, CHEMICAL_VOLUME_ROUNDING));
        // reagents.add_reagent(reagent_type, amount, data = list(BLOOD_DATA_DNA = pick(blood_DNA))) for each blood type
        // (:139-143): the holder's add_reagent is not in the port (reagents, M8)
        Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:139", "lazy_init_reagents: the blood reagents of the decal in its holder (reagents, M8)");
        return reagents;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:146
    public override object replace_decal(obj_effect_decal_cleanable other = null)
    {
        obj_effect_decal_cleanable_blood merger = other as obj_effect_decal_cleanable_blood;
        if (Dm.truthy(merger.dried)) // New blood will lie on dry blood
            return FALSE;
        return base.replace_decal(other);
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:151
    public override async DmTask handle_merge_decal(obj_effect_decal_cleanable merger = null)
    {
        await base.handle_merge_decal(merger);
        obj_effect_decal_cleanable_blood blood_merger = (obj_effect_decal_cleanable_blood)merger;
        await blood_merger.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
        await blood_merger.adjust_bloodiness(bloodiness);
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:156
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(dried) || !Dm.truthy(can_dry))
            return PROCESS_KILL;

        if (Dm.truthy(decay_bloodiness))
            Waiting.NoSleep(adjust_bloodiness(-bloodiness / drying_time * Dm.num(seconds_per_tick) * 1 * SECONDS, ignore_timer: TRUE));

        drying_time -= Dm.num(seconds_per_tick) * 1 * SECONDS;
        if (drying_time <= 0)
            Waiting.NoSleep(dry());
        return null;
    }

    /// Slows down the drying time by a given amount,
    /// then updates the effect, meaning the animation will slow down
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:168
    public virtual async DmTask slow_dry(float by_amount)
    {
        drying_time += by_amount;
        total_dry_time = Dm.add(total_dry_time, by_amount);
        await update_atom_colour();
    }

    /// This is what actually "dries" the blood
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:174
    public virtual async DmTask<object> dry()
    {
        dried = TRUE;
        // Not deleting as doing so would cause reagents to get lazyloaded again
        if (reagents is datum_reagents holder)
            await holder.clear_reagents();
        update_appearance();
        await update_atom_colour();
        STOP_PROCESSING(SSblood_drying, this);
        return TRUE;
    }

    /// Increments or decrements the bloodiness value
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:183
    public virtual async DmTask<object> adjust_bloodiness(float by_amount, object ignore_timer = null)
    {
        ignore_timer ??= FALSE;
        if (by_amount == 0)
            return FALSE;

        if (QDELING(this))
            return FALSE;

        bloodiness = clamp((bloodiness + by_amount), 0, BLOOD_POOL_MAX);
        if (bloodiness == 0)
            await dry();
        else if (Dm.truthy(ignore_timer))
            await slow_dry(5 * SECONDS * by_amount * BLOOD_TO_UNITS_MULTIPLIER);
        return TRUE;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:197
    public override async DmTask update_atom_colour()
    {
        await base.update_atom_colour();
        await update_blood_color();
    }

    // When color changes we need to update the drying animation
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:202
    public virtual async DmTask<object> update_blood_color()
    {
        object base_color = BLOOD_COLOR_RED;
        // Get a default color based on DNA if it ends up unset somehow
        DmList blood_DNA = GET_ATOM_BLOOD_DECALS(this);
        if (!Dm.truthy(Dm.length(blood_DNA))) // In case we're only composed of stuff that doesn't normally have a visual
            blood_DNA = GET_ATOM_BLOOD_DNA(this);
        if (Dm.truthy(Dm.length(blood_DNA)))
            base_color = await get_color_from_blood_list(blood_DNA);

        if (!Dm.truthy(color))
            color = base_color;

        // Stop ongoing drying animations
        animate(this);

        object dried_color = get_dried_color(base_color);
        // If it's dried (or about to dry) we can just set color directly
        if (Dm.truthy(dried) || drying_time <= 0)
        {
            color = dried_color;
            return TRUE;
        }

        if (!Dm.truthy(total_dry_time))
            return FALSE;

        float dry_coeff = round(1 - drying_time / Dm.num(total_dry_time), 0.01f);
        // Otherwise set the color to what it should be at the current drying progress, then animate down to the dried color if we can
        color = BlendRGB(color, dried_color, dry_coeff);
        if (Dm.truthy(can_dry))
            animate(this, Dm.named("time", drying_time), Dm.named("color", dried_color));
        return TRUE;
    }

    /// Calculates and returns either an RGB or a matrix color for dried blood, depending on whever our current color is RGB or matrix
    /// Because BYOND does *not* like animating from text to matrix and vice versa
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:235
    public virtual object get_dried_color(object base_color)
    {
        DmList starting_color = Dm.rgb2num(base_color as string);

        if (!Dm.truthy(starting_color))
            starting_color = Dm.list(255, 255, 255);

        // We want a fixed offset for a fixed drop in color intensity, plus a scaling offset based on our strongest color
        // The scaling offset helps keep dark colors from turning black, while also ensurse bright colors don't stay super bright
        float red = Dm.num(starting_color[1]);
        float green = Dm.num(starting_color[2]);
        float blue = Dm.num(starting_color[3]);
        float max_color = max(red, green, blue);
        float red_offset = 50 + (75 * (red / max_color));
        float green_offset = 50 + (75 * (green / max_color));
        float blue_offset = 50 + (75 * (blue / max_color));

        // If the color is already decently dark, we should reduce the offsets even further
        // This is intended to prevent already dark blood (mixed blood in particular) from becoming full black
        float strength = red + green + blue;
        if (strength <= 192)
        {
            red_offset *= 0.5f;
            green_offset *= 0.5f;
            blue_offset *= 0.5f;
        }

        // Finally, get this show on the road
        return rgb(
            clamp(red - red_offset, 0, 255),
            clamp(green - green_offset, 0, 255),
            clamp(blue - blue_offset, 0, 255));
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:276
public partial class obj_effect_decal_cleanable_blood_splatter_over_window : obj_effect_decal_cleanable_blood_splatter
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:276
    public override object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return isgroundlessturf(here_turf);
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:288
public partial class obj_effect_decal_cleanable_blood_trail_holder : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:302
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        icon_state = "nothing";
        update_appearance(); // Cut possible overlays
        if (Dm.truthy(mapload))
            Waiting.NoSleep(add_dir_to_trail(Dm.num(dir)));
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:311
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(trail_components);
        return base.Destroy(force);
    }

    /**
     * Returns the trail component corresponding to the given direction
     *
     * * for_dir: The direction to get the trail for
     * * check_reverse: If TRUE, will also check for the reverse direction
     * For example if you pass dir = EAST it will return the first EAST or WEST trail component
     * * check_diagonals: If TRUE, will also check for any diagonal directions
     * For example if you pass dir = EAST it will return the first EAST, NORTHEAST, or SOUTHEAST trail component
     * * check_reverse_diagonals: If TRUE, will also check for any reverse diagonal directions
     * For example if you pass dir = EAST it will return the first SOUTHEAST, EAST, NORTHEAST, WEST, SOUTHWEST, or NORTHWEST trail component
     */
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:328
    public virtual obj_effect_decal_cleanable_blood_trail get_trail_component(float for_dir, object check_reverse = null, object check_diagonals = null, object check_reverse_diagonals = null)
    {
        obj_effect_decal_cleanable_blood_trail dot = LAZYACCESS(trail_components, Dm.text("[]", for_dir)) as obj_effect_decal_cleanable_blood_trail;
        if (dot != null)
            return dot;

        if (Dm.truthy(check_reverse))
        {
            dot = LAZYACCESS(trail_components, Dm.text("[]", REVERSE_DIR(for_dir))) as obj_effect_decal_cleanable_blood_trail;
            if (dot != null)
                return dot;
        }

        if (!Dm.truthy(check_diagonals))
            return null;

        foreach (object comp_dir_txt in Dm.each(trail_components))
        {
            float comp_dir = Dm.num(text2num(comp_dir_txt));
            if (comp_dir <= 0)
                continue;

            if (Dm.truthy(Dm.bitand(comp_dir, for_dir)))
                return LAZYACCESS(trail_components, comp_dir_txt) as obj_effect_decal_cleanable_blood_trail;

            if (Dm.truthy(check_reverse_diagonals) && Dm.truthy(Dm.bitand(comp_dir, REVERSE_DIR(for_dir))))
                return LAZYACCESS(trail_components, comp_dir_txt) as obj_effect_decal_cleanable_blood_trail;
        }

        return null;
    }

    /**
     * Add a new direction to this trail
     *
     * * new_dir: The direction to add
     * * source - Mob we're sourcing blood from, if any
     * * blood_to_add - Amount of bloodiness to give to the new component. Does not adjust this decal's own bloodiness
     * * half_piece - If TRUE, only creates start of a trail. Does not support corners (diagonal directions)
     * This can be a cardinal direction, a diagonal direction, or a negative number to denote a cardinal direction angled 45 degrees.
     *
     * Returns the new trail, a [/obj/effect/decal/cleanable/blood/trail]
     */
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:365
    public virtual async DmTask<obj_effect_decal_cleanable_blood_trail> add_dir_to_trail(float new_dir = NORTH, mob_living source = null, float blood_to_add = BLOOD_AMOUNT_PER_DECAL * 0.1f, object half_piece = null)
    {
        half_piece ??= FALSE;
        bool check_reverse = true;
        // Do not check the reverse dir if we're a diagonal corner
        if (new_dir > 0 && !Dm.@in(new_dir, GLOB.cardinals))
            check_reverse = false;
        obj_effect_decal_cleanable_blood_trail new_trail = get_trail_component(new_dir, check_reverse: check_reverse ? TRUE : FALSE, check_diagonals: half_piece);
        DmList blood_DNA = GET_ATOM_BLOOD_DNA(this);
        if (source != null)
        {
            // Source's DNA goes first as to override possible matches in non-enzyme DNA
            blood_DNA = Dm.bitor(source.get_blood_dna_list() ?? Dm.list(), blood_DNA) as DmList;
        }

        if (new_trail != null)
        {
            // If we found a full trail (straight or diagonal), or a half trail that fully overlaps with us, abort this
            if (!Dm.truthy(new_trail.half_piece) || Dm.truthy(half_piece) && LAZYACCESS(trail_components, Dm.text("[]", new_dir)) == new_trail)
            {
                await new_trail.adjust_bloodiness(blood_to_add);
                if (source != null)
                    await new_trail.add_mob_blood(source);
                return new_trail;
            }

            // We've found a mirrored half-piece, so we should merge into a full piece
            // Alternatively, we've found a half piece while being a straight piece that overlaps with it
            // in which case we just overlap with it which should get us the same full piece
            new_trail.half_piece = FALSE;
            new_trail.update_appearance();
            await new_trail.adjust_bloodiness(blood_to_add);
            if (source != null)
                await new_trail.add_mob_blood(source);
            return new_trail;
        }

        // There's a chance that we're on the same tile as a diagonal corner, in which case we need to check for those too
        if (Dm.truthy(half_piece) && new_dir > 0)
        {
            float first_dir = Dm.truthy(Dm.bitand(new_dir, Dm.bitor(NORTH, SOUTH))) ? EAST : NORTH;
            float second_dir = Dm.truthy(Dm.bitand(new_dir, Dm.bitor(NORTH, SOUTH))) ? WEST : SOUTH;
            new_trail = get_trail_component(Dm.bitor(new_dir, first_dir)) ?? get_trail_component(Dm.bitor(new_dir, second_dir));
            // Found a diagonal overlapping with us, abort
            if (new_trail != null)
            {
                await new_trail.adjust_bloodiness(blood_to_add);
                if (source != null)
                    await new_trail.add_mob_blood(source);
                return new_trail;
            }

            // Look for perpendicular pieces to merge into a diagonal with
            new_trail = get_trail_component(first_dir);
            if (!Dm.truthy(new_trail?.half_piece))
                new_trail = get_trail_component(second_dir);

            if (Dm.truthy(new_trail?.half_piece))
            {
                new_trail.half_piece = FALSE;
                LAZYREMOVE(ref trail_components, Dm.text("[]", new_trail.dir));
                await new_trail.setDir(Dm.bitor(new_dir, new_trail.dir));
                LAZYSET(ref trail_components, Dm.text("[]", new_trail.dir), new_trail);
                new_trail.update_appearance();
                await new_trail.adjust_bloodiness(blood_to_add);
                if (source != null)
                    await new_trail.add_mob_blood(source);
                return new_trail;
            }
        }

        new_trail = Dm.@new<obj_effect_decal_cleanable_blood_trail>(this, source == null ? null : await source.get_static_viruses(), blood_DNA);
        if (Dm.truthy(half_piece))
        {
            new_trail.half_piece = TRUE;
            new_trail.update_appearance();
        }

        await new_trail.adjust_bloodiness(blood_to_add - new_trail.bloodiness);

        if (new_dir > 0)
        {
            // add some free sprite variation by flipping it around
            if (Dm.@in(new_dir, GLOB.cardinals) && prob(50) && !Dm.truthy(half_piece))
                await new_trail.setDir(REVERSE_DIR(new_dir));
            // otherwise the dir is the same
            else
                await new_trail.setDir(new_dir);
        }
        // negative dirs denote "straight diagonal" dirs
        else
        {
            float real_dir = abs(new_dir);
            await new_trail.setDir(Dm.bitand(real_dir, Dm.bitor(EAST, WEST)));
            if (real_dir == NORTHEAST)
                new_trail.transform = ((matrix)new_trail.transform).Turn(-45);
            else if (real_dir == NORTHWEST)
                new_trail.transform = ((matrix)new_trail.transform).Turn(45);
            else if (real_dir == SOUTHEAST)
                new_trail.transform = ((matrix)new_trail.transform).Turn(-135);
            else if (real_dir == SOUTHWEST)
                new_trail.transform = ((matrix)new_trail.transform).Turn(135);
        }

        LAZYSET(ref trail_components, Dm.text("[]", new_dir), new_trail);
        Dm.append(vis_contents, new_trail);
        return new_trail;
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:455
public partial class obj_effect_decal_cleanable_blood_trail : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:470
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Despite having VIS_INHERIT_PLANE, our emissives still inherit our plane offset, so we need to inherit our parent's offset to have them render correctly
        if (loc is obj_effect_decal_cleanable_blood_trail_holder)
        {
            SET_PLANE_EXPLICIT(this, Dm.num(Dm.initial(this, (obj_effect_decal_cleanable_blood_trail p) => p.plane)), loc);
            if (Dm.truthy(emissive_alpha) && !Dm.truthy(dried))
                update_appearance(); // correct our emissive
            return dot;
        }

        // #ifndef UNIT_TESTS (:482-486): a mapped trail outside a holder is logged and deleted
        if (Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of a trail holder at []!", this, AREACOORD(this)));
            return INITIALIZE_HINT_QDEL;
        }

        stack_trace(Dm.text("[] spawned outside of a trail holder at []!", this, AREACOORD(this)), "code/game/objects/effects/decals/cleanable/blood.dm", 488);
        return INITIALIZE_HINT_QDEL;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:491
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.text("A []trail of [].", Dm.truthy(dried) ? "dried " : "", get_blood_string());
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:495
    public override async DmTask<object> lazy_init_reagents()
    {
        if (loc is not obj_effect_decal_cleanable_blood_trail_holder)
            return await base.lazy_init_reagents();
        return null;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:499
    public override async DmTask<object> adjust_bloodiness(float by_amount, object ignore_timer = null)
    {
        ignore_timer ??= FALSE;
        object dot = await base.adjust_bloodiness(by_amount, ignore_timer);
        if (Dm.truthy(very_bloody) || bloodiness < 0.25f * BLOOD_AMOUNT_PER_DECAL)
            return dot;

        very_bloody = TRUE;
        icon_state = pick("trails_1", "trails_2") as string;
        base_icon_state = icon_state;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:509
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        if (Dm.truthy(half_piece))
            icon_state = Dm.text("[]_start", base_icon_state);
        else
            icon_state = base_icon_state as string;
        return await base.update_icon(updates, __extra);
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:676
public partial class obj_effect_decal_cleanable_blood_footprints : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:695
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        icon_state = ""; // All of the footprint visuals come from overlays
        if (Dm.truthy(mapload))
            entered_dirs = Dm.bitor(entered_dirs, dir); // Keep the same appearance as in the map editor
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:704
    public override object get_blood_string()
    {
        return null;
    }

    //Rotate all of the footprint directions too
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:708
    public override async DmTask setDir(object newdir = null)
    {
        if (Dm.eq(dir, newdir))
        {
            await base.setDir(newdir);
            return;
        }

        float ang_change = Dm.num(Dm.sub(dir2angle(newdir), dir2angle(dir)));
        float old_entered_dirs = entered_dirs;
        float old_exited_dirs = exited_dirs;
        entered_dirs = NONE;
        exited_dirs = NONE;

        foreach (object Ddir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(old_entered_dirs, Ddir)))
                entered_dirs = Dm.bitor(entered_dirs, angle2dir_cardinal(Dm.num(dir2angle(Ddir)) + ang_change));
            if (Dm.truthy(Dm.bitand(old_exited_dirs, Ddir)))
                exited_dirs = Dm.bitor(exited_dirs, angle2dir_cardinal(Dm.num(dir2angle(Ddir)) + ang_change));
        }

        update_appearance();
        await base.setDir(newdir);
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:726
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        DmList bloody_footprints_cache = obj_effect_decal_cleanable_blood_footprints__update_overlays__bloody_footprints_cache;
        string icon_state_to_use = "blood";
        if (Dm.truthy(LAZYACCESS(species_types, BODYPART_ID_DIGITIGRADE)))
            icon_state_to_use += "claw";
        else if (Dm.truthy(LAZYACCESS(species_types, SPECIES_MONKEY)))
            icon_state_to_use += "paw";
        else if (Dm.truthy(LAZYACCESS(species_types, "bot")))
            icon_state_to_use += "bot";

        foreach (object Ddir in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(entered_dirs, Ddir)))
            {
                string enter_state = Dm.text("entered-[]-[]", icon_state_to_use, Ddir);
                image bloodstep_overlay = bloody_footprints_cache[enter_state] as image;
                if (bloodstep_overlay == null)
                {
                    bloodstep_overlay = image(icon, icon_state: Dm.text("[]1", icon_state_to_use), dir: Ddir);
                    bloody_footprints_cache[enter_state] = bloodstep_overlay;
                }
                dot = Dm.add(dot, bloodstep_overlay);

                if (Dm.truthy(emissive_alpha) && emissive_alpha < alpha && !Dm.truthy(dried))
                {
                    // the emissive copy of the step (blood_emissive, lighting, D24)
                    Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:743", "update_overlays: the emissive footprint of glowing blood (emissives, D24)");
                }
            }

            if (Dm.truthy(Dm.bitand(exited_dirs, Ddir)))
            {
                string exit_state = Dm.text("exited-[]-[]", icon_state_to_use, Ddir);
                image bloodstep_overlay = bloody_footprints_cache[exit_state] as image;
                if (bloodstep_overlay == null)
                {
                    bloodstep_overlay = image(icon, icon_state: Dm.text("[]2", icon_state_to_use), dir: Ddir);
                    bloody_footprints_cache[exit_state] = bloodstep_overlay;
                }
                dot = Dm.add(dot, bloodstep_overlay);

                if (Dm.truthy(emissive_alpha) && emissive_alpha < alpha && !Dm.truthy(dried))
                {
                    // the emissive copy of the step (blood_emissive, lighting, D24)
                    Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:758", "update_overlays: the emissive footprint of glowing blood (emissives, D24)");
                }
            }
        }
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:765
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(LAZYLEN(species_types)) + Dm.num(LAZYLEN(shoe_types)) == 0)
            return dot;

        dot = Dm.add(dot, "You recognise the footprints as belonging to:");
        foreach (object sole_path in Dm.each(shoe_types))
        {
            Type sole = sole_path as Type;
            object article = Dm.initial(sole, (obj_item_clothing_shoes p) => p.article);
            if (!Dm.truthy(article))
                article = Dm.eq(Dm.initial(sole, (obj_item_clothing_shoes p) => p.gender), PLURAL) ? "Some" : "A";
            // [icon2html(initial(sole.icon), user, initial(sole.icon_state))]: the shoe's picture in the chat line (chat icons, M9)
            Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:773", "examine: icon2html of the shoe, its picture in the chat line (the 3D client's chat is text: chat icons, M10+)");
            dot = Dm.add(dot, Dm.text("[] [] <B>[]</B>.", "", article, Dm.initial(sole, (obj_item_clothing_shoes p) => p.name)));
        }

        foreach (object species in Dm.each(species_types))
        {
            if (Dm.eq(species, "unknown"))
                dot = Dm.add(dot, "&bull; Some <B>creature's feet</B>.");
            else if (Dm.eq(species, SPECIES_MONKEY))
                dot = Dm.add(dot, "&bull; Some <B>monkey feet</B>.");
            else if (Dm.eq(species, SPECIES_HUMAN))
                dot = Dm.add(dot, "&bull; Some <B>human feet</B>.");
            else
                dot = Dm.add(dot, Dm.text("&bull; Some <B>[] feet</B>.", species));
        }
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:727  var/static/list/bloody_footprints_cache (a proc-level static, rules 10.6)
    private static DmList obj_effect_decal_cleanable_blood_footprints__update_overlays__bloody_footprints_cache => TgState.Current.obj_effect_decal_cleanable_blood_footprints__update_overlays__bloody_footprints_cache;
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:791
public partial class obj_effect_decal_cleanable_blood_hitsplatter : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:822
    [DmParams("mapload", "diseases", "blood_or_dna", "splatter_strength")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object splatter_strength = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        leave_blood = Dm.truthy(has_blood_flag(GET_ATOM_BLOOD_DNA(this), BLOOD_COVER_TURFS)) ? TRUE : FALSE;
        prev_loc = loc as turf; //Just so we are sure prev_loc exists
        if (Dm.truthy(splatter_strength))
            this.splatter_strength = Dm.num(splatter_strength);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:831
    public virtual async DmTask expire()
    {
        if (isturf(loc) && !Dm.truthy(skip))
        {
            playsound(this, Dm.resource("sound/effects/wounds/splatter.ogg"), 60, TRUE, -1);
            await loc.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
        }
        qdel(this);
    }

    /// Set the splatter up to fly through the air until it rounds out of steam or hits something
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:838
    public virtual void fly_towards(turf target_turf, float range)
    {
        flight_dir = get_dir(this, target_turf);
        datum_move_loop loop = GLOB.move_manager.move_towards(this, target_turf, splatter_speed, timeout: splatter_speed * range, priority: MOVEMENT_ABOVE_SPACE_PRIORITY, flags: MOVEMENT_LOOP_START_FAST) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move));
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(post_move));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(loop_done));
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:845  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void pre_move(datum_move_loop source = null)
    {
        prev_loc = loc as turf;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:849  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void post_move(datum_move_loop source = null)
    {
        if (loc == prev_loc || !isturf(loc))
            return;

        foreach (atom_movable iter_atom in Dm.each<atom_movable>(loc))
        {
            if (Dm.truthy(hit_endpoint))
                return;
            if (iter_atom == this || Dm.truthy(iter_atom.invisibility) || iter_atom.alpha <= 0 || (isobj(iter_atom) && !Dm.truthy(iter_atom.density)))
                continue;
            if (splatter_strength <= 0)
                break;
            Waiting.NoSleep(iter_atom.add_blood_DNA(GET_ATOM_BLOOD_DNA(this)));
            if (isliving(iter_atom))
            {
                mob_living splatted = (mob_living)iter_atom;
                Waiting.NoSleep(splatted.add_mood_event("splattered_with_blood", typeof(datum_mood_event_splattered_with_blood)));
            }
        }

        splatter_strength--;
        // we used all our blood so go away
        if (splatter_strength <= 0)
        {
            Waiting.NoSleep(expire());
            return;
        }

        if (!Dm.truthy(leave_blood))
        {
            Waiting.NoSleep(loc.add_blood_DNA(GET_ATOM_BLOOD_DNA(this)));
            return;
        }

        // make a trail
        obj_effect_decal_cleanable_blood fly_trail = Dm.@new<obj_effect_decal_cleanable_blood>(loc, null, GET_ATOM_BLOOD_DNA(this));
        fly_trail.dir = dir;
        if (Dm.truthy(ISDIAGONALDIR(flight_dir)))
            fly_trail.transform = ((matrix)fly_trail.transform).Turn((Dm.eq(flight_dir, NORTHEAST) || Dm.eq(flight_dir, SOUTHWEST)) ? 135 : 45);
        fly_trail.icon_state = pick("trails_1", "trails_2") as string;
        Waiting.NoSleep(fly_trail.adjust_bloodiness(fly_trail.bloodiness * -0.66f));
        fly_trail.update_appearance();
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:883  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void loop_done(datum source = null)
    {
        if (!QDELETED(this))
            Waiting.NoSleep(expire());
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:888
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        if (!iswallturf(bumped_atom) && bumped_atom is not obj_structure_window)
        {
            await expire();
            return null;
        }

        if (bumped_atom is obj_structure_window bumped_window)
        {
            if (!Dm.truthy(bumped_window.fulltile))
            {
                hit_endpoint = TRUE;
                await expire();
                return null;
            }
        }

        hit_endpoint = TRUE;
        if (!isturf(prev_loc)) // This will only happen if prev_loc is not even a turf, which is highly unlikely.
        {
            await abstract_move(bumped_atom);
            await expire();
            return null;
        }

        await abstract_move(bumped_atom);
        skip = TRUE;
        //Adjust pixel offset to make splatters appear on the wall
        if (bumped_atom is obj_structure_window window)
        {
            if (Dm.truthy(await land_on_window(window)))
                return null;
        }

        if (!Dm.truthy(leave_blood))
        {
            await prev_loc.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
            return null;
        }

        obj_effect_decal_cleanable_blood_splatter_over_window final_splatter = Dm.@new<obj_effect_decal_cleanable_blood_splatter_over_window>(prev_loc, null, GET_ATOM_BLOOD_DNA(this));
        final_splatter.pixel_x = Dm.eq(dir, EAST) ? 32 : (Dm.eq(dir, WEST) ? -32 : 0);
        final_splatter.pixel_y = Dm.eq(dir, NORTH) ? 32 : (Dm.eq(dir, SOUTH) ? -32 : 0);
        return null;
    }

    /// A special case for hitsplatters hitting windows, since those can actually be moved around, store it in the window and slap it in the vis_contents
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:924
    public virtual async DmTask<object> land_on_window(obj_structure_window the_window)
    {
        if (!Dm.truthy(leave_blood))
        {
            await the_window.add_blood_DNA(GET_ATOM_BLOOD_DNA(this));
            return TRUE;
        }

        if (!Dm.truthy(the_window.fulltile))
            return FALSE;

        // Spawn in the window so we don't merge with a floor splatter and delete ourselves before attaching.
        obj_effect_decal_cleanable final_splatter = Dm.@new<obj_effect_decal_cleanable_blood_splatter_over_window>(the_window, null, GET_ATOM_BLOOD_DNA(this));
        Dm.append(the_window.vis_contents, final_splatter);
        await expire();
        return TRUE;
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:514  (M6 slice 5, gibbing; the vars are generated, D111)
public partial class obj_effect_decal_cleanable_blood_gibs : obj_effect_decal_cleanable_blood
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:539
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        leave_blood = Dm.truthy(has_blood_flag(GET_ATOM_BLOOD_DNA(this), BLOOD_COVER_TURFS)) ? TRUE : FALSE;
        if (Dm.truthy(squishy))
            Waiting.NoSleep(AddElement(typeof(datum_element_squish_sound)));
        RegisterSignal(this, COMSIG_MOVABLE_PIPE_EJECTING, nameof(on_pipe_eject));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    /// Don't override our reagents with our bloodtype ones, if bloodtypes want unique reagents they need to do it themselves (like oil)
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:550
    public override async DmTask<object> lazy_init_reagents()
    {
        if (Dm.truthy(reagents))
            return reagents;
        return await init_reagents(decal_reagent, reagent_amount);
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:555
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(has_overlay))
            return dot;
        mutable_appearance gib_overlay = mutable_appearance(icon, Dm.text("[]-overlay", icon_state), appearance_flags: Dm.bitor(KEEP_APART, RESET_COLOR)) as mutable_appearance;
        if (Dm.truthy(dried))
            gib_overlay.color = COLOR_GRAY;
        else if (Dm.truthy(total_dry_time))
            gib_overlay.color = BlendRGB(COLOR_WHITE, COLOR_GRAY, 1 - drying_time / Dm.num(total_dry_time));
        dot = Dm.add(dot, gib_overlay);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:566
    public override object get_blood_string()
    {
        return null;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:569
    public override object Destroy(float? force = null)
    {
        streak_diseases = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:573
    public override object replace_decal(obj_effect_decal_cleanable other = null)
    {
        return FALSE; //Never fail to place us
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:576
    public override async DmTask<object> dry()
    {
        object dot = await base.dry();
        if (!Dm.truthy(dot))
            return dot;
        await AddComponent(typeof(datum_component_rot), 0, 5 * MINUTES, 0.7f); // gaps VI1
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:583
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:586
    [SignalHandler]
    public virtual void on_pipe_eject(atom source = null, object direction = null)
    {
        DmList dirs;
        if (Dm.truthy(direction))
            dirs = Dm.list(direction, turn(direction, -45), turn(direction, 45));
        else
            dirs = GLOB.alldirs.Copy();

        Waiting.NoSleep(streak(dirs));
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:597
    public virtual async DmTask streak(DmList directions, object mapload = null)
    {
        mapload ??= FALSE;
        if (Dm.truthy(_listen_lookup?[COMSIG_GIBS_STREAK]))
            this._SendSignal(COMSIG_GIBS_STREAK, Dm.list(this, directions));
        object direction = pick(directions);
        float delay = 2;
        float range = Dm.num(Dm.pick_weighted((100, 0f), (200, 1f), (150, 2f), (50, 3f), (17, 50f))); //the 3% chance of 50 steps is intentional and played for laughs.
        if (!Dm.truthy(await step_to(this, get_step(this, Dm.num(direction)), 0)))
            return;

        if (!Dm.truthy(mapload))
        {
            datum_move_loop loop = GLOB.move_manager.move_to(this, get_step(this, Dm.num(direction)), delay: delay, timeout: range * delay, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
            if (Dm.truthy(leave_blood))
                RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(spread_movement_effects));
            return;
        }

        for (int i = 1; i <= range; i++)
        {
            if (Dm.truthy(leave_blood))
                create_splatter();

            if (!Dm.truthy(await step_to(this, get_step(this, Dm.num(direction)), 0)))
                break;
        }
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:618
    public virtual void create_splatter()
    {
        turf my_turf = get_turf(this);
        if (!isgroundlessturf(my_turf) || Dm.truthy(GET_TURF_BELOW(my_turf)))
            Dm.@new<obj_effect_decal_cleanable_blood_splatter>(my_turf, streak_diseases, GET_ATOM_BLOOD_DNA(this));
    }

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:623
    [SignalHandler]
    public virtual void spread_movement_effects(datum_move_loop source = null)
    {
        if (!Dm.truthy(NeverShouldHaveComeHere(loc as turf)))
            Dm.@new<obj_effect_decal_cleanable_blood_splatter>(loc, streak_diseases, GET_ATOM_BLOOD_DNA(this));
    }
}

// tg: code/game/objects/effects/decals/cleanable/blood.dm:652
public partial class obj_effect_decal_cleanable_blood_gibs_old : obj_effect_decal_cleanable_blood_gibs
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:661
    [DmParams("mapload", "diseases", "blood_or_dna")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        // AddElement(/datum/element/swabable, CELL_LINE_TABLE_SLUDGE, CELL_VIRUS_TABLE_GENERIC, rand(2,4), 10): the cell lines
        // a swab takes from old gibs (cytology, M8)
        Dm.Deferred("code/game/objects/effects/decals/cleanable/blood.dm:664", "Initialize: AddElement(/datum/element/swabable), the cell lines of old gibs (cytology, M8)");
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/decals/cleanable/blood.dm:11  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_decal_cleanable_blood__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_decal_cleanable_blood.on_entered)));

    // tg: code/game/objects/effects/decals/cleanable/blood.dm:727  var/static/list/bloody_footprints_cache = list()
    internal DmList obj_effect_decal_cleanable_blood_footprints__update_overlays__bloody_footprints_cache = Dm.list();
}
