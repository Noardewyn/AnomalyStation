// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Status effect gained by basilisks when they touch something hot
// tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:2
public partial class datum_status_effect_basilisk_overheat : datum_status_effect
{
    /// Things which will chill us out if we get hit by them
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:7 (var/static/list/chilling_reagents)
    public static DmList chilling_reagents => TgState.Current.datum_status_effect_basilisk_overheat__chilling_reagents ??= Dm.list(
        typeof(datum_reagent_medicine_cryoxadone),
        typeof(datum_reagent_firefighting_foam),
        typeof(datum_reagent_consumable_frostoil),
        typeof(datum_reagent_consumable_ice),
        typeof(datum_reagent_water));

    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:15
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (!Dm.truthy(dot) || !(owner is mob_living_basic_mining_basilisk) || IS_UNCONSCIOUS_OR_CRIT(owner))
            return FALSE;
        mob_living_basic_mining_basilisk hot_stuff = owner as mob_living_basic_mining_basilisk;
        hot_stuff.visible_message(span_warning(I18n.T("[] is getting fired up!", hot_stuff)));
        await hot_stuff.fully_heal();
        hot_stuff.icon_living = "basilisk_alert";
        hot_stuff.icon_state = "basilisk_alert";
        hot_stuff.update_appearance(UPDATE_ICON_STATE);
        await hot_stuff.add_movespeed_modifier(typeof(datum_movespeed_modifier_basilisk_overheat));
        hot_stuff.set_projectile_type(typeof(obj_projectile_basilisk_hot));

        RegisterSignal(hot_stuff, COMSIG_LIVING_DEATH, nameof(on_death));
        RegisterSignal(hot_stuff, COMSIG_ATOM_EXPOSE_REAGENTS, nameof(on_splashed));
        RegisterSignal(hot_stuff, COMSIG_ATOM_BULLET_ACT, nameof(on_shot));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:32
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        mob_living_basic_mining_basilisk hot_stuff = owner as mob_living_basic_mining_basilisk;
        hot_stuff.icon_living = "basilisk";
        hot_stuff.icon_state = "basilisk";
        hot_stuff.set_projectile_type(typeof(obj_projectile_temp_watcher));

        hot_stuff.update_appearance(UPDATE_ICON_STATE);
        hot_stuff.remove_movespeed_modifier(typeof(datum_movespeed_modifier_basilisk_overheat));
        UnregisterSignal(hot_stuff, Dm.list(COMSIG_LIVING_DEATH, COMSIG_ATOM_EXPOSE_REAGENTS, COMSIG_ATOM_BULLET_ACT));

        if (IS_UNCONSCIOUS_OR_CRIT(hot_stuff))
            return dot;
        hot_stuff.visible_message(span_notice(I18n.T("[] seems to have cooled down.", hot_stuff)));
        obj_effect_particle_effect_fluid_smoke poof = Dm.@new<obj_effect_particle_effect_fluid_smoke>(get_turf(hot_stuff));
        poof.lifetime = 2 * SECONDS;
        return dot;
    }

    /// Cool down if we die
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:50
    [SignalHandler]
    public virtual void on_death()
    {
        qdel(this);
    }

    /// Cool down if splashed with water
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:55
    [SignalHandler]
    public virtual void on_splashed(atom source = null, DmList reagents = null, datum_reagents source_reagents = null, object methods = null, object show_message = null)
    {
        if (!Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
            return;
        foreach (datum reagent in Dm.each<datum>(reagents))
        {
            if (!Dm.truthy(is_type_in_list(reagent, chilling_reagents)))
                continue;
            qdel(this);
            return;
        }
    }

    /// Cool down if shot with a cryo beam
    // tg: code/modules/mob/living/basic/lavaland/basilisk/basilisk_overheat.dm:66
    [SignalHandler]
    public virtual void on_shot(datum source = null, obj_projectile cryo_shot__any = null)
    {
        obj_projectile_temp cryo_shot = cryo_shot__any as obj_projectile_temp;
        if (cryo_shot == null || Dm.num(cryo_shot.temperature) > 0)
            return;
        qdel(this);
    }
}

public sealed partial class TgState
{
    internal DmList datum_status_effect_basilisk_overheat__chilling_reagents;
}
