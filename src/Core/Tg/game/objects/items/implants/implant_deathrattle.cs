// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_deathrattle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3: the deathrattle implant box). The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implant_deathrattle.dm:9
public partial class datum_deathrattle_group : datum
{
    /// This deathrattle group does not care about the area someone dies in.
    // tg: code/game/objects/items/implants/implant_deathrattle.dm:2
    public const float DEATHRATTLE_AREA_NOLIST = 0;
    /// This deathrattle group uses its `area_list` as a blacklist: if someone dies in these areas, they DO NOT cause an alert.
    // tg: code/game/objects/items/implants/implant_deathrattle.dm:4
    public const float DEATHRATTLE_AREA_BLACKLIST = 1;
    /// This deathrattle group uses its `area_list` as a whitelist: if someone dies in these areas, they DO cause an alert.
    // tg: code/game/objects/items/implants/implant_deathrattle.dm:6
    public const float DEATHRATTLE_AREA_WHITELIST = 2;

    // tg: code/game/objects/items/implants/implant_deathrattle.dm:20
    [DmParams("name")]
    public override async DmTask New(params object[] __extra)
    {
        object name = Dm.arg(__extra, 0);
        if (Dm.truthy(name))
            this.name = name;
        else
            // Give the group a unique name for debugging, and possible future
            // use for making custom linked groups.
            this.name = Dm.text("[][] []", Dm.truthy(prefix) ? Dm.text("[]-", prefix) : "", rand(100, 999), pick(GLOB.phonetic_alphabet));
        await DmTask.CompletedTask;
    }

    /*
     * Proc called by new implant being added to the group. Listens for the
     * implant being implanted, removed and destroyed.
     *
     * If implant is already implanted in a person, then trigger the implantation
     * code.
     */
    // tg: code/game/objects/items/implants/implant_deathrattle.dm:49
    public virtual void register(obj_item_implant_deathrattle implant = null)
    {
        if (Dm.@in(implant, implants))
            return;
        RegisterSignal(implant, COMSIG_IMPLANT_IMPLANTED, nameof(on_implant_implantation));
        RegisterSignal(implant, COMSIG_IMPLANT_REMOVED, nameof(on_implant_removal));
        RegisterSignal(implant, COMSIG_QDELETING, nameof(on_implant_destruction));

        Dm.append(ref implants, implant);
        implant.current_group = this;

        if (implant.imp_in != null)
            on_implant_implantation(implant.imp_in);
    }

    // tg: code/game/objects/items/implants/implant_deathrattle.dm:62
    [SignalHandler]
    public virtual void on_implant_implantation(object implant = null, mob_living target = null, mob user = null, object silent = null, object force = null)
    {
        // a runtime in tg: register() passes the host as the implant, so target is null and the hook lands on nothing
        if (target == null)
            return;
        RegisterSignal(target, COMSIG_MOB_STATCHANGE, nameof(on_user_statchange));
    }

    // tg: code/game/objects/items/implants/implant_deathrattle.dm:67
    [SignalHandler]
    public virtual void on_implant_removal(obj_item_implant implant = null, mob_living source = null, object silent = null, object special = null)
    {
        UnregisterSignal(source, COMSIG_MOB_STATCHANGE);
    }

    // tg: code/game/objects/items/implants/implant_deathrattle.dm:72
    [SignalHandler]
    public virtual void on_implant_destruction(obj_item_implant implant = null)
    {
        Dm.remove(ref implants, implant);
    }

    // tg: code/game/objects/items/implants/implant_deathrattle.dm:77
    [SignalHandler]
    public virtual void on_user_statchange(mob_living owner = null, object new_stat = null)
    {
        if (!Dm.eq(new_stat, DEAD))
            return;

        object name = owner.mind != null ? owner.mind.name : owner.real_name;
        area death_area = get_area(owner) as area;
        // don't alert for a death if it's in in the area list AND we're on blacklist mode
        if (Dm.truthy(is_type_in_list(death_area, area_list)) && area_list_mode == DEATHRATTLE_AREA_BLACKLIST)
            return;
        // don't alert a death if it's not in the area list AND we're on whitelist mode
        if (!Dm.truthy(is_type_in_list(death_area, area_list)) && area_list_mode == DEATHRATTLE_AREA_WHITELIST)
            return;
        object area_name = get_area_name(get_turf(owner));
        // All "hearers" hear the same sound.
        object sound = pick(
            Dm.resource("sound/items/knell/knell1.ogg"),
            Dm.resource("sound/items/knell/knell2.ogg"),
            Dm.resource("sound/items/knell/knell3.ogg"),
            Dm.resource("sound/items/knell/knell4.ogg"));

        foreach (object _implant in Dm.each(implants))
        {
            obj_item_implant_deathrattle implant = (obj_item_implant_deathrattle)_implant;

            // Skip the unfortunate soul, and any unimplanted implants
            if (implant.imp_in == owner || implant.imp_in == null)
                continue;

            // Deliberately the same message framing as ghost deathrattle
            mob_living recipient = implant.imp_in;
            to_chat(recipient, I18n.T("<i>You hear a strange, robotic voice in your head...</i> \"[]\"", span_robot(I18n.T("<b>[]</b> has died at <b>[]</b>.", name, area_name))));
            recipient.playsound_local(get_turf(recipient), sound, vol: 75, vary: FALSE, pressure_affected: FALSE, use_reverb: FALSE);
        }
    }
}

// tg: code/game/objects/items/implants/implant_deathrattle.dm:112
public partial class obj_item_implant_deathrattle : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_deathrattle.dm:134
    public override object can_be_implanted_in(mob_living target = null)
    {
        if (current_group == null)
        {
            balloon_alert(target, I18n.T("deathrattle needs configuration!"));
            return FALSE;
        }
        // Can be implanted in anything that's a mob. Syndicate cyborgs, talking fish, humans...
        return TRUE;
    }
}
