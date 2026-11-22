// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: the wall's init and its Bumped/Exited signals. M5 slice 4: its construction and destruction (atom_destruction,
// dismantle_wall, break_wall, devastate_wall, the hand, paw and telekinesis clicks, item_interaction with try_clean, try_decon
// and try_build_support, add_dent, the wrench's corner adjustment). M5x slice 1: ex_act (explosions); slice 5: the singularity's pull. Still generated live-partial stubs (docs/ai/specs/live-partial.txt,
// P4): examine and the context tips (M9), leaning (M9), hulks, blobs, cult, rust,
// drills and mechs (M10+). E13: rcd_vals and rcd_act (the RCD). Silicons SI1c: acid_melt. The var block is generated.

// tg: code/game/turfs/closed/walls.dm:1
public partial class turf_closed_wall : turf_closed
{
    // tg: code/game/turfs/closed/walls.dm:283
    public override object get_dumping_location()
    {
        return null;
    }

    // tg: code/game/turfs/closed/walls.dm:286
    public override async DmTask acid_melt()
    {
        await dismantle_wall(1);
    }

    // tg: code/game/turfs/closed/walls.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(can_engrave))
            ADD_TRAIT(this, TRAIT_NOT_ENGRAVABLE, INNATE_TRAIT);
        if (Dm.truthy(is_station_level(z)))
            Dm.append(ref GLOB.station_turfs, this);
        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_DIAGONAL_CORNERS)) && Dm.truthy(fixed_underlay)) //Set underlays for the diagonal walls.
        {
            mutable_appearance underlay_appearance = mutable_appearance(layer: LOW_FLOOR_LAYER, offset_spokesman: this, plane: FLOOR_PLANE) as mutable_appearance;
            if (Dm.truthy(fixed_underlay["space"]))
                generate_space_underlay(underlay_appearance, this);
            else
            {
                underlay_appearance.icon = fixed_underlay["icon"];
                underlay_appearance.icon_state = (string)fixed_underlay["icon_state"];
            }
            fixed_underlay = string_assoc_list(fixed_underlay) as DmList;
            Dm.append(underlays, underlay_appearance);
        }
        // register_context() (datums/screentips/atom_context.dm:5): the screentip context of the held tool, M9 (examine and the
        // context tips, as the wall's add_context row); nothing in M3 reads it.
        Dm.Deferred("code/game/turfs/closed/walls.dm:62", "Initialize: register_context(): screentip contexts (M10+)");
        return dot;
    }

    // tg: code/game/turfs/closed/walls.dm:81
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(is_station_level(z)))
            Dm.remove(ref GLOB.station_turfs, this);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/closed/walls.dm:86
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(Dm.initial(this, (atom p) => p.smoothing_flags), SMOOTH_DIAGONAL_CORNERS)))
            Dm.append(ref dot, span_notice(I18n.T("You could adjust its corners with a <b>wrench</b>.")));
        Dm.append(ref dot, deconstruction_hints(user));
        return dot;
    }

    // tg: code/game/turfs/closed/walls.dm:92
    public virtual object deconstruction_hints(mob user = null)
    {
        return span_notice(I18n.T("The outer plating is <b>welded</b> firmly in place."));
    }

    // tg: code/game/turfs/closed/walls.dm:77
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await base.atom_destruction(damage_flag);
        await dismantle_wall(TRUE, FALSE);
    }

    // tg: code/game/turfs/closed/walls.dm:98
    public virtual async DmTask dismantle_wall(float? devastated = null, float? explode = null)
    {
        devastated ??= FALSE;
        explode ??= FALSE;
        if (Dm.truthy(devastated))
            await devastate_wall();
        else
        {
            playsound(this, break_sound, 100, TRUE);
            object newgirder = await break_wall();
            if (Dm.truthy(newgirder)) //maybe we don't /want/ a girder!
                // tg: code/game/turfs/closed/walls.dm:105
                transfer_fingerprints_to(newgirder as atom);
        }

        object new_turf;
        if (Dm.truthy(decon_type))
            new_turf = await ChangeTurf(decon_type, flags: CHANGETURF_INHERIT_AIR);
        else
            new_turf = await ScrapeAway();
        // TURF-IDENTITY: tg's src is the turf the change left
        QUEUE_SMOOTH_NEIGHBORS(new_turf ?? this);
    }

    // tg: code/game/turfs/closed/walls.dm:113
    public virtual async DmTask<object> break_wall()
    {
        Dm.@new(sheet_type, this, sheet_amount);
        if (Dm.truthy(girder_type))
            return Dm.@new(girder_type, this);
        return null;
    }

    // tg: code/game/turfs/closed/walls.dm:118
    public virtual async DmTask devastate_wall()
    {
        Dm.@new(sheet_type, this, sheet_amount);
        if (Dm.truthy(girder_type))
            Dm.@new<obj_item_stack_sheet_iron>(this);
    }

    // tg: code/game/turfs/closed/walls.dm:123  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of waiting-model.md
    // 5.4; dismantle_wall and ScrapeAway may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, null), null); // `.` is never assigned: no box (5.4)
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        CeilingSystem.Of(World.Current).ExAct((int)x, (int)y, (int)z, Dm.num(severity), target); // D295: the floor's odds on the ceiling
        // TURF-IDENTITY: the cell, to find the turf that dismantle_wall leaves (tg's src after the change)
        float cell_x = x, cell_y = y, cell_z = z;
        if (target == this)
        {
            await dismantle_wall(1, 1);
            return TRUE;
        }

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
            {
                //SN src = null
                turf NT = await ScrapeAway() as turf;
                await NT.contents_explosion(severity, target);
                return TRUE;
            }
            case EXPLODE_HEAVY:
                await dismantle_wall(prob(50) ? TRUE : FALSE, TRUE);
                break;
            case EXPLODE_LIGHT:
                if (prob(hardness))
                    await dismantle_wall(0, 1);
                break;
        }

        // TURF-IDENTITY: tg's src is the turf dismantle_wall left (the plating after a wall); ..() runs /turf/ex_act there,
        // whose only own effect is a fish source's reward (none on a plating, nor on this wall: ChangeTurf carries no
        // fish_source) and /atom/ex_act's TRUE, which the wall's own parent call gives as well.
        turf now = locate(cell_x, cell_y, cell_z) ?? this;
        if (!Dm.truthy(now.density))
            return base.ex_act(severity, target, __extra);

        return TRUE;
    }

    // tg: code/game/turfs/closed/walls.dm:152
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        user.changeNext_move(CLICK_CD_MELEE);
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/turfs/closed/walls.dm:187
    public virtual async DmTask hulk_recoil(obj_item_bodypart arm = null, mob_living_carbon_human hulkman = null, float? damage = null)
    {
        Dm.NotTranslated("code/game/turfs/closed/walls.dm:187", "hulk_recoil: hulk attacks (M10+)");
    }

    // tg: code/game/turfs/closed/walls.dm:193
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);
        to_chat(user, span_notice(I18n.T("You push the wall but nothing happens!")));
        playsound(this, "sound/items/weapons/genhit.ogg", 25, TRUE);
        // tg: code/game/turfs/closed/walls.dm:200
        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/turfs/closed/walls.dm:202
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return ITEM_INTERACT_BLOCKING;
        }

        // tg: code/game/turfs/closed/walls.dm:207
        add_fingerprint(user);

        //the istype cascade has been spread among various procs for easy overriding
        if (Dm.truthy(await try_clean(tool, user)) || Dm.truthy(await try_decon(tool, user)) || Dm.truthy(await try_build_support(tool, user)))
            return ITEM_INTERACT_SUCCESS;

        return NONE;
    }

    // tg: code/game/turfs/closed/walls.dm:215
    public virtual async DmTask<object> try_clean(obj_item W = null, mob_living user = null)
    {
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(LAZYLEN(dent_decals)))
            return FALSE;

        if (Dm.eq(W.tool_behaviour, TOOL_WELDER))
        {
            if (!Dm.truthy(W.tool_start_check(user, amount: 1)))
                return FALSE;

            to_chat(user, span_notice(I18n.T("You begin fixing dents on the wall...")));
            if (Dm.truthy(await W.use_tool(this, user, 0, volume: 100)))
            {
                // TURF-IDENTITY: a replaced src is not this wall (its dents went with it)
                if (!QDELETED(this) && iswallturf(this) && Dm.truthy(LAZYLEN(dent_decals)))
                {
                    to_chat(user, span_notice(I18n.T("You fix some dents on the wall.")));
                    cut_overlay(dent_decals);
                    dent_decals.Cut();
                }
                return TRUE;
            }
        }

        return FALSE;
    }

    // tg: code/game/turfs/closed/walls.dm:233
    public virtual async DmTask<object> try_decon(obj_item I = null, mob user = null, params object[] __extra)
    {
        if (Dm.eq(I.tool_behaviour, TOOL_WELDER))
        {
            if (!Dm.truthy(I.tool_start_check(user as mob_living, amount: Dm.round(slicing_duration / 50), heat_required: HIGH_TEMPERATURE_REQUIRED)))
                return FALSE;

            to_chat(user, span_notice(I18n.T("You begin slicing through the outer plating...")));
            if (Dm.truthy(await I.use_tool(this, user as mob_living, slicing_duration, volume: 100)))
            {
                // TURF-IDENTITY: tg's iswallturf(src) asks whether the turf here is still a wall; a replaced src is no longer
                // this wall (the port's old turf is deleted and off the map)
                if (!QDELETED(this) && iswallturf(this))
                {
                    to_chat(user, span_notice(I18n.T("You remove the outer plating.")));
                    await dismantle_wall();
                }
                return TRUE;
            }
        }

        return FALSE;
    }

    ///Helper for building wall_support on top of desired wall
    // tg: code/game/turfs/closed/walls.dm:248
    public virtual async DmTask<object> try_build_support(obj_item I = null, mob user = null)
    {
        Type rods_type = Dm.initial(typeof(obj_structure_wall_support), (obj_structure_wall_support p) => p.rods_type);
        if (!isstack(I) || !istype(I, rods_type))
            return FALSE;
        obj_item_stack rods = I as obj_item_stack;
        float amount_needed = Dm.initial(typeof(obj_structure_wall_support), (obj_structure_wall_support p) => p.rods_amount);
        if (Dm.num(rods.get_amount()) < Dm.num(amount_needed))
        {
            to_chat(user, span_warning(I18n.T("You need at least [] rods for that!", amount_needed)));
            return FALSE;
        }
        to_chat(user, span_notice(I18n.T("You start constructing wall support...")));
        if (Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
        {
            obj_structure_wall_support WS = Dm.@new<obj_structure_wall_support>(this);
            await rods.use(Dm.num(amount_needed));
            // tg: code/game/turfs/closed/walls.dm:262
            rods.transfer_fingerprints_to(WS);
            to_chat(user, span_notice(I18n.T("You place [] on [].", WS, this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/closed/walls.dm:265  (M5x slice 5)
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        await wall_singularity_pull(current_size);
        return null;
    }

    // tg: code/game/turfs/closed/walls.dm:269  (M5x slice 5)
    public virtual async DmTask wall_singularity_pull(object current_size = null)
    {
        if (Dm.num(current_size) >= STAGE_FIVE)
        {
            if (prob(50))
                await dismantle_wall();
            return;
        }
        if (Dm.num(current_size) == STAGE_FOUR)
        {
            if (prob(30))
                await dismantle_wall();
        }
    }

    // tg: code/game/turfs/closed/walls.dm:309
    // DEVIATION(DV214): face, the wall's side the dent is on (the decal's dir; tg's top-down view has one picture for all its sides)
    public virtual void add_dent(object denttype = null, object x = null, object y = null, object face = null)
    {
        x ??= rand(-8, 8);
        y ??= rand(-8, 8);
        if (Dm.num(LAZYLEN(dent_decals)) >= MAX_DENT_DECALS)
            return;

        mutable_appearance decal = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "", BULLET_HOLE_LAYER) as mutable_appearance;
        if (Dm.eq(denttype, WALL_DENT_SHOT))
            decal.icon_state = "bullet_hole";
        else if (Dm.eq(denttype, WALL_DENT_HIT))
            decal.icon_state = Dm.text("impact[]", rand(1, 3));

        decal.pixel_w = Dm.num(x);
        decal.pixel_z = Dm.num(y);
        if (face != null)
            decal.dir = Dm.num(face);

        if (Dm.truthy(LAZYLEN(dent_decals)))
        {
            cut_overlay(dent_decals);
            Dm.append(ref dent_decals, decal);
        }
        else
            dent_decals = Dm.list(decal);

        add_overlay(dent_decals);
    }

    // tg: code/game/turfs/closed/walls.dm:341
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = null;
        dot = base.Bumped(bumped_atom);
        if (Dm.truthy(bumped_atom._listen_lookup?[COMSIG_LIVING_WALL_BUMP]))
            bumped_atom._SendSignal(COMSIG_LIVING_WALL_BUMP, Dm.list(bumped_atom, this));
        return dot;
    }

    // tg: code/game/turfs/closed/walls.dm:345
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (Dm.truthy(gone._listen_lookup?[COMSIG_LIVING_WALL_EXITED]))
            gone._SendSignal(COMSIG_LIVING_WALL_EXITED, Dm.list(gone, this));
        return dot;
    }

    // tg: code/game/turfs/closed/walls.dm:349
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(Dm.bitand(Dm.initial(this, (atom p) => p.smoothing_flags), SMOOTH_DIAGONAL_CORNERS)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_DIAGONAL_CORNERS)))
            smoothing_flags = Dm.num(Dm.bitand(smoothing_flags, Dm.bitnot(SMOOTH_DIAGONAL_CORNERS)));
        else
            smoothing_flags = Dm.num(Dm.bitor(smoothing_flags, SMOOTH_DIAGONAL_CORNERS));
        QUEUE_SMOOTH(this);
        to_chat(user, span_notice(I18n.T("You adjust [].", this)));
        tool.play_tool_sound(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/turfs/closed/walls.dm:289 (E13: the RCD)
public partial class turf_closed_wall : turf_closed
{
    // tg: code/game/turfs/closed/walls.dm:289
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 4 * SECONDS), Dm.kv("cost", 26));
        else if (Dm.eq(sw__1, RCD_WALLFRAME))
            return Dm.list(Dm.kv("delay", 1 * SECONDS), Dm.kv("cost", 8));
        return FALSE;
    }

    // tg: code/game/turfs/closed/walls.dm:297
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_WALLFRAME))
        {
            Type wallmount = rcd_data[RCD_DESIGN_PATH] as Type;
            obj_item_wallframe new_wallmount = await Dm.new_async(wallmount, user.drop_location()) as obj_item_wallframe;
            if (Dm.eq(await new_wallmount.interact_with_atom(this, user as mob_living), ITEM_INTERACT_SUCCESS))
                return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            await ScrapeAway();
            return TRUE;
        }
        return FALSE;
    }
}
