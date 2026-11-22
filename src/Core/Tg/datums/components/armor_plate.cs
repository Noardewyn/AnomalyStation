// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/armor_plate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/components/armor_plate.dm:1
public partial class datum_component_armor_plate : datum_component
{
    // tg: code/datums/components/armor_plate.dm:21
    [DmParams("maxamount", "upgrade_item", "armor_mod", "upgrade_prefix")]
    public override DmTask<object> Initialize(params object[] __extra)
    {
        object maxamount__1 = Dm.arg(__extra, 0);
        object upgrade_item__1 = Dm.arg(__extra, 1);
        object armor_mod__1 = Dm.arg(__extra, 2);
        object upgrade_prefix__1 = Dm.arg(__extra, 3) ?? "reinforced";

        if (!isobj(parent))
            return DmTask<object>.FromResult(COMPONENT_INCOMPATIBLE);

        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(applyplate));
        RegisterSignal(parent, COMSIG_QDELETING, nameof(dropplates));
        if (istype(parent, typeof(obj_vehicle_sealed_mecha_ripley)))
            RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(apply_mech_overlays));

        if (Dm.truthy(maxamount__1))
            maxamount = Dm.num(maxamount__1);
        if (Dm.truthy(upgrade_item__1))
            upgrade_item = upgrade_item__1 as Type;
        if (Dm.truthy(armor_mod__1))
            armor_mod = armor_mod__1 as Type;
        if (Dm.truthy(upgrade_prefix__1))
            upgrade_prefix = upgrade_prefix__1 as string;
        upgrade_name = Dm.initial(upgrade_item, "name") as string;
        return DmTask<object>.FromResult(null);
    }

    // tg: code/datums/components/armor_plate.dm:43
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref plate_component);
        return base.Destroy(force);
    }

    // tg: code/datums/components/armor_plate.dm:47
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        //upgrade_item could also be typecast here instead
        if (ismecha(parent))
        {
            if (Dm.truthy(amount))
            {
                if (amount < maxamount)
                    examine_list.Add(span_notice(I18n.T("Its armor is enhanced with [] [].", amount, upgrade_name)));
                else
                    examine_list.Add(span_notice(I18n.T("It's wearing a fearsome carapace entirely composed of [] - its pilot must be an experienced monster hunter.", upgrade_name)));
            }
            else
                examine_list.Add(span_notice(I18n.T("It has attachment points for strapping monster hide on for added protection.")));
        }
        else
        {
            if (Dm.truthy(amount))
                examine_list.Add(span_notice(I18n.T("It has been strengthened with []/[] [].", amount, maxamount, upgrade_name)));
            else
                examine_list.Add(span_notice(I18n.T("It can be strengthened with up to [] [].", maxamount, upgrade_name)));
        }
    }

    // tg: code/datums/components/armor_plate.dm:65
    public virtual void applyplate(datum source = null, obj_item our_upgrade_item = null, mob user = null, object @params = null)
    {
        if (!istype(our_upgrade_item, upgrade_item))
            return;

        if (amount >= maxamount)
        {
            to_chat(user, span_warning(I18n.T("You can't improve [] any further!", parent)));
            return;
        }

        if (ismecha(parent) && plate_component == null)
        {
            obj_vehicle_sealed_mecha as_mecha = parent as obj_vehicle_sealed_mecha;
            if (Dm.num(LAZYLEN(as_mecha.equip_by_category[MECHA_ARMOR] as DmList)) >= Dm.num(as_mecha.max_equip_by_category[MECHA_ARMOR]))
            {
                to_chat(user, span_warning(I18n.T("[] doesn't have any availible armor slots!", as_mecha)));
                return;
            }
        }

        if (isstack(our_upgrade_item))
            Waiting.Detach((our_upgrade_item as obj_item_stack).use(1));
        else
        {
            if (Dm.truthy(length(our_upgrade_item.contents)))
            {
                to_chat(user, span_warning(I18n.T("[] cannot be used for armoring while there's something inside!", our_upgrade_item)));
                return;
            }
            qdel(our_upgrade_item);
        }

        obj target_for_upgrading = parent as obj;
        amount++;
        target_for_upgrading.set_armor((target_for_upgrading.get_armor() as datum_armor).add_other_armor(armor_mod));
        target_for_upgrading.SEND_SIGNAL(COMSIG_ARMOR_PLATED, Dm.list(target_for_upgrading, amount, maxamount));

        if (!ismecha(target_for_upgrading))
        {
            if (Dm.truthy(upgrade_prefix) && !Dm.truthy(have_upgraded))
            {
                target_for_upgrading.name = Dm.text("[] []", upgrade_prefix, target_for_upgrading.name);
                have_upgraded = TRUE;
            }
            to_chat(user, span_info(I18n.T("You strengthen [], improving its resistance against attacks.", target_for_upgrading)));
            return;
        }

        obj_vehicle_sealed_mecha mecha_for_upgrading = target_for_upgrading as obj_vehicle_sealed_mecha;
        mecha_for_upgrading.update_appearance();
        to_chat(user, span_info(I18n.T("You strengthen [], improving its resistance against attacks.", mecha_for_upgrading)));
        if (plate_component != null)
            return;
        plate_component = Dm.@new<obj_item_mecha_parts_mecha_equipment_armor_armor_plate>(mecha_for_upgrading);
        plate_component.name = our_upgrade_item.name;
        plate_component.desc = our_upgrade_item.desc;
        plate_component.icon = our_upgrade_item.icon;
        plate_component.icon_state = our_upgrade_item.icon_state;
        Waiting.Detach(plate_component.attach(mecha_for_upgrading));
    }

    // tg: code/datums/components/armor_plate.dm:116
    public virtual void dropplates(datum source = null, object force = null)
    {
        if (ismecha(parent)) //items didn't drop the plates before and it causes erroneous behavior for the time being with collapsible helmets
        {
            for (int i = 1; i <= amount; i++)
                Dm.@new(upgrade_item, get_turf(parent));
        }
    }

    // tg: code/datums/components/armor_plate.dm:122
    public virtual void apply_mech_overlays(obj_vehicle_sealed_mecha mech = null, DmList overlays = null)
    {
        if (Dm.truthy(amount))
        {
            string overlay_string = "ripley-g";
            if (amount >= 3)
                overlay_string += "-full";
            if (!Dm.truthy(LAZYLEN(mech.occupants)))
                overlay_string += "-open";
            overlays.Add(overlay_string);
        }
    }
}
