// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ingredients_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the custom food (sandwiches, burgers, custom pizzas, cakes and pies with what is put on them).
// The filling's colour, read from the ingredient's sprite (icon.GetPixel), waits: the server reads no tg icon (D104). The var block is
// generated (D111).

/**
 * # Custom Atom Component
 *
 * When added to an atom, item ingredients can be put into that.
 * The sprite is updated and reagents and custom materials are transferred.
 *
 * If the component is added to something that is processed, creating new objects (being cut, for example),
 * the replacement type needs to also have the component. The ingredients will be copied over. Reagents are not
 * copied over since other components already take care of that.
 */
// tg: code/datums/components/ingredients_holder.dm:11
public partial class datum_component_ingredients_holder : datum_component
{
    // tg: code/datums/components/ingredients_holder.dm:33
    [DmParams("replacement", "fill_type", "ingredient_type", "max_ingredients", "processed_holder", "screentip_verb")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object replacement = Dm.arg(__extra, 0);
        object fill_type = Dm.arg(__extra, 1);
        object ingredient_type = Dm.arg(__extra, 2) ?? CUSTOM_INGREDIENT_TYPE_EDIBLE;
        object max_ingredients = Dm.arg(__extra, 3) ?? MAX_ATOM_OVERLAYS - 3; // The cap is >= MAX_ATOM_OVERLAYS so we reserve 2 for top /bottom of item + 1 to stay under cap
        datum_component_ingredients_holder processed_holder = Dm.arg(__extra, 4) as datum_component_ingredients_holder; //when processing a holder, the results receive their own comps, but need the ingredient names and filling passed down
        object screentip_verb = Dm.arg(__extra, 5) ?? "Add";
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        atom atom_parent = (atom)parent;
        // assume replacement is OK
        if (atom_parent.reagents == null && !Dm.truthy(replacement))
            return COMPONENT_INCOMPATIBLE;

        this.replacement = replacement;
        this.fill_type = fill_type;
        this.max_ingredients = max_ingredients;
        this.ingredient_type = ingredient_type;
        this.screentip_verb = screentip_verb;

        if (processed_holder == null || !Dm.truthy(Dm.length(processed_holder.ingredient_names)))
            return null;

        ingredient_names = processed_holder.ingredient_names;
        custom_name = processed_holder.custom_name;
        atom_parent.name = I18n.T("[] [] []", custom_adjective(), custom_name, atom_parent.name);
        foreach (object fillcol in Dm.each(processed_holder.filling_colors))
            apply_fill(fillcol);
        return null;
    }

