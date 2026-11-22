// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/_box_magazine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the ammo box and the magazine base, whole but for
// the ammo band's colour picker (the writing implements and tgui pickers are M9; docs/ai/specs/not-translated.md section 48). stored_ammo
// holds casings, or typepaths for casings not made yet (lazy loading); a revolver cylinder also holds nulls for its empty chambers.
// The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:2
public partial class obj_item_ammo_box : obj_item
{
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(start_empty))
        {
            top_off(starting: TRUE);
        }
        else if (custom_materials != null && !Dm.truthy(Dm.bitand(item_flags, ABSTRACT))) //internal magazines are abstract
        {
            obj_item_ammo_casing prototype = Dm.@new(ammo_type) as obj_item_ammo_casing;
            DmList new_materials = custom_materials.Copy();
            foreach (object mat in Dm.each(prototype.custom_materials))
                new_materials[mat] = Dm.num(new_materials[mat]) - Dm.num(prototype.custom_materials[mat]) * max_ammo;
            qdel(prototype);
            Waiting.NoSleep(set_custom_materials(new_materials));
        }
        Waiting.NoSleep(update_icon_state());
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:60
    public override object Destroy(float? force = null)
    {
        foreach (object casing in Dm.each(stored_ammo))
        {
            if (!ispath(casing))
                qdel(casing);
        }
        stored_ammo = null;
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:67
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (stored_ammo != null && stored_ammo.Find(gone) != 0)
            remove_from_stored_ammo(gone);
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:72
    public virtual void remove_from_stored_ammo(atom_movable gone = null)
    {
        stored_ammo.Remove(gone);
        if (gone.custom_materials != null && custom_materials != null && !Dm.truthy(Dm.bitand(item_flags, ABSTRACT)))
        {
            DmList new_materials = custom_materials.Copy();
            foreach (object mat in Dm.each(gone.custom_materials))
                new_materials[mat] = Dm.num(new_materials[mat]) - Dm.num(gone.custom_materials[mat]);
            Waiting.NoSleep(set_custom_materials(new_materials));
        }
        update_appearance();
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:81
    public override async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description), Dm.named("attached_proc", nameof(add_notes_box)));
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:84
    public virtual object add_notes_box()
    {
        DmList readout = Dm.list();
        if (caliber != null && Dm.truthy(max_ammo)) // Text references a 'magazine' as only magazines generally have the caliber variable initialized
        {
            Dm.append(ref readout, I18n.T("Up to [] can be found within this magazine. \nAccidentally discharging any of these projectiles may void your insurance contract.", span_warning(Dm.text("[] [] []s", max_ammo, caliber, casing_phrasing))));
        }
        obj_item_ammo_casing mag_ammo = get_and_shuffle_round();
        if (mag_ammo != null)
            Dm.append(ref readout, Dm.text("\n[]", mag_ammo.add_notes_ammo()));
        return readout.Join("\n");
    }

    ///list of every bullet in the box
    ///forces all bullets to lazyload
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:100
    public virtual DmList ammo_list()
    {
        for (int i = 1; i <= stored_ammo.Count; i++)
        {
            if (ispath(stored_ammo[(float)i]))
            {
                Type casing_type = stored_ammo[(float)i] as Type;
                stored_ammo[(float)i] = Dm.@new(casing_type, this);
            }
        }
        return stored_ammo.Copy();
    }

    /**
     * top_off is used to refill the magazine to max, in case you want to increase the size of a magazine with VV then refill it at once
     *
     * Arguments:
     * * load_type - if you want to specify a specific ammo casing type to load, enter the path here, otherwise it'll use the basic [/obj/item/ammo_box/var/ammo_type]. Must be a compatible round
     * * starting - Relevant for revolver cylinders, if FALSE then we mind the nulls that represent the empty cylinders (since those nulls don't exist yet if we haven't initialized when this is TRUE)
     */
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:114
    public virtual void top_off(Type load_type = null, float? starting = null)
    {
        starting ??= FALSE;
        if (load_type == null) //this check comes first so not defining an argument means we just go with default ammo
            load_type = ammo_type;
        Type round_check = load_type;
        if (!Dm.truthy(starting) && !(caliber != null ? Dm.eq(caliber, Dm.initial(round_check, (obj_item_ammo_casing p) => p.caliber)) : ammo_type == load_type))
        {
            stack_trace(Dm.text("Tried loading unsupported ammocasing type [] into ammo box [].", load_type, GetType()), "code/modules/projectiles/boxes_magazines/_box_magazine.dm", 123);
            return;
        }
        DmList new_materials = null;
        if (!Dm.truthy(Dm.bitand(item_flags, ABSTRACT)))
            new_materials = custom_materials?.Copy() ?? Dm.list();
        for (float i = max(1, stored_ammo.Count + 1); i <= max_ammo; i++)
        {
            object casing = round_check;
            if (!Dm.truthy(starting))
            {
                obj_item_ammo_casing made = Dm.@new(round_check, this) as obj_item_ammo_casing;
                casing = made;
                if (new_materials != null)
                {
                    foreach (object mat in Dm.each(made.custom_materials))
                        new_materials[mat] = Dm.num(new_materials[mat]) + Dm.num(made.custom_materials[mat]);
                }
            }
            stored_ammo.Add(casing);
        }
        if (!Dm.truthy(starting) && new_materials != null && new_materials.Count > 0)
            Waiting.NoSleep(set_custom_materials(new_materials));
        update_appearance();
    }

    ///gets a round from the magazine
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:141
    public virtual obj_item_ammo_casing get_round()
    {
        int ammo_len = stored_ammo.Count;
        if (ammo_len == 0)
            return null;
        object casing = stored_ammo[(float)ammo_len];
        if (ispath(casing))
        {
            casing = Dm.@new(casing as Type, this);
            stored_ammo[(float)ammo_len] = casing;
        }
        return casing as obj_item_ammo_casing;
    }

    /// Gets a round from the magazine and puts it back at the bottom of the ammo list
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:152
    public virtual obj_item_ammo_casing get_and_shuffle_round()
    {
        obj_item_ammo_casing casing = get_round();
        if (casing == null)
            return null;
        stored_ammo.Remove(casing);
        stored_ammo.Insert(1, casing);
        return casing;
    }

    ///puts a round into the magazine
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:161
    public virtual object give_round(obj_item_ammo_casing new_round = null, float? replace_spent = null)
    {
        replace_spent ??= 0;
        // Boxes don't have a caliber type, magazines do. Not sure if it's intended or not, but if we fail to find a caliber, then we fall back to ammo_type.
        if (!Dm.truthy(is_compatible_round(new_round)))
            return FALSE;

        if (stored_ammo.Count < max_ammo)
        {
            stored_ammo.Add(new_round);
            Waiting.NoSleep(new_round.forceMove(this));
            if (new_round.custom_materials != null && !Dm.truthy(Dm.bitand(item_flags, ABSTRACT)))
            {
                DmList new_materials = custom_materials?.Copy() ?? Dm.list();
                foreach (object mat in Dm.each(new_round.custom_materials))
                    new_materials[mat] = Dm.num(new_materials[mat]) + Dm.num(new_round.custom_materials[mat]);
                Waiting.NoSleep(set_custom_materials(new_materials));
            }
            return TRUE;
        }

        if (!Dm.truthy(replace_spent))
            return FALSE;

        //for accessibles magazines (e.g internal ones) when full, start replacing spent ammo
        foreach (object entry in Dm.each(stored_ammo))
        {
            if (ispath(entry) || entry is not obj_item_ammo_casing casing || casing.loaded_projectile != null)
                continue;
            //found a spent ammo
            stored_ammo.Remove(casing);
            Waiting.NoSleep(casing.forceMove(get_turf(this)));
            stored_ammo.Add(new_round);
            Waiting.NoSleep(new_round.forceMove(this));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:192
    public virtual object is_compatible_round(obj_item_ammo_casing new_round = null)
    {
        if (new_round == null || !(caliber != null ? Dm.eq(caliber, new_round.caliber) : ammo_type == new_round.GetType()))
            return FALSE;
        return TRUE;
    }

    ///Whether or not the box can be loaded, used in overrides
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:198
    public virtual object can_load(mob user = null)
    {
        return TRUE;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:201
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (IS_WRITING_UTENSIL(tool)) // gaps S3: a pen sets the ammo band's colour
        {
            if (!Dm.truthy(ammo_band_icon))
            {
                balloon_alert(user, I18n.T("no indicator support!"));
                return null;
            }
            object new_color = await tgui_color_picker(user, I18n.T("Set a new ammo band color, cancel to remove indicator"), I18n.T("Ammo Box Indicator Color"), ammo_band_color);
            ammo_band_color = new_color as string;
            balloon_alert(user, I18n.T("indicator updated"));
            update_appearance();
            return null;
        }

        if (Dm.truthy(await try_load(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:215
    public virtual async DmTask<object> try_load(mob_living user = null, obj_item tool = null, float? silent = null, float? replace_spent = null)
    {
        silent ??= FALSE;
        replace_spent ??= FALSE;
        float num_loaded = 0;
        if (!Dm.truthy(can_load(user)))
            return null;

        if (istype(tool, typeof(obj_item_ammo_box)))
        {
            obj_item_ammo_box other_box = tool as obj_item_ammo_box;
            foreach (object entry in Dm.each(other_box.ammo_list()))
            {
                if (entry is not obj_item_ammo_casing casing)
                    continue;
                bool did_load = Dm.truthy(give_round(casing, replace_spent));
                if (did_load)
                {
                    other_box.stored_ammo.Remove(casing);
                    num_loaded++;
                }
                // failed to load (full already? ran out of ammo?)
                if (!did_load)
                    break;
                // this box can't accept being multiloaded into
                if (!Dm.truthy(Dm.bitand(ammo_box_multiload, AMMO_BOX_MULTILOAD_IN)))
                    break;
                // the other box can't give multiple bullets in one go to an unloaded magazine
                if (!isgun(loc) && !Dm.truthy(Dm.bitand(other_box.ammo_box_multiload, AMMO_BOX_MULTILOAD_OUT)))
                    break;
                // the other box can't give multiple bullets in one go to a loaded magazine
                if (isgun(loc) && !Dm.truthy(Dm.bitand(other_box.ammo_box_multiload, AMMO_BOX_MULTILOAD_OUT_LOADED)))
                    break;
            }
            if (num_loaded != 0)
                other_box.update_appearance();
        }

        if (istype(tool, typeof(obj_item_ammo_casing)))
        {
            obj_item_ammo_casing casing = tool as obj_item_ammo_casing;
            if (Dm.truthy(give_round(casing, replace_spent)))
            {
                await user.transferItemToLoc(casing, this, TRUE);
                num_loaded++;
                casing.update_appearance();
            }
        }

        if (num_loaded != 0)
        {
            if (!Dm.truthy(silent))
            {
                to_chat(user, span_notice(I18n.T("You load [] into \\the []!", num_loaded > 1 ? Dm.text("[] []s", num_loaded, casing_phrasing) : Dm.text("a []", casing_phrasing), this)));
                playsound(this, Dm.resource("sound/items/weapons/gun/general/mag_bullet_insert.ogg"), 60, TRUE);
            }
            update_appearance();
        }
        return num_loaded;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:258
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        obj_item_ammo_casing A = get_round();
        if (A == null)
            return null;

        await A.forceMove(drop_location() as atom);
        if (!Dm.truthy(user.is_holding(this)) || !Dm.truthy(await user.put_in_hands(A))) //incase they're using TK
            A.bounce_away(FALSE, NONE);

        playsound(this, Dm.resource("sound/items/weapons/gun/general/mag_bullet_insert.ogg"), 60, TRUE);
        to_chat(user, span_notice(I18n.T("You remove a [] from []!", casing_phrasing, this)));
        update_appearance();
        return null;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:270
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        int shells_left = stored_ammo?.Count ?? 0;
        obj_item_ammo_casing top_round = get_round();
        if (top_round == null)
            return dot;

        Dm.append(ref dot, Dm.text("It has <b>[]</b> []\\s remaining.", shells_left, casing_phrasing));
        // this is kind of awkward phrasing, but it's the top/ready ammo in the box
        // intended for people who have like three mislabeled magazines
        Dm.append(ref dot, span_notice(Dm.text("\\A <b>[]</b> is ready.", top_round)));
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:281
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        int shells_left = stored_ammo?.Count ?? 0;
        switch ((int)multiple_sprites)
        {
            case AMMO_BOX_PER_BULLET:
                icon_state = Dm.text("[]-[]", Dm.truthy(multiple_sprite_use_base) ? base_icon_state : Dm.initial(this, (obj_item p) => p.icon_state), shells_left);
                break;
            case AMMO_BOX_FULL_EMPTY:
                icon_state = Dm.text("[]-[]", Dm.truthy(multiple_sprite_use_base) ? base_icon_state : Dm.initial(this, (obj_item p) => p.icon_state), shells_left != 0 ? "full" : "empty");
                break;
        }
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:290
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(ammo_band_color) && Dm.truthy(ammo_band_icon))
            Dm.append(ref dot, update_ammo_band());
        return dot;
    }

    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:295
    public virtual object update_ammo_band()
    {
        object band_icon = ammo_band_icon;
        if ((stored_ammo?.Count ?? 0) == 0 && Dm.truthy(ammo_band_icon_empty))
            band_icon = ammo_band_icon_empty;
        image ammo_band_image = image(icon, this, band_icon) as image;
        ammo_band_image.color = ammo_band_color;
        ammo_band_image.appearance_flags = Dm.bitor(Dm.bitor(RESET_COLOR, KEEP_APART), 0);
        return ammo_band_image;
    }
}

// tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:304
public partial class obj_item_ammo_box_magazine : obj_item_ammo_box
{
    ///Count of number of bullets in the magazine
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:313
    public virtual float ammo_count(float? countempties = null)
    {
        countempties ??= TRUE;
        float boolets = 0;
        foreach (object entry in Dm.each(stored_ammo))
        {
            if (ispath(entry) || (entry is obj_item_ammo_casing bullet && (bullet.loaded_projectile != null || Dm.truthy(countempties))))
                boolets++;
        }
        return boolets;
    }

    ///drops the entire contents of the magazine on the floor
    // tg: code/modules/projectiles/boxes_magazines/_box_magazine.dm:321
    public virtual void empty_magazine()
    {
        turf turf_mag = get_turf(this);
        obj_item_ammo_casing casing = get_round();
        while (casing != null)
        {
            Waiting.NoSleep(casing.forceMove(turf_mag));
            casing = get_round();
        }
    }
}
