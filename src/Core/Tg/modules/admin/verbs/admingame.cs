// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/admingame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A1, D316): only the admin combo HUD (enable_combo_hud and disable_combo_hud, and the Toggle Combo HUD verb's
// body, which the admin console's combo-hud runs, D275): the data HUDs and every mind's antagonist icons; gaps SL1 (D333) the Show Skill
// Panel verb's body (the console's skill-panel). The file's other verbs (the player panel and the rest) are the admin tools not ported
// (the console's commands stand in, D275).

// tg: code/modules/admin/verbs/admingame.dm:404
public partial class client : datum
{
    // tg: code/modules/admin/verbs/admingame.dm:416  #define ADMIN_HUDS list(TRAIT_SECURITY_HUD, TRAIT_MEDICAL_HUD, TRAIT_DIAGNOSTIC_HUD, TRAIT_BOT_PATH_HUD) (#undef at :448)
    private static DmList ADMIN_HUDS => Dm.list(TRAIT_SECURITY_HUD, TRAIT_MEDICAL_HUD, TRAIT_DIAGNOSTIC_HUD, TRAIT_BOT_PATH_HUD);

    // ADMIN_VERB(combo_hud, R_ADMIN, "Toggle Combo HUD", "Toggles the Admin Combo HUD.", ADMIN_CATEGORY_GAME): the verb's body, which the
    // admin console's combo-hud runs (D275; the verb's rights are the console's)
    // tg: code/modules/admin/verbs/admingame.dm:404
    public void toggle_combo_hud()
    {
        if (Dm.truthy(combo_hud_enabled))
            disable_combo_hud();
        else
            enable_combo_hud();

        to_chat(this, Dm.text("You toggled your admin combo HUD [].", Dm.truthy(combo_hud_enabled) ? "ON" : "OFF"), confidential: TRUE);
        message_admins(Dm.text("[] toggled their admin combo HUD [].", key_name_admin(this), Dm.truthy(combo_hud_enabled) ? "ON" : "OFF"));
        log_admin(Dm.text("[] toggled their admin combo HUD [].", key_name(this), Dm.truthy(combo_hud_enabled) ? "ON" : "OFF"));
        SSblackbox.record_feedback("nested tally", "admin_toggle", 1, Dm.list("Toggle Combo HUD", Dm.text("[]", Dm.truthy(combo_hud_enabled) ? "Enabled" : "Disabled"))); // If you are copy-pasting this, ensure the 4th parameter is unique to the new proc!
    }

    // ADMIN_VERB(show_skill_panel, R_ADMIN, "Show Skill Panel", "Edit mobs's experience and skill levels", ADMIN_CATEGORY_GAME): the verb's
    // body, which the admin console's skill-panel runs with its typed target (D275; the verb's rights are the console's); user is this client
    // tg: code/modules/admin/verbs/admingame.dm:462
    public async DmTask show_skill_panel(object target_mob)
    {
        datum_mind target_mind;
        if (target_mob is datum_mind)
            target_mind = (datum_mind)target_mob;
        else
            target_mind = ((mob)target_mob).mind;

        datum_skill_panel SP = await Dm.new_async<datum_skill_panel>(this, target_mind);
        await SP.ui_interact(mob);
    }

    // tg: code/modules/admin/verbs/admingame.dm:418
    public virtual void enable_combo_hud()
    {
        if (Dm.truthy(combo_hud_enabled))
            return;

        combo_hud_enabled = TRUE;

        foreach (object hudtrait in Dm.each(ADMIN_HUDS))
            ADD_TRAIT(mob, hudtrait, ADMIN_TRAIT);

        foreach (datum_atom_hud_alternate_appearance_basic_antagonist_hud antag_hud in Dm.each<datum_atom_hud_alternate_appearance_basic_antagonist_hud>(GLOB.active_alternate_appearances))
            antag_hud.show_to(mob);

        mob.lighting_cutoff = Dm.num(mob.default_lighting_cutoff());
        mob.update_sight();
    }

    // tg: code/modules/admin/verbs/admingame.dm:433
    public virtual void disable_combo_hud()
    {
        if (!Dm.truthy(combo_hud_enabled))
            return;

        combo_hud_enabled = FALSE;

        foreach (object hudtrait in Dm.each(ADMIN_HUDS))
            REMOVE_TRAIT(mob, hudtrait, ADMIN_TRAIT);

        foreach (datum_atom_hud_alternate_appearance_basic_antagonist_hud antag_hud in Dm.each<datum_atom_hud_alternate_appearance_basic_antagonist_hud>(GLOB.active_alternate_appearances))
            antag_hud.hide_from(mob);

        mob.lighting_cutoff = Dm.num(mob.default_lighting_cutoff());
        mob.update_sight();
    }
}
