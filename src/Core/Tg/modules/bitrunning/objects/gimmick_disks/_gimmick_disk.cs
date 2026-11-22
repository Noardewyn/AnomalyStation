// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the gimmick disks' loadouts (a briefcase of items and the actions granted to the avatar). The
// var blocks are generated (D111).

/**
 * Bitrunning tech disks which let you load full loadouts into the vdom on first avatar generation.
 */
// tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:3
public partial class obj_item_disk_bitrunning_gimmick : obj_item_disk_bitrunning
{
    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:12
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref granted_loadout);
        return base.Destroy(force);
    }

    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:16
    public override async DmTask<object> load_onto_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (granted_loadout == null)
            return BITRUNNER_GEAR_LOAD_FAILED;
        return await granted_loadout.grant_loadout(neo, avatar, domain_flags);
    }

    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:21
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (granted_loadout != null)
            return dot;

        DmList names = Dm.list();
        foreach (Type loadout in Dm.each<Type>(selectable_loadouts))
            Dm.append(ref names, Dm.initial(loadout, (datum_bitrunning_gimmick p) => p.name));

        object choice = await tgui_input_list(user, message: I18n.T("Select a gimmick loadout"), title: I18n.T("Bitrunning Program"), items: names);
        if (choice == null || !Dm.truthy(user.is_holding(this)))
            return dot;

        foreach (Type loadout in Dm.each<Type>(selectable_loadouts))
        {
            if (Dm.eq(Dm.initial(loadout, (datum_bitrunning_gimmick p) => p.name), choice))
                granted_loadout = await Dm.new_async(loadout) as datum_bitrunning_gimmick;
        }

        balloon_alert(user, I18n.T("selected"));
        playsound(user, Dm.resource("sound/items/click.ogg"), 50, TRUE);
        choice_made = choice;
        return dot;
    }
}

/**
 * The datum used by the gimmick loadout disk to determine what a loadout actually spawns.
 */
// tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:47
public partial class datum_bitrunning_gimmick : datum
{
    /// Grants out loadout.
    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:60
    public virtual async DmTask<object> grant_loadout(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        object return_flags = NONE;
        return_flags = Dm.bitor(return_flags, await grant_items(neo, avatar, domain_flags));
        return_flags = Dm.bitor(return_flags, await grant_abilities(neo, avatar, domain_flags));
        return return_flags;
    }

    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:66
    public virtual async DmTask<object> grant_items(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (!Dm.truthy(length(granted_items)))
            return NONE;

        if (Dm.truthy(Dm.bitand(domain_flags, DOMAIN_FORBIDS_ITEMS)))
            return BITRUNNER_GEAR_LOAD_BLOCKED;

        obj_item container_item = await Dm.new_async(container_item_type) as obj_item;
        if (Dm.truthy(prefix_container_name))
            container_item.name = Dm.text("[]'s []", LOWER_TEXT(name), Dm.initial(container_item, nameof(container_item.name)));

        foreach (Type granted_item in Dm.each<Type>(granted_items))
            await Dm.new_async(granted_item, container_item);

        await avatar.put_in_hands(container_item);

        return NONE;
    }

    // tg: code/modules/bitrunning/objects/gimmick_disks/_gimmick_disk.dm:84
    public virtual async DmTask<object> grant_abilities(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (!Dm.truthy(length(granted_actions)))
            return NONE;

        if (Dm.truthy(Dm.bitand(domain_flags, DOMAIN_FORBIDS_ABILITIES)))
            return BITRUNNER_GEAR_LOAD_BLOCKED;

        object return_flags = NONE;

        foreach (Type granted_action in Dm.each<Type>(granted_actions))
        {
            if (Dm.locate_type(granted_action, avatar.actions) != null)
            {
                return_flags = Dm.bitor(return_flags, BITRUNNER_GEAR_LOAD_FAILED);
                continue;
            }

            datum_action our_action = await Dm.new_async(granted_action) as datum_action;
            await our_action.Grant(avatar);
        }

        return return_flags;
    }
}
