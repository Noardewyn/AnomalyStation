// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/reskinnable_atom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ### Atom skin singleton datum
 *
 * Simple datum which holds information about a skin that can be applied to an atom.
 */
// tg: code/datums/components/reskinnable_atom.dm:6
public partial class datum_atom_skin : datum
{
    // tg: code/datums/components/reskinnable_atom.dm:46
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (greyscale_item_path == null)
            return;

        // Populate the fields required for GAGS previews
        greyscale_config = Dm.initial(greyscale_item_path, (atom a) => a.greyscale_config) as Type;
        greyscale_colors = Dm.initial(greyscale_item_path, (atom a) => a.greyscale_colors);

        // The icon isn't 'new'. So we don't make an extra icon in map_icons.
        if (Dm.eq(Dm.initial(greyscale_item_path, (atom a) => a.post_init_icon_state), new_icon_state))
            new_icon_state = null;
    }

    /// Returns the correct preview icon state for this atom skin, whether it be a map_preview or a normal icon. This is assumed from our var population above.
    // tg: code/datums/components/reskinnable_atom.dm:60
    public virtual object get_preview_icon_state()
    {
        if (greyscale_item_path == null) // Not a GAGs icon
            return new_icon_state;
        if (new_icon_state == null) // The base GAGS item icon
            return Dm.text("[]", greyscale_item_path);

        return Dm.text("[]--[]", greyscale_item_path, new_icon_state); // A GAGs reskin of the item icon
    }

    /**
     * Applies all relevant skin changes to the given atom
     * Can be overridden to add additional behavior, such as registering signals or altering other vars.
     *
     * * apply_to: The atom to apply the skin to
     */
    // tg: code/datums/components/reskinnable_atom.dm:74
    [ShouldCallParent]
    public virtual void apply(atom apply_to = null, mob user = null)
    {
        bool reset = Dm.truthy(reset_missing);
        if (!HAS_TRAIT(apply_to, TRAIT_WAS_RENAMED))
        {
            // APPLY_VAR_OR_RESET_INITIAL (__DEFINES/reskin.dm:10), inline
            if (Dm.truthy(new_name)) apply_to.name = new_name as string; else if (reset) apply_to.name = Dm.initial(apply_to.type, (atom a) => a.name);
            if (Dm.truthy(new_desc)) apply_to.desc = new_desc as string; else if (reset) apply_to.desc = Dm.initial(apply_to.type, (atom a) => a.desc);
        }
        if (!Dm.truthy(apply_to.greyscale_config)) // Never want to be resetting a GAGS icon back to initial.
        {
            if (Dm.truthy(new_icon)) apply_to.icon = new_icon; else if (reset) apply_to.icon = Dm.initial(apply_to.type, (atom a) => a.icon);
        }
        // APPLY_VAR_OR_RESET_TO (__DEFINES/reskin.dm:14), inline
        if (Dm.truthy(new_icon_state))
            apply_to.icon_state = new_icon_state as string;
        else if (reset)
            apply_to.icon_state = (Dm.initial(apply_to.type, (atom a) => a.post_init_icon_state) ?? Dm.initial(apply_to.type, (atom a) => a.icon_state)) as string;
        if (Dm.truthy(change_base_icon_state))
        {
            if (Dm.truthy(new_icon_state)) apply_to.base_icon_state = new_icon_state; else if (reset) apply_to.base_icon_state = Dm.initial(apply_to.type, (atom a) => a.base_icon_state);
        }
        if (isitem(apply_to))
        {
            obj_item item_apply_to = apply_to as obj_item;
            if (item_apply_to.greyscale_config_worn == null)
            {
                if (Dm.truthy(new_worn_icon)) item_apply_to.worn_icon = new_worn_icon; else if (reset) item_apply_to.worn_icon = Dm.initial(item_apply_to.type, (obj_item i) => i.worn_icon);
            }
            if (item_apply_to.greyscale_config_inhand_left == null)
            {
                if (Dm.truthy(new_lefthand_file)) item_apply_to.lefthand_file = new_lefthand_file; else if (reset) item_apply_to.lefthand_file = Dm.initial(item_apply_to.type, (obj_item i) => i.lefthand_file);
                if (Dm.truthy(new_righthand_file)) item_apply_to.righthand_file = new_righthand_file; else if (reset) item_apply_to.righthand_file = Dm.initial(item_apply_to.type, (obj_item i) => i.righthand_file);
            }
            if (Dm.truthy(change_worn_icon_state))
            {
                if (Dm.truthy(new_icon_state)) item_apply_to.worn_icon_state = new_icon_state; else if (reset) item_apply_to.worn_icon_state = Dm.initial(item_apply_to.type, (obj_item i) => i.worn_icon_state);
            }
            if (Dm.truthy(change_inhand_icon_state) || Dm.truthy(new_inhand_icon_state))
            {
                object new_inhand = Dm.truthy(new_inhand_icon_state) ? new_inhand_icon_state : new_icon_state;
                if (Dm.truthy(new_inhand)) item_apply_to.inhand_icon_state = new_inhand; else if (reset) item_apply_to.inhand_icon_state = Dm.initial(item_apply_to.type, (obj_item i) => i.inhand_icon_state);
            }
        }
    }

    /**
     * Resets all changes this skin would have made to the given atom
     * Does not verify that the skin was actually applied to the atom beforehand.
     * Can be overridden to add additional behavior, such as unregistering signals or altering other vars.
     *
     * * clear_from: The atom to clear the skin from
     */
    // tg: code/datums/components/reskinnable_atom.dm:103
    [ShouldCallParent]
    public virtual void clear_skin(atom clear_from = null, mob user = null)
    {
        // RESET_INITIAL_IF_SET and RESET_TO_IF_SET (__DEFINES/reskin.dm:18, :22), inline
        if (Dm.truthy(new_name)) clear_from.name = Dm.initial(clear_from.type, (atom a) => a.name);
        if (Dm.truthy(new_desc)) clear_from.desc = Dm.initial(clear_from.type, (atom a) => a.desc);
        bool is_greyscale = Dm.truthy(clear_from.greyscale_config) || Dm.truthy(clear_from.greyscale_colors);
        if (!is_greyscale)
        {
            if (Dm.truthy(new_icon)) clear_from.icon = Dm.initial(clear_from.type, (atom a) => a.icon);
        }
        if (Dm.truthy(new_icon_state))
            clear_from.icon_state = (Dm.initial(clear_from.type, (atom a) => a.post_init_icon_state) ?? Dm.initial(clear_from.type, (atom a) => a.icon_state)) as string;
        if (Dm.truthy(change_base_icon_state))
        {
            if (Dm.truthy(new_icon_state)) clear_from.base_icon_state = Dm.initial(clear_from.type, (atom a) => a.base_icon_state);
        }
        if (isitem(clear_from))
        {
            obj_item item_clear_from = clear_from as obj_item;
            if (Dm.truthy(new_worn_icon)) item_clear_from.worn_icon = Dm.initial(item_clear_from.type, (obj_item i) => i.worn_icon);
            if (Dm.truthy(Dm.truthy(change_inhand_icon_state) ? new_inhand_icon_state : new_icon_state)) item_clear_from.inhand_icon_state = Dm.initial(item_clear_from.type, (obj_item i) => i.inhand_icon_state);
            if (!is_greyscale)
            {
                if (Dm.truthy(new_icon_state)) item_clear_from.worn_icon_state = Dm.initial(item_clear_from.type, (obj_item i) => i.worn_icon_state);
                if (Dm.truthy(new_lefthand_file)) item_clear_from.lefthand_file = Dm.initial(item_clear_from.type, (obj_item i) => i.lefthand_file);
                if (Dm.truthy(new_righthand_file)) item_clear_from.righthand_file = Dm.initial(item_clear_from.type, (obj_item i) => i.righthand_file);
            }
        }
    }

    /// Gets a preview image for this skin based on the given atom's icon and icon_state
    // tg: code/datums/components/reskinnable_atom.dm:123
    public virtual object get_preview_icon(atom for_atom = null)
    {
        return image(
            icon: Dm.truthy(new_icon) ? new_icon : for_atom.icon,
            icon_state: Dm.truthy(new_icon_state) ? new_icon_state : for_atom.icon_state);
    }
}

