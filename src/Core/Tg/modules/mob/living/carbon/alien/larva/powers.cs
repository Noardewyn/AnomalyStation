// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/powers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the larva's hiding and its evolution into a caste (a radial menu, drawn at the crosshair, DV40).
// The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/alien/larva/powers.dm:1
public partial class datum_action_cooldown_alien_hide : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:9
    public override async DmTask<object> Activate(atom target = null)
    {
        if (owner.layer == hide_layer)
        {
            owner.layer = Dm.initial(owner, (atom p) => p.layer);
            owner.visible_message(
                span_notice(I18n.T("[] slowly peeks up from the ground...", owner)),
                span_noticealien(I18n.T("You stop hiding.")));
            ADD_TRAIT(owner, TRAIT_IGNORE_ELEVATION, ACTION_TRAIT);
        }

        else
        {
            owner.layer = hide_layer;
            owner.visible_message(
                span_name(I18n.T("[] scurries to the ground!", owner)),
                span_noticealien(I18n.T("You are now hiding.")));
            REMOVE_TRAIT(owner, TRAIT_IGNORE_ELEVATION, ACTION_TRAIT);
        }

        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/alien/larva/powers.dm:28
public partial class datum_action_cooldown_alien_larva_evolve : datum_action_cooldown_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:66  var/static/list/caste_options (a proc-level static, rules 10.6)
    private static ref DmList Activate__caste_options => ref TgState.Current.datum_action_cooldown_alien_larva_evolve__Activate__caste_options;

    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:35
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext_x = 1;
        return button;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:40
    [DmParams("button", "force")]
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        atom_movable_screen_movable_action_button button = current_button;
        force ??= FALSE;
        await base.update_button_status(button, force);
        mob_living_carbon_alien_larva larva_owner = owner as mob_living_carbon_alien_larva;
        string percentage_shown = Dm.text("[]", round((larva_owner.amount_grown / XENOMORPH_MAX_GROWTH) * 100, 0.1f));
        button.maptext_x = (Dm.length(percentage_shown) >= 3) ? 0 : 1;
        button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("<span style='text-align: center'>[]%</span>", percentage_shown));
    }

    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:47
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!islarva(owner))
            return FALSE;

        mob_living_carbon_alien_larva larva = owner as mob_living_carbon_alien_larva;
        if (Dm.truthy(larva.handcuffed) || Dm.truthy(larva.legcuffed)) // Cuffing larvas ? Eh ?
            return FALSE;
        if (larva.amount_grown < XENOMORPH_MAX_GROWTH)
            return FALSE;
        if (Dm.truthy(Dm.bitand(larva.movement_type, VENTCRAWLING)))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:64
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_carbon_alien_larva larva = owner as mob_living_carbon_alien_larva;
        if (Activate__caste_options == null)
        {
            Activate__caste_options = Dm.list();

            // This can probably be genericized in the future.
            Type hunter_path = typeof(mob_living_carbon_alien_adult_hunter);
            datum_radial_menu_choice hunter = Dm.@new<datum_radial_menu_choice>();
            hunter.name = I18n.T("Hunter");
            hunter.image = Dm.image(icon: Dm.initial(hunter_path, (atom p) => p.icon), icon_state: Dm.initial(hunter_path, (atom p) => p.icon_state));
            hunter.info = span_info(I18n.T("Hunters are the most agile caste, tasked with hunting for hosts. They are faster than a human and can even pounce, but are not much tougher than a drone."));

            Activate__caste_options["Hunter"] = hunter;

            Type sentinel_path = typeof(mob_living_carbon_alien_adult_sentinel);
            datum_radial_menu_choice sentinel = Dm.@new<datum_radial_menu_choice>();
            sentinel.name = I18n.T("Sentinel");
            sentinel.image = Dm.image(icon: Dm.initial(sentinel_path, (atom p) => p.icon), icon_state: Dm.initial(sentinel_path, (atom p) => p.icon_state));
            sentinel.info = span_info(I18n.T("Sentinels are tasked with protecting the hive. With their ranged spit, invisibility, and high health, they make formidable guardians and acceptable secondhand hunters."));

            Activate__caste_options["Sentinel"] = sentinel;

            Type drone_path = typeof(mob_living_carbon_alien_adult_drone);
            datum_radial_menu_choice drone = Dm.@new<datum_radial_menu_choice>();
            drone.name = I18n.T("Drone");
            drone.image = Dm.image(icon: Dm.initial(drone_path, (atom p) => p.icon), icon_state: Dm.initial(drone_path, (atom p) => p.icon_state));
            drone.info = span_info(I18n.T("Drones are the weakest and slowest of the castes, but can grow into a praetorian and then queen if no queen exists, and are vital to maintaining a hive with their resin secretion abilities."));

            Activate__caste_options["Drone"] = drone;
        }

        object alien_caste = await show_radial_menu(owner, owner, Activate__caste_options, radius: 38, require_near: TRUE, tooltips: TRUE);
        if (QDELETED(this) || QDELETED(owner) || !Dm.truthy(await IsAvailable(feedback: TRUE)) || alien_caste == null)
            return null;

        mob_living_carbon_alien_adult new_xeno = null;
        object sw__1 = alien_caste;
        if (Dm.eq(sw__1, "Hunter"))
            new_xeno = Dm.@new<mob_living_carbon_alien_adult_hunter>(larva.loc);
        else if (Dm.eq(sw__1, "Sentinel"))
            new_xeno = Dm.@new<mob_living_carbon_alien_adult_sentinel>(larva.loc);
        else if (Dm.eq(sw__1, "Drone"))
            new_xeno = Dm.@new<mob_living_carbon_alien_adult_drone>(larva.loc);
        else
            throw Dm.CRASH(Dm.text("Alien evolve was given an invalid / incorrect alien cast type. Got: []", alien_caste), "code/modules/mob/living/carbon/alien/larva/powers.dm", 113);

        await larva.alien_evolve(new_xeno);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/alien/larva/powers.dm:66
    internal DmList datum_action_cooldown_alien_larva_evolve__Activate__caste_options;
}
