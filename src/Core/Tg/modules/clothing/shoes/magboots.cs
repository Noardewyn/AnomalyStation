// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/magboots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 2: the magboots (the mag-pulse toggle in hand, its traits, slowdown, icon state and rigstep footsteps). The
// "Toggle Magboots" verb itself waits for the verbs (M9; its body __gvb_toggle is translated); the item action button
// ("Toggle magboots", the generic toggle) came with M4c slice 3. The fishing difficulty element is fishing (M10+). The advanced and
// blood-red magboots are var-only. The var blocks are generated.

// tg: code/modules/clothing/shoes/magboots.dm:4
public partial class obj_item_clothing_shoes_magboots : obj_item_clothing_shoes
{
    // The earlier of the type's two Initialize definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/modules/clothing/shoes/magboots.dm:33
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shoe_footstep), inactive_step_sounds, Dm.named("volume", 50)));
        return dot;
    }

    // tg: code/modules/clothing/shoes/magboots.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = Initialize__d0(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        RegisterSignal(this, COMSIG_SPEED_POTION_APPLIED, nameof(on_speed_potioned));
        if (Dm.truthy(fishing_modifier))
        {
            // AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)
            Dm.Deferred("code/modules/clothing/shoes/magboots.dm:42", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty), the fishing difficulty (fishing, M10+)");
        }
        return dot;
    }

    /// Signal handler for [COMSIG_SPEED_POTION_APPLIED]. Speed potion removes the active slowdown
    // tg: code/modules/clothing/shoes/magboots.dm:45
    [SignalHandler]
    public virtual void on_speed_potioned(datum source = null)
    {
        // Don't need to touch the actual slowdown here, since the speed potion does it for us
        slowdown_active = 0;

        if (Dm.truthy(magpulse) && Dm.truthy(magpulse_fishing_modifier))
        {
            // RemoveElement(/datum/element/adjust_fishing_difficulty) and AddElement(..., fishing_modifier): fishing (M10+)
            Dm.Deferred("code/modules/clothing/shoes/magboots.dm:52", "on_speed_potioned: the fishing difficulty element (fishing, M10+)");
        }
        magpulse_fishing_modifier = fishing_modifier;
    }

    // tg: code/modules/clothing/shoes/magboots.dm:57  GAME_VERB_SRC(/obj/item/clothing/shoes/magboots, toggle, usr, "Toggle Magboots", null)
    public virtual void toggle(params object[] __extra)
    {
        // The verb queues its body (__gvb_toggle, below) through SSverb_manager: the verbs reach the client with the command
        // layer (rules 7.10, M9).
        Dm.NotTranslated("code/modules/clothing/shoes/magboots.dm:57", "toggle: the Toggle Magboots verb (verbs, M10+)");
    }

    // tg: code/modules/clothing/shoes/magboots.dm:57  (the verb's body, __gvb_toggle)
    public virtual async DmTask __gvb_toggle(DmList structured_args = null)
    {
        if (!Dm.truthy(await can_use(usr)))
            return;
        await attack_self(usr);
    }

    // tg: code/modules/clothing/shoes/magboots.dm:63
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        magpulse = Dm.truthy(magpulse) ? FALSE : TRUE;
        if (Dm.truthy(magpulse))
        {
            attach_clothing_traits(active_traits);
            slowdown += slowdown_active;
            if (Dm.truthy(magpulse_fishing_modifier))
            {
                // AddElement(/datum/element/adjust_fishing_difficulty, magpulse_fishing_modifier): fishing (M10+)
                Dm.Deferred("code/modules/clothing/shoes/magboots.dm:69", "attack_self: the fishing difficulty element (fishing, M10+)");
            }
            else if (magpulse_fishing_modifier != fishing_modifier)
            {
                // RemoveElement(/datum/element/adjust_fishing_difficulty): fishing (M10+)
                Dm.Deferred("code/modules/clothing/shoes/magboots.dm:71", "attack_self: the fishing difficulty element (fishing, M10+)");
            }
            await AddComponent(typeof(datum_component_shoe_footstep), active_step_sounds, Dm.named("volume", 50));
        }
        else
        {
            if (Dm.truthy(fishing_modifier))
            {
                // AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing (M10+)
                Dm.Deferred("code/modules/clothing/shoes/magboots.dm:75", "attack_self: the fishing difficulty element (fishing, M10+)");
            }
            else if (magpulse_fishing_modifier != fishing_modifier)
            {
                // RemoveElement(/datum/element/adjust_fishing_difficulty): fishing (M10+)
                Dm.Deferred("code/modules/clothing/shoes/magboots.dm:77", "attack_self: the fishing difficulty element (fishing, M10+)");
            }
            detach_clothing_traits(active_traits);
            slowdown -= slowdown_active;
            await AddComponent(typeof(datum_component_shoe_footstep), inactive_step_sounds, Dm.named("volume", 50));
        }

        update_appearance();
        balloon_alert(user, I18n.T("mag-pulse []", Dm.truthy(magpulse) ? I18n.T("enabled") : I18n.T("disabled")));
        //we want to update our speed so we arent running at max speed in regular magboots
        await user.update_equipment_speed_mods();
        return null;
    }

    // tg: code/modules/clothing/shoes/magboots.dm:87
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("Its mag-pulse traction system appears to be [].", Dm.truthy(magpulse) ? I18n.T("enabled") : I18n.T("disabled")));
        return dot;
    }

    // tg: code/modules/clothing/shoes/magboots.dm:91
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, magpulse);
        return dot;
    }
}