    // tg: code/datums/components/ingredients_holder.dm:64
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref top_overlay);
        return base.Destroy(force);
    }

    // tg: code/datums/components/ingredients_holder.dm:68
    public override async DmTask RegisterWithParent()
    {
        atom atom_parent = (atom)parent;
        Dm.combine(ref atom_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(customizable_attack));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(food_exited));
        RegisterSignal(parent, COMSIG_ATOM_PROCESSED, nameof(on_processed));
        RegisterSignal(parent, COMSIG_PIZZA_SLICE_TAKEN, nameof(on_slice_taken));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
        ADD_TRAIT(parent, TRAIT_INGREDIENTS_HOLDER, INNATE_TRAIT);
    }

    // tg: code/datums/components/ingredients_holder.dm:79
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_EXITED,
            COMSIG_ATOM_PROCESSED,
            COMSIG_PIZZA_SLICE_TAKEN,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM
        ));
        REMOVE_TRAIT(parent, TRAIT_INGREDIENTS_HOLDER, INNATE_TRAIT);
    }

    // tg: code/datums/components/ingredients_holder.dm:90
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        if (!isatom(new_parent))
            return COMPONENT_INCOMPATIBLE;
        atom atom_parent = (atom)new_parent;
        if (atom_parent.reagents == null)
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    ///Handles when the customizable food is examined.
    // tg: code/datums/components/ingredients_holder.dm:98
    [SignalHandler]
    public virtual void on_examine(atom A = null, mob user = null, DmList examine_list = null)
    {
        atom atom_parent = (atom)parent;

        Dm.append(ref examine_list, I18n.T("It [].", Dm.truthy(LAZYLEN(ingredient_names))
            ? I18n.T("contains [] making a []-sized []", english_list(ingredient_names), custom_adjective(), Dm.initial(atom_parent, (atom a) => a.name))
            : I18n.T("does not contain any ingredients")));
    }

    //// Proc that checks if an ingredient is valid or not, returning false if it isnt and true if it is.
    // tg: code/datums/components/ingredients_holder.dm:108
    public virtual object valid_ingredient(obj ingredient = null)
    {
        if (HAS_TRAIT(ingredient, TRAIT_INGREDIENTS_HOLDER))
            return FALSE;
        if (HAS_TRAIT(ingredient, TRAIT_ODD_CUSTOMIZABLE_FOOD_INGREDIENT))
            return TRUE;
        object sw__1 = ingredient_type;
        if (Dm.eq(sw__1, CUSTOM_INGREDIENT_TYPE_EDIBLE))
            return Dm.num(IS_EDIBLE(ingredient));
        else if (Dm.eq(sw__1, CUSTOM_INGREDIENT_TYPE_DRYABLE))
            return Dm.num(HAS_TRAIT(ingredient, TRAIT_DRYABLE));
        return TRUE;
    }

    ///Handles when the customizable food is attacked by something.
    // tg: code/datums/components/ingredients_holder.dm:121
    [SignalHandler]
    public virtual object customizable_attack(datum source = null, obj ingredient = null, mob attacker = null, float? silent = null, float? force = null)
    {
        silent ??= FALSE;
        force ??= FALSE;

        if (!Dm.truthy(valid_ingredient(ingredient)))
        {
            if (Dm.truthy(ingredient.is_drainable())) // For stuff like adding flour from a flour sack into a bowl, we handle the transfer of the reagent elsewhere, but we shouldn't regard it beyond some user feedback.
            {
                attacker.balloon_alert(attacker, I18n.T("transferring..."));
                return null;
            }
            attacker.balloon_alert(attacker, I18n.T("doesn't go on that!"));
            return null;
        }

        if (LAZYLEN(ingredient_names) >= Dm.num(max_ingredients))
        {
            attacker.balloon_alert(attacker, I18n.T("too full!"));
            return COMPONENT_NO_AFTERATTACK;
        }

        if (!Dm.truthy(Waiting.NoSleep(attacker.transferItemToLoc(ingredient as obj_item, parent))))
            return null;
        Waiting.NoSleep(add_ingredient(ingredient as obj_item));
        return null;
    }

    ///Extract the filling color from the ingredient, than calls apply_fill()
    // tg: code/datums/components/ingredients_holder.dm:141
    public virtual void get_fill(obj_item ingredient = null)
    {
        // get average color: tg scales the ingredient's sprite to one pixel and reads it (icon.Scale, icon.GetPixel, :143-147), then
        // LAZYADD(filling_colors, fillcol) and apply_fill(fillcol). The server reads no tg icon (D104), so the filling waits.
        Dm.Deferred("code/datums/components/ingredients_holder.dm:143", "get_fill: the filling's colour is the ingredient sprite's average colour (icon.GetPixel of a tg icon: the server reads no tg icon, D104)");
    }

    ///Add a filling overlay to the parent atom.
    // tg: code/datums/components/ingredients_holder.dm:152
    public virtual void apply_fill(object fill_color = null)
    {
        if (Dm.eq(fill_type, CUSTOM_INGREDIENT_ICON_NOCHANGE))
            //don't bother doing the icon procs
            return;
        atom atom_parent = (atom)parent;
        mutable_appearance filling = mutable_appearance(atom_parent.icon, Dm.text("[]_filling", Dm.initial(atom_parent, (atom a) => a.icon_state))) as mutable_appearance;
        filling.color = fill_color;
        object sw__1 = fill_type;
        if (Dm.eq(sw__1, CUSTOM_INGREDIENT_ICON_SCATTER))
        {
            filling.pixel_w = rand(-1, 1);
            filling.pixel_z = rand(-1, 1);
        }
        else if (Dm.eq(sw__1, CUSTOM_INGREDIENT_ICON_STACK))
        {
            filling.pixel_w = rand(-1, 1);
            // we're gonna abuse position layering to ensure overlays render right
            filling.pixel_y = -LAZYLEN(ingredient_names);
            filling.pixel_z = 3 * LAZYLEN(ingredient_names) - 1;
        }
        else if (Dm.eq(sw__1, CUSTOM_INGREDIENT_ICON_STACKPLUSTOP))
        {
            filling.pixel_w = rand(-1, 1);
            // similar here
            filling.pixel_y = -LAZYLEN(ingredient_names);
            filling.pixel_z = 3 * LAZYLEN(ingredient_names) - 1;
            if (top_overlay != null) // delete old top if exists
                atom_parent.cut_overlay(top_overlay);
            top_overlay = mutable_appearance(atom_parent.icon, Dm.text("[]_top", atom_parent.icon_state)) as mutable_appearance;
            top_overlay.pixel_y = -LAZYLEN(ingredient_names) - 1;
            top_overlay.pixel_z = 3 * LAZYLEN(ingredient_names) + 4;
            atom_parent.add_overlay(filling);
            atom_parent.add_overlay(top_overlay);
            return;
        }
        else if (Dm.eq(sw__1, CUSTOM_INGREDIENT_ICON_FILL))
        {
            if (top_overlay != null)
            {
                filling.color = mix_color(filling.color);
                atom_parent.cut_overlay(top_overlay);
            }
            top_overlay = filling;
        }
        else if (Dm.eq(sw__1, CUSTOM_INGREDIENT_ICON_LINE))
        {
            filling.pixel_w = filling.pixel_z = rand(-8, 3);
        }
        atom_parent.add_overlay(filling);
    }

    ///Takes the reagents from an ingredient.
    // tg: code/datums/components/ingredients_holder.dm:192
    public virtual async DmTask handle_reagents(obj_item ingredient = null)
    {
        atom atom_parent = (atom)parent;
        if (atom_parent.reagents != null && ingredient.reagents != null)
        {
            ((datum_reagents)atom_parent.reagents).maximum_volume += ((datum_reagents)ingredient.reagents).maximum_volume; // If we don't do this custom food starts voiding reagents past a certain point.
            await ((datum_reagents)ingredient.reagents).trans_to(atom_parent, ((datum_reagents)ingredient.reagents).total_volume);
        }
        return;
    }

    ///Adds a new ingredient and updates the parent's name.
    // tg: code/datums/components/ingredients_holder.dm:201
    public virtual async DmTask add_ingredient(obj_item ingredient = null)
    {
        atom atom_parent = (atom)parent;

        if (Dm.truthy(replacement))
        {
            atom replacement_parent = Dm.@new((Type)replacement, atom_parent.drop_location()) as atom;
            await ingredient.forceMove(replacement_parent);
            replacement = null;
            await replacement_parent.TakeComponent(this);
            await handle_reagents(parent as obj_item);
            qdel(atom_parent);
            atom_parent = (atom)parent;
        }

        await handle_reagents(ingredient);

        LAZYADD(ref ingredient_names, Dm.text("\\a []", ingredient.name));
        if (isitem(atom_parent))
        {
            obj_item item_parent = (obj_item)atom_parent;
            if (ingredient.w_class > item_parent.w_class)
                item_parent.update_weight_class(ingredient.w_class);
        }
        if (!Dm.truthy(custom_name))
            set_custom_name(ingredient);
        atom_parent.name = I18n.T("[] [] []", custom_adjective(), custom_name, Dm.initial(atom_parent, (atom a) => a.name));
        if (Dm.truthy(atom_parent._listen_lookup?[COMSIG_ATOM_CUSTOMIZED]))
            atom_parent._SendSignal(COMSIG_ATOM_CUSTOMIZED, Dm.list(atom_parent, ingredient));
        if (Dm.truthy(ingredient._listen_lookup?[COMSIG_ITEM_USED_AS_INGREDIENT]))
            ingredient._SendSignal(COMSIG_ITEM_USED_AS_INGREDIENT, Dm.list(ingredient, atom_parent));

        get_fill(ingredient);
        await handle_materials(ingredient);

        if (ingredient.loc != atom_parent)
            await ingredient.forceMove(atom_parent);
    }

    ///Rebuilds the custom materials the holder is composed of based on the materials of each ingredient
    // tg: code/datums/components/ingredients_holder.dm:233
    public virtual async DmTask handle_materials(obj_item ingredient = null, float? remove = null)
    {
        remove ??= FALSE;
        if (!Dm.truthy(ingredient?.custom_materials)) // (food_exited passes whatever leaves; a non-item has no obj_item here)
            return;
        atom atom_parent = (atom)parent;
        DmList new_materials = (atom_parent.custom_materials?.Copy() is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list());
        foreach (object mat in Dm.each(ingredient.custom_materials))
            Dm.append_at(new_materials, mat, Dm.num(ingredient.custom_materials[mat]) * (Dm.truthy(remove) ? -1 : 1));
        await atom_parent.set_custom_materials(new_materials);
    }

    ///Gives an adjective to describe the size of the custom food.
    // tg: code/datums/components/ingredients_holder.dm:243
    public virtual object custom_adjective()
    {
        float sw__1 = LAZYLEN(ingredient_names);
        if (Dm.in_to(sw__1, 0, 2))
            return "small";
        else if (Dm.in_to(sw__1, 3, 5))
            return "standard";
        else if (Dm.in_to(sw__1, 6, 8))
            return "big";
        else if (Dm.in_to(sw__1, 8, 11))
            return "ridiculous";
        else if (Dm.in_to(sw__1, 12, INFINITY))
            return "monstrous";
        return null;
    }

    ///Gives the type of custom food (based on what the first ingredient was).
    // tg: code/datums/components/ingredients_holder.dm:258
    public virtual void set_custom_name(obj_item ingredient = null)
    {
        if (ingredient is obj_item_food_meat)
        {
            obj_item_food_meat meat = (obj_item_food_meat)ingredient;
            if (Dm.truthy(meat.subjectname))
            {
                custom_name = meat.subjectname;
                return;
            }
            if (Dm.truthy(meat.subjectjob))
            {
                custom_name = meat.subjectjob;
                return;
            }
        }
        custom_name = ingredient.name;
    }

    ///Returns the color of the input mixed with the top_overlay's color.
    // tg: code/datums/components/ingredients_holder.dm:270
    public virtual object mix_color(object color = null)
    {
        if (Dm.length(filling_colors) == 1 || top_overlay == null)
            return color;
        DmList rgbcolor = Dm.list(0, 0, 0, 0);
        DmList customcolor = GetColors(color) as DmList;
        DmList ingcolor = GetColors(top_overlay.color) as DmList;
        rgbcolor[1] = (Dm.num(customcolor[1]) + Dm.num(ingcolor[1])) / 2;
        rgbcolor[2] = (Dm.num(customcolor[2]) + Dm.num(ingcolor[2])) / 2;
        rgbcolor[3] = (Dm.num(customcolor[3]) + Dm.num(ingcolor[3])) / 2;
        rgbcolor[4] = (Dm.num(customcolor[4]) + Dm.num(ingcolor[4])) / 2;
        return rgb(Dm.num(rgbcolor[1]), Dm.num(rgbcolor[2]), Dm.num(rgbcolor[3]), Dm.num(rgbcolor[4]));
    }

    ///Copies over the parent's fillings and name of ingredients to the processing results (such as slices when the parent is cut).
    // tg: code/datums/components/ingredients_holder.dm:284
    [SignalHandler]
    public virtual void on_processed(datum source = null, mob_living user = null, obj_item ingredient = null, DmList results = null)
    {
        // Reagents are not transferred since that should be handled elsewhere
        // while custom materials are already transferred evenly between results by atom/proc/StartProcessingAtom()
        foreach (atom result in Dm.each_cast<atom>(results))
            Waiting.NoSleep(result.AddComponent(typeof(datum_component_ingredients_holder), null, fill_type, Dm.named("ingredient_type", ingredient_type), Dm.named("max_ingredients", max_ingredients), Dm.named("processed_holder", this)));
    }

    /// Pizzas have unique slicing interaction so we need to do this
    // tg: code/datums/components/ingredients_holder.dm:293
    [SignalHandler]
    public virtual void on_slice_taken(datum source = null, mob_living user = null, obj_item slice = null)
    {
        Waiting.NoSleep(slice.AddComponent(typeof(datum_component_ingredients_holder), null, fill_type, Dm.named("ingredient_type", ingredient_type), Dm.named("max_ingredients", max_ingredients), Dm.named("processed_holder", this)));
    }

    /**
     * Adds context sensitivy directly to the customizable reagent holder file for screentips
     * Arguments:
     * * source - refers to item that will display its screentip
     * * context - refers to, in this case, an item that can be customized with other reagents or ingrideints
     * * held_item - refers to the item in your hand, which is hopefully an ingredient
     * * user - refers to user who will see the screentip when the proper context and tool are there
     */
    // tg: code/datums/components/ingredients_holder.dm:305
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        // only accept valid ingredients
        if (held_item == null || !Dm.truthy(valid_ingredient(held_item)))
            return NONE;

        context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] []", screentip_verb, held_item);

        return CONTEXTUAL_SCREENTIP_SET;
    }

    /// Clear refs if our food "goes away" somehow
    // tg: code/datums/components/ingredients_holder.dm:317
    [SignalHandler]
    public virtual void food_exited(datum source = null, atom_movable gone = null)
    {
        LAZYREMOVE(ref ingredient_names, Dm.text("\\a []", gone.name));
        Waiting.NoSleep(handle_materials(gone as obj_item, remove: TRUE));
    }
}
