// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/ai_upgrades.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326: the AI upgrade modules a law rack installs). The var blocks are generated (D111).

/// AI module which gives some specific malf ai ability when installed in a law rack
// tg: code/game/objects/items/AI_modules/ai_upgrades.dm:4
public partial class obj_item_ai_module_upgrade : obj_item_ai_module
{
    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        gift = (datum_ai_module)Dm.@new(upgrade_type);
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:18
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref gift);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("Must be inserted into a module rack to function.")));
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:26
    public override async DmTask silicon_linked_to_installed(mob_living_silicon lawed = null)
    {
        if (!isAI(lawed))
            return;

        if (Dm.truthy(gift.unlock_text))
            to_chat(lawed, gift.unlock_text);
        if (gift.unlock_sound != null)
            lawed.playsound_local(lawed, gift.unlock_sound, 50, 0);

        if (Dm.truthy(gift.upgrade))
            await gift.upgrade__proc((mob_living_silicon_ai)lawed);
        else if (!Dm.each<datum_action>(lawed.actions).Any(a => gift.power_type.IsInstanceOfType(a)))
        {
            var gifted_action = (datum_action)Dm.@new(gift.power_type);
            await gifted_action.Grant(lawed);
        }
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:41
    public override void silicon_unlinked_from_installed(mob_living_silicon lawed = null)
    {
        if (!isAI(lawed))
            return;
        if (Dm.truthy(gift.upgrade))
        {
            // Can't un-upgrade (yet), so just return
            return;
        }
        if (Dm.truthy(IS_MALF_AI(lawed)))
        {
            // To prevent accidentally deleting a hard earned malf ability
            return;
        }

        var existing_action = Dm.each<datum_action_innate_ai>(lawed.actions).FirstOrDefault(a => gift.power_type.IsInstanceOfType(a));
        qdel(existing_action);
    }
}

/// AI module which gives some ALL malf ai abilities when installed in a law rack
// tg: code/game/objects/items/AI_modules/ai_upgrades.dm:55
public partial class obj_item_ai_module_combat : obj_item_ai_module
{
    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_warning(I18n.T("Must be inserted into a module rack to function.")));
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:69
    public override void log_install(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        base.log_install(user, rack);
        message_admins(Dm.text("[] has installed [] into [] ([])", ADMIN_LOOKUPFLW(user), this, ADMIN_VERBOSEJMP(rack), rack.log_status()));
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:73
    public override void log_uninstall(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        base.log_uninstall(user, rack);
        message_admins(Dm.text("[] has removed [] from [] ([])", ADMIN_LOOKUPFLW(user), this, ADMIN_VERBOSEJMP(rack), rack.log_status()));
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:77
    public override async DmTask silicon_linked_to_installed(mob_living_silicon lawed = null)
    {
        if (lawed is not mob_living_silicon_ai combatai)
            return;
        if (combatai.malf_picker != null)
            return;
        to_chat(combatai, span_userdanger(I18n.T("Your module rack has been upgraded with combat software!")));
        to_chat(combatai, span_danger(I18n.T("Your current laws and objectives remain unchanged."))); //this unlocks malf powers, but does not give the license to plasma flood
        await combatai.add_malf_picker();
        combatai.hack_software = TRUE;
    }

    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:88
    public override void silicon_unlinked_from_installed(mob_living_silicon lawed = null)
    {
        if (lawed is not mob_living_silicon_ai combatai)
            return;
        // Don't delete a malf ai's malf picker please
        if (combatai.malf_picker == null || Dm.truthy(IS_MALF_AI(combatai)))
            return;
        // Does not revert upgrades, no clean way of doing that (yet)
        // Though it makes sense that they'd stick around, so I'm fine with it (for now)
        QDEL_NULL(ref combatai.malf_picker);
        QDEL_NULL(ref combatai.modules_action);
        foreach (datum_action_innate_ai leftover_action in Dm.each<datum_action_innate_ai>(combatai.actions?.Copy()))
            qdel(leftover_action);
        combatai.hack_software = FALSE;
    }
}

//Lipreading
// tg: code/game/objects/items/AI_modules/ai_upgrades.dm:104
public partial class obj_item_ai_module_upgrade_surveillance : obj_item_ai_module_upgrade
{
    // tg: code/game/objects/items/AI_modules/ai_upgrades.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CONTRABAND, INNATE_TRAIT);
        return dot;
    }
}
