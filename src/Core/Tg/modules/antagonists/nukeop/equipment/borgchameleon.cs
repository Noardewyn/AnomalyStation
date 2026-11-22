// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/borgchameleon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI9 (D326): the whole file (the saboteur cyborg's disguise). The var block is generated (D111); its static signal list is a
// TgState field.

// tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:4
public partial class obj_item_borg_chameleon : obj_item
{
    private static float ACTIVATION_COST => 0.3f * STANDARD_CELL_CHARGE;
    private static float ACTIVATION_UP_KEEP => 0.025f * STANDARD_CELL_RATE;

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:19  var/static/list/signalCache
    public DmList signalCache => TgState.Current.obj_item_borg_chameleon__signalCache ??= Dm.list( // list here all signals that should break the camouflage
        COMSIG_ATOM_ATTACKBY,
        COMSIG_ATOM_ATTACK_HAND,
        COMSIG_MOVABLE_IMPACT_ZONE,
        COMSIG_ATOM_BULLET_ACT,
        COMSIG_ATOM_EX_ACT,
        COMSIG_ATOM_FIRE_ACT,
        COMSIG_ATOM_EMP_ACT);

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        friendlyName = Dm.str(pick(GLOB.ai_names));
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:35
    public override object Destroy(float? force = null)
    {
        listeningTo = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:39
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        disrupt(user as mob_living_silicon_robot);
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:43
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        disrupt(user as mob_living_silicon_robot);
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:47
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        var borg = user as mob_living_silicon_robot;
        var cell = borg?.cell as obj_item_stock_parts_power_store;
        if (borg != null && cell != null && Dm.num(cell.charge) > ACTIVATION_COST)
        {
            if (isturf(borg.loc))
                await toggle(borg);
            else
                to_chat(user, span_warning(I18n.T("You can't use [] while inside something!", this)));
        }
        else
            to_chat(user, span_warning(I18n.T("You need at least [] of charge in your cell to use []!", display_energy(ACTIVATION_COST), this)));
        return null;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:56
    public virtual async DmTask toggle(mob_living_silicon_robot user = null)
    {
        if (Dm.truthy(active))
        {
            playsound(this, Dm.resource("sound/effects/pop.ogg"), 100, TRUE, -6);
            to_chat(user, span_notice(I18n.T("You deactivate the [].", this)));
            deactivate(user);
        }
        else
        {
            if (Dm.truthy(animation_playing))
            {
                to_chat(user, span_notice(I18n.T("the [] is recharging.", this)));
                return;
            }
            animation_playing = TRUE;
            to_chat(user, span_notice(I18n.T("You activate the [].", this)));
            playsound(this, Dm.resource("sound/effects/seedling_chargeup.ogg"), 100, TRUE, -6);
            apply_wibbly_filters(user);
            var cell = (obj_item_stock_parts_power_store)user.cell;
            if (Dm.truthy(await do_after(user, 5 * SECONDS, user, cog_icon: null)) && Dm.truthy(await cell.use(ACTIVATION_COST)))
            {
                playsound(this, Dm.resource("sound/effects/bamf.ogg"), 100, TRUE, -6);
                to_chat(user, span_notice(I18n.T("You are now disguised as the Nanotrasen engineering borg \"[]\".", friendlyName)));
                activate(user);
            }
            else
            {
                to_chat(user, span_warning(I18n.T("The chameleon field fizzles.")));
                do_sparks(3, FALSE, user);
            }
            remove_wibbly_filters(user);
            animation_playing = FALSE;
        }
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:79
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (user != null)
        {
            var cell = user.cell as obj_item_stock_parts_power_store;
            if (cell == null || !Dm.truthy(Waiting.NoSleep(cell.use(ACTIVATION_UP_KEEP * Dm.num(seconds_per_tick)))))
                disrupt(user);
            return null;
        }
        return PROCESS_KILL;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:86
    public virtual void activate(mob_living_silicon_robot user = null)
    {
        START_PROCESSING(SSobj, this);
        this.user = user;
        savedName = user.name;
        user.name = Dm.str(friendlyName);
        user.model.cyborg_base_icon = Dm.str(disguise);
        user.model.name = Dm.str(capitalize(disguise));
        user.bubble_icon = "robot";
        active = TRUE;
        Waiting.NoSleep(user.update_icons());

        if (listeningTo == user)
            return;
        if (listeningTo != null)
            UnregisterSignal(listeningTo, signalCache);
        RegisterSignals(user, signalCache, nameof(disrupt));
        listeningTo = user;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:103
    public virtual void deactivate(mob_living_silicon_robot user = null)
    {
        STOP_PROCESSING(SSobj, this);
        if (listeningTo != null)
        {
            UnregisterSignal(listeningTo, signalCache);
            listeningTo = null;
        }
        do_sparks(5, FALSE, user);
        user.name = Dm.str(savedName);
        user.model.cyborg_base_icon = Dm.initial(user.model, (obj_item_robot_model m) => m.cyborg_base_icon);
        user.model.name = Dm.initial(user.model, (obj_item_robot_model m) => m.name);
        user.bubble_icon = "syndibot";
        active = FALSE;
        Waiting.NoSleep(user.update_icons());
        this.user = user;
    }

    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:118
    public virtual void disrupt(mob_living_silicon_robot user = null)
    {
        if (Dm.truthy(active))
        {
            to_chat(user, span_danger(I18n.T("Your chameleon field deactivates.")));
            deactivate(user);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/antagonists/nukeop/equipment/borgchameleon.dm:19
    internal DmList obj_item_borg_chameleon__signalCache;
}