/**
 * ### Reskinnable atoms
 *
 * Simple component which lets an atom be alt-clicked to open a radial menu to choose a new skin to apply.
 */
// tg: code/datums/components/reskinnable_atom.dm:134
public partial class datum_component_reskinable_item : datum_component
{
    // tg: code/datums/components/reskinnable_atom.dm:145
    [DmParams("base_reskin_type", "infinite", "initial_skin", "blacklisted_subtypes")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object base_reskin_type = Dm.arg(__extra, 0);
        object infinite = Dm.arg(__extra, 1) ?? FALSE;
        object initial_skin = Dm.arg(__extra, 2);
        DmList blacklisted_subtypes = Dm.arg<DmList>(__extra, 3);
        if (!isatom(parent) || isarea(parent))
            return COMPONENT_INCOMPATIBLE;

        atom atom_parent = parent as atom;
        // #ifdef UNIT_TESTS (:150-158): the loadout's greyscale check is the unit tests' (GLOB.all_loadout_datums), not built here.

        this.base_reskin_type = base_reskin_type;
        infinite_reskin = Dm.num(infinite);
        this.blacklisted_subtypes = blacklisted_subtypes;

        if (Dm.truthy(initial_skin))
            set_skin_by_name(initial_skin);

        DmList reskin_options = get_skins_by_name();
        if (Dm.length(reskin_options) <= 1) // Check that we actually have reskin options - if not there's no point to existing past this point.
            return COMPONENT_REDUNDANT;

        atom_parent.flags_1 = Dm.num(Dm.bitor(atom_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1));
        return null;
    }

