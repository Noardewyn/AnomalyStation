// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/subtype_picker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * subtype picker component allows for an item to transform into its subtypes (this is not enforced and you can turn in whatever types, but
 * i used this name as it was incredibly accurate for current usage of the behavior)
 *
 * Used for the null rod to pick the other holy weapons.
 */
// tg: code/datums/components/subtype_picker.dm:7
public partial class datum_component_subtype_picker : datum_component
{
    // tg: code/datums/components/subtype_picker.dm:17
    [DmParams("subtype2descriptions", "on_picked_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object subtype2descriptions = Dm.arg(__extra, 0);
        object on_picked_callback = Dm.arg(__extra, 1);
        object dot = await base.Initialize(Dm.forward(__extra, subtype2descriptions, on_picked_callback));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        this.subtype2descriptions = subtype2descriptions as DmList;
        this.on_picked_callback = on_picked_callback as datum_callback;
        build_radial_list();
        return dot;
    }

    // tg: code/datums/components/subtype_picker.dm:25
    public override object Destroy(float? force = null)
    {
        on_picked_callback = null;
        subtype2descriptions = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/subtype_picker.dm:30
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
    }

    // tg: code/datums/components/subtype_picker.dm:34
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_ITEM_ATTACK_SELF);
    }

    ///signal called by the stat of the target changing
    // tg: code/datums/components/subtype_picker.dm:39
    [SignalHandler]
    public virtual void on_attack_self(datum target = null, mob user = null)
    {
        Waiting.Detach(pick_subtype(target, user));
    }

    /**
     * pick_subtype: turns the list of types to their description into all the data radial menus need
     */
    // tg: code/datums/components/subtype_picker.dm:46
    public virtual void build_radial_list()
    {
        built_radial_list = Dm.list();
        name2subtype = Dm.list();
        foreach (Type subtype in Dm.each_cast<Type>(subtype2descriptions))
        {
            datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
            option.image = image(icon: Dm.initial(subtype, "icon"), icon_state: Dm.initial(subtype, "icon_state"));
            option.info = span_boldnotice(Dm.str(subtype2descriptions[subtype]));

            name2subtype[Dm.initial(subtype, "name")] = subtype;
            built_radial_list[Dm.initial(subtype, "name")] = option; // tg: built_radial_list += list(initial(subtype.name) = option)
        }
        built_radial_list = sort_list(built_radial_list) as DmList;
    }

    /**
     * pick_subtype: called from on_attack_self, shows a user a radial menu of all available null rod reskins and replaces the current null rod with the user's chosen reskinned variant
     *
     * Arguments:
     * * target: parent this element is attached to that is being activated
     * * picker: user who interacted with the item
     */
    // tg: code/datums/components/subtype_picker.dm:66
    public virtual async DmTask pick_subtype(datum target = null, mob picker = null)
    {
        object name_of_type = await show_radial_menu(picker, target as atom, built_radial_list, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), target, picker), radius: 42, require_near: TRUE);
        if (!Dm.truthy(name_of_type) || !Dm.truthy(check_menu(target, picker)))
            return;

        Type picked_subtype = name2subtype[name_of_type] as Type;
        obj_item picked = Dm.@new(picked_subtype, picker.drop_location()) as obj_item;
        if (on_picked_callback != null)
            await on_picked_callback.Invoke(picked, picker);
        if (Dm.truthy(picked?._listen_lookup?[COMSIG_ITEM_SUBTYPE_PICKER_SELECTED]))
            picked._SendSignal(COMSIG_ITEM_SUBTYPE_PICKER_SELECTED, Dm.list(picked, target, picker));

        qdel(target);
        await picker.put_in_hands(picked);
    }

    /**
     * Checks if we are allowed to interact with the radial menu
     *
     * Arguments:
     * * target: parent the radial menu is from
     * * user: the mob interacting with the menu
     */
    // tg: code/datums/components/subtype_picker.dm:87
    public virtual object check_menu(datum target = null, mob user = null)
    {
        if (user == null)
            return FALSE;
        if (QDELETED(target))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(target as obj_item)))
            return FALSE;
        return TRUE;
    }
}
