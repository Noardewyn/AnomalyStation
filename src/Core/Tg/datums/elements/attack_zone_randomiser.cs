// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/attack_zone_randomiser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Pick a random attack zone before you attack something
// tg: code/datums/elements/attack_zone_randomiser.dm:2
public partial class datum_element_attack_zone_randomiser : datum_element
{
    // tg: code/datums/elements/attack_zone_randomiser.dm:8
    [DmParams("target", "valid_attack_zones")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList valid_attack_zones = Dm.arg<DmList>(__extra, 0) ?? GLOB.all_body_zones;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignals(target, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_LIVING_UNARMED_ATTACK), nameof(randomise));
        this.valid_attack_zones = valid_attack_zones;
        return dot;
    }

    // tg: code/datums/elements/attack_zone_randomiser.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_LIVING_UNARMED_ATTACK));
        return base.Detach(source, __extra);
    }

    /// If we're attacking a carbon, pick a random defence zone
    // tg: code/datums/elements/attack_zone_randomiser.dm:20
    [SignalHandler]
    public virtual void randomise(mob_living source = null, atom target = null)
    {
        if (!iscarbon(target))
            return;
        if (source.mind != null && source.hud_used?.screen_objects[HUD_MOB_ZONE_SELECTOR] != null)
            return;
        mob_living living_target = (mob_living)target;
        DmList blacklist_zones = Dm.sub(GLOB.all_body_zones, valid_attack_zones) as DmList;
        object new_zone = living_target.get_random_valid_zone(blacklisted_parts: blacklist_zones, bypass_warning: TRUE);
        if (new_zone == null)
            new_zone = BODY_ZONE_CHEST;
        atom_movable_screen_zone_sel zone_selector = source.hud_used?.screen_objects[HUD_MOB_ZONE_SELECTOR] as atom_movable_screen_zone_sel;
        if (zone_selector == null)
            source.zone_selected = new_zone as string;
        else
            zone_selector.set_selected_zone(new_zone, source, should_log: FALSE);
    }
}