    // tg: code/datums/components/reskinnable_atom.dm:173
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_CLICK_ALT, nameof(on_click_alt_reskin));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_TAGS, nameof(add_tags));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(add_context));
    }

    // tg: code/datums/components/reskinnable_atom.dm:178
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_CLICK_ALT);
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE_TAGS);
        UnregisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM);
    }

    // tg: code/datums/components/reskinnable_atom.dm:183
    [DmParams("comp", "base_reskin_type", "infinite", "initial_skin", "blacklisted_subtypes")]
    public override async DmTask<object> CheckDupeComponent(datum_component comp = null, params object[] __extra)
    {
        object base_reskin_type = Dm.arg(__extra, 0);
        object infinite = Dm.arg(__extra, 1) ?? FALSE;
        object initial_skin = Dm.arg(__extra, 2);
        DmList blacklisted_subtypes = Dm.arg<DmList>(__extra, 3);
        // Always absorb added components
        this.base_reskin_type = base_reskin_type;
        infinite_reskin = Dm.num(infinite);
        this.blacklisted_subtypes = blacklisted_subtypes;

        if (Dm.truthy(initial_skin))
            set_skin_by_name(initial_skin);

        DmList reskin_options = get_skins_by_name();
        if (Dm.length(reskin_options) <= 1) // Check that we actually have reskin options - if not there's no point to existing past this point.
            qdel(this);

        return TRUE;
    }

    // tg: code/datums/components/reskinnable_atom.dm:198
    public virtual DmList get_skins_by_name()
    {
        DmList reskin_options = Dm.list();
        foreach (Type reskin_option in Dm.each<Type>(Dm.sub(valid_subtypesof(base_reskin_type as Type), blacklisted_subtypes)))
            reskin_options[Dm.initial(reskin_option, (datum_atom_skin s) => s.preview_name)] = reskin_option;

        return reskin_options;
    }

    // tg: code/datums/components/reskinnable_atom.dm:205
    public virtual void set_skin_by_name(object input_name = null, mob user = null)
    {
        DmList reskin_options = get_skins_by_name();
        DmList atom_skins = get_atom_skins();
        if (Dm.truthy(current_skin))
        {
            datum_atom_skin previous_skin = atom_skins[reskin_options[current_skin]] as datum_atom_skin;
            previous_skin.clear_skin(parent as atom, user);
        }

        if (Dm.truthy(input_name))
        {
            datum_atom_skin reskin_to_apply = atom_skins[reskin_options[input_name]] as datum_atom_skin;
            reskin_to_apply.apply(parent as atom, user);
        }

        current_skin = input_name;

        atom atom_parent = parent as atom;
        atom_parent.update_appearance();

        if (isitem(parent))
        {
            obj_item item_parent = parent as obj_item;
            item_parent.update_slot_icon();
        }

        if (Dm.truthy(parent._listen_lookup?[COMSIG_OBJ_RESKIN]))
            parent._SendSignal(COMSIG_OBJ_RESKIN, Dm.list(parent, input_name));
    }

    // tg: code/datums/components/reskinnable_atom.dm:227
    [SignalHandler]
    public virtual object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_ALT_LMB] = I18n.T("Reskin");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/datums/components/reskinnable_atom.dm:233
    [SignalHandler]
    public virtual void add_tags(atom source = null, mob user = null, DmList tags = null)
    {
        tags["reskinnable"] = I18n.T("This item is able to be reskinned! Alt-Click to do so!");
    }

    /// Called when alt clicked and the item has unique reskin options
    // tg: code/datums/components/reskinnable_atom.dm:239
    [SignalHandler]
    public virtual object on_click_alt_reskin(datum source = null, mob user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(parent as atom, NEED_DEXTERITY))))
            return NONE;

        Waiting.Detach(reskin_obj(user));
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Reskins object based on a user's choice
     *
     * Arguments:
     * * user The mob choosing a reskin option
     */
    // tg: code/datums/components/reskinnable_atom.dm:254
    public virtual async DmTask reskin_obj(mob user = null)
    {
        atom atom_parent = parent as atom;

        DmList items = Dm.list();
        DmList atom_skins = get_atom_skins();
        DmList skins_by_name = get_skins_by_name();
        foreach (object reskin_name in Dm.each(skins_by_name))
        {
            object reskin_typepath = skins_by_name[reskin_name];
            datum_atom_skin reskin = atom_skins[reskin_typepath] as datum_atom_skin;
            items[reskin_name] = reskin.get_preview_icon(atom_parent);
        }

        sort_list(items);

        object pick = await show_radial_menu(user, parent as atom, items, custom_check: Dm.@new<datum_callback>(this, nameof(check_reskin_menu), user), radius: 38, require_near: TRUE);
        if (!Dm.truthy(pick) || !Dm.truthy(items[pick]))
            return;

        set_skin_by_name(pick, user);
        to_chat(user, span_info(I18n.T("[] is now skinned as '[].'", parent, pick)));

        if (!Dm.truthy(infinite_reskin))
            qdel(this);
    }

    /**
     * Checks if we are allowed to interact with a radial menu for reskins
     *
     * Arguments:
     * * user The mob interacting with the menu
     */
    // tg: code/datums/components/reskinnable_atom.dm:281
    public virtual object check_reskin_menu(mob user = null)
    {
        if (QDELETED(parent))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }
}
