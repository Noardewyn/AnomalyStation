// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:1
public partial class obj_item_raptor_dex : obj_item
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:14
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RaptorDex");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:20
    public override async DmTask<object> ui_data(mob user = null)
    {
        return scan_data;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:23
    public override async DmTask<object> interact_with_atom(atom attacked_atom = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(attacked_atom is mob_living_basic_raptor my_raptor))
            return NONE;

        scan_data = raptor_scan_data(my_raptor);
        playsound(this, Dm.resource("sound/mobs/non-humanoids/orbie/orbie_send_out.ogg"), 20);
        my_raptor.balloon_alert(user, "scanned");
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_dex.dm:72
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (!(target is mob_living_basic_raptor))
            return NONE;
        context[SCREENTIP_CONTEXT_LMB] = I18n.T("Scan Raptor");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    /// The scan's data, as tg's interact_with_atom builds it (raptor_dex.dm:27-66) and the RaptorDex program's tap repeats it
    /// (raptordex.dm:19-58). DEVIATION(DV619): the raptor's picture and the friendship hearts go as their icon, icon state and
    /// the hearts' states, which the client draws, not as tg's icon2base64 pictures the server renders.
    internal static DmList raptor_scan_data(mob_living_basic_raptor my_raptor)
    {
        datum_movespeed_modifier intent_mod = my_raptor.get_move_intent_slowdown() as datum_movespeed_modifier;

        DmList scan_data = Dm.list();
        scan_data["raptor_scan"] = TRUE;
        // scan_data["raptor_image"] = icon2base64(icon(icon = my_raptor.icon, icon_state = my_raptor.icon_state, dir = SOUTH, frame = 1))
        scan_data["raptor_icon"] = my_raptor.icon?.ToString();
        scan_data["raptor_icon_state"] = my_raptor.icon_state;
        scan_data["raptor_attack"] = my_raptor.melee_damage_lower;
        scan_data["raptor_health"] = my_raptor.health;
        scan_data["raptor_max_health"] = my_raptor.maxHealth;
        scan_data["raptor_speed"] = my_raptor.speed + Dm.num(intent_mod?.multiplicative_slowdown);
        scan_data["raptor_color"] = my_raptor.name;
        scan_data["raptor_gender"] = my_raptor.gender;
        scan_data["raptor_growth"] = my_raptor.growth_progress;
        scan_data["can_grow"] = !Dm.eq(my_raptor.growth_stage, RAPTOR_ADULT) ? TRUE : FALSE;
        scan_data["raptor_description"] = (my_raptor.raptor_color as datum_raptor_color).description;

        object happiness_percentage = (my_raptor.ai_controller as datum_ai_controller)?.blackboard[BB_BASIC_HAPPINESS];
        // The happiness overlay's hearts (happiness.dm:162-170): four of them, as many full as the percentage rounds to
        float display_amount = round(4 * Dm.num(happiness_percentage), 1);
        DmList hearts = Dm.list();
        for (float index = 1; index <= 4; index++)
            Dm.append(ref hearts, display_amount >= index ? "full_heart" : "empty_heart");
        scan_data["raptor_happiness"] = hearts;

        datum_raptor_inheritance inherit = my_raptor.inherited_stats;
        if (inherit != null)
        {
            scan_data["inherited_attack"] = inherit.attack_modifier;
            scan_data["inherited_attack_max"] = RAPTOR_INHERIT_MAX_ATTACK;
            scan_data["inherited_health"] = inherit.health_modifier;
            scan_data["inherited_health_max"] = RAPTOR_INHERIT_MAX_HEALTH;
            scan_data["inherited_speed"] = inherit.speed_modifier;
            scan_data["inherited_speed_max"] = RAPTOR_INHERIT_MAX_SPEED;
            scan_data["inherited_ability"] = inherit.ability_modifier;
            scan_data["inherited_ability_max"] = RAPTOR_INHERIT_MAX_MODIFIER;
            scan_data["inherited_growth"] = inherit.growth_modifier;
            scan_data["inherited_growth_max"] = RAPTOR_INHERIT_MAX_MODIFIER;

            DmList traits = Dm.list();
            scan_data["inherited_traits"] = traits;
            foreach (object index in Dm.each(inherit.personality_traits))
                Dm.append(ref traits, GLOB.raptor_inherit_traits[index]);
        }
        return scan_data;
    }
}
