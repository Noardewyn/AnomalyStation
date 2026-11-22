// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/modules_ninja.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the cloaking module, /obj/item/mod/module/stealth (:4-59), translated (gaps MO2, D333: the base of the syndicate's
// Wraith cloaking module, modules_antag.dm). The space ninja's modules of this file (the advanced cloak, the hacker, the netcom, the
// weapon recall, the adrenaline boost, the EMP pulse, the energy net, the star dispenser, the status readout and the rest) come with
// the ninja (M10). The var blocks are generated (D111).

///Cloaking - Lowers the user's visibility, can be interrupted by being touched or attacked.
// tg: code/modules/mod/modules/modules_ninja.dm:4
public partial class obj_item_mod_module_stealth : obj_item_mod_module
{
    // tg: code/modules/mod/modules/modules_ninja.dm:23
    public override async DmTask on_activation(mob activator = null)
    {
        if (Dm.truthy(bumpoff))
            RegisterSignal(mod.wearer, COMSIG_LIVING_MOB_BUMP, nameof(unstealth));
        RegisterSignal(mod.wearer, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
        RegisterSignal(mod.wearer, COMSIG_ATOM_BULLET_ACT, nameof(on_bullet_act));
        RegisterSignals(mod.wearer, Dm.list(COMSIG_MOB_ITEM_ATTACK, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_HITBY, COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACK_PAW, COMSIG_CARBON_CUFF_ATTEMPTED), nameof(unstealth));
        animate(mod.wearer, Dm.named("alpha", stealth_alpha), Dm.named("time", 1.5f * SECONDS));
        await drain_power(use_energy_cost);
    }

    // tg: code/modules/mod/modules/modules_ninja.dm:32
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        display_message ??= TRUE;
        deleting ??= FALSE;
        if (Dm.truthy(bumpoff))
            UnregisterSignal(mod.wearer, COMSIG_LIVING_MOB_BUMP);
        UnregisterSignal(mod.wearer, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_MOB_ITEM_ATTACK, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_BULLET_ACT, COMSIG_ATOM_HITBY, COMSIG_ATOM_HULK_ATTACK, COMSIG_ATOM_ATTACK_PAW, COMSIG_CARBON_CUFF_ATTEMPTED));
        animate(mod.wearer, Dm.named("alpha", 255), Dm.named("time", 1.5f * SECONDS));
    }

    // tg: code/modules/mod/modules/modules_ninja.dm:38
    [SignalHandler]
    public virtual void unstealth(datum source = null)
    {
        to_chat(mod.wearer, span_warning(I18n.T("[] gets discharged from contact!", this)));
        do_sparks(2, TRUE, this);
        Waiting.NoSleep(drain_power(use_energy_cost));
        Waiting.NoSleep(deactivate());
    }

    // tg: code/modules/mod/modules/modules_ninja.dm:46
    [SignalHandler]
    public virtual void on_unarmed_attack(datum source = null, atom target = null)
    {
        if (!isliving(target))
            return;
        unstealth(source);
    }

    // tg: code/modules/mod/modules/modules_ninja.dm:53
    [SignalHandler]
    public virtual void on_bullet_act(datum source = null, obj_projectile projectile = null)
    {
        if (!Dm.truthy(projectile.is_hostile_projectile()))
            return;
        unstealth(source);
    }
}
