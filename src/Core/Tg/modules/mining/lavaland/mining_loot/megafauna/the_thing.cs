// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:1
public partial class datum_action_innate_brain_undeployment : datum_action_innate
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:7
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags, __extra)))
            return FALSE;
        obj_item_organ_brain_cybernetic_ai shell_to_disconnect = (owner as mob_living)?.get_organ_by_type(typeof(obj_item_organ_brain_cybernetic_ai)) as obj_item_organ_brain_cybernetic_ai;

        shell_to_disconnect.undeploy();
        return TRUE;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:15
public partial class obj_item_organ_brain_cybernetic_ai : obj_item_organ_brain_cybernetic
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Their eyes move with machine precision, their expression completely blank.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:32
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        undeploy();
        mainframe = null;
        QDEL_NULL(ref undeployment_action);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:38
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon brain_owner = organ_owner;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        brain_owner.add_traits(Dm.list(TRAIT_BASIC_HEALTH_HUD_VISIBLE, TRAIT_NO_MINDSWAP, TRAIT_CORPSELOCKED), REF(this));
        update_med_hud_status(brain_owner);
        RegisterSignal(brain_owner, COMSIG_LIVING_HEALTH_UPDATE, nameof(update_med_hud_status));
        RegisterSignal(brain_owner, COMSIG_CLICK, nameof(owner_clicked));
        RegisterSignal(brain_owner, COMSIG_MOB_GET_STATUS_TAB_ITEMS, nameof(get_status_tab_item));
        RegisterSignal(brain_owner, COMSIG_MOB_MIND_BEFORE_MIDROUND_ROLL, nameof(cancel_rolls));
        RegisterSignals(brain_owner, Dm.list(COMSIG_QDELETING, COMSIG_LIVING_PRE_WABBAJACKED), nameof(undeploy));
        RegisterSignal(brain_owner, COMSIG_CARBON_GAIN_ORGAN, nameof(on_organ_gain));

        obj_item_implant_radio radio = Dm.@new<obj_item_implant_radio>(owner);
        await radio.implant(owner, null, TRUE, TRUE);
        radio_weakref = WEAKREF(radio) as datum_weakref;
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:53
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        undeploy();
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon carbon_owner = (mob_living_carbon)organ_owner;
        carbon_owner.remove_traits(Dm.list(TRAIT_BASIC_HEALTH_HUD_VISIBLE, TRAIT_NO_MINDSWAP, TRAIT_CORPSELOCKED), REF(this));
        UnregisterSignal(carbon_owner, Dm.list(COMSIG_LIVING_HEALTH_UPDATE, COMSIG_CLICK, COMSIG_MOB_GET_STATUS_TAB_ITEMS, COMSIG_MOB_MIND_BEFORE_MIDROUND_ROLL, COMSIG_QDELETING, COMSIG_LIVING_PRE_WABBAJACKED));

        obj_item_implant_radio radio = radio_weakref.resolve() as obj_item_implant_radio;
        if (radio != null)
            QDEL_NULL(ref radio);
        connected_ai = null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:64
    [SignalHandler]
    public virtual object cancel_rolls(mob_living source = null, datum_mind mind = null, object antag_flag = null)
    {
        return Dm.eq(antag_flag, ROLE_MALF) ? NONE : CANCEL_ROLL;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:68
    [SignalHandler]
    public virtual void get_status_tab_item(mob_living source = null, DmList items = null)
    {
        if (mainframe == null)
            return;
        Dm.append(ref items, mainframe.get_status_tab_items());
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:74
    [SignalHandler]
    public virtual void update_med_hud_status(mob_living mob_parent = null)
    {
        image holder = mob_parent.hud_list?[STATUS_HUD] as image;
        if (holder == null)
            return;
        icon size_check = Dm.icon(mob_parent.icon, mob_parent.icon_state, mob_parent.dir);
        holder.pixel_y = size_check.Height() - ICON_SIZE_Y;
        if (IS_DEAD_OR_FAKING(mob_parent) || mainframe == null)
        {
            holder.icon_state = "huddead2";
            holder.pixel_x = -8; // new icon states? nuh uh
        }
        else
        {
            holder.icon_state = "hudtrackingai";
            holder.pixel_x = -16;
        }
    }

    // no thoughts only wifi
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:88
    public override object can_gain_trauma(object trauma = null, object resilience = null, float? natural_gain = null)
    {
        return FALSE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:91
    [SignalHandler]
    public virtual void owner_clicked(datum source = null, atom location = null, object control = null, object @params = null, mob user = null)
    {
        if (!isAI(user))
            return;
        DmList lines = Dm.list();
        Dm.append(ref lines, span_bold(Dm.text("[]", owner)));
        Dm.append(ref lines, !HAS_TRAIT(owner, TRAIT_INCAPACITATED) ? I18n.T("Target is currently functional") : I18n.T("Target is currently incapacitated"));
        Dm.append(ref lines, I18n.T("Estimated organic/inorganic integrity: []", owner.health));
        if (mainframe != null)
            Dm.append(ref lines, span_warning(I18n.T("Already occupied by another digital entity.")));
        else if (connected_ai != null && connected_ai != user)
            Dm.append(ref lines, span_warning(I18n.T("Uplink is locked by another digital entity.")));
        else if (!Dm.truthy(is_sufficiently_augmented()))
            Dm.append(ref lines, span_warning(I18n.T("Organic organs detected. Robotic organs only, cannot take over.")));
        else
            Dm.append(ref lines, Dm.text("<a href='byond://?src=[];ai_take_control=[]'>[]</a><br>", REF(this), REF(user), span_boldnotice(I18n.T("Take control?"))));

        to_chat(user, boxed_message(Dm.jointext(lines, "\n")), type: MESSAGE_TYPE_INFO);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:110
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (!Dm.truthy(href_list["ai_take_control"]) || !Dm.truthy(is_sufficiently_augmented()) || mainframe != null)
            return null;
        mob_living_silicon_ai AI = Dm.locate_ref(href_list["ai_take_control"]) is mob_living_silicon_ai found && Dm.@in(found, GLOB.silicon_mobs) ? found : null; // locate(href_list["ai_take_control"]) in GLOB.silicon_mobs
        if (AI == null)
            return null;
        if (AI.controlled_equipment != null)
        {
            to_chat(AI, span_warning(I18n.T("You are already loaded into an onboard computer!")));
            return null;
        }
        if (!SScameras.is_visible_by_cameras(owner))
        {
            to_chat(AI, span_warning(I18n.T("Target is no longer near active cameras.")));
            return null;
        }
        if (!isturf(AI.loc))
        {
            to_chat(AI, span_warning(I18n.T("You aren't in your core!")));
            return null;
        }

        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(undeploy));
        // DEVIATION(DV624): AI.deployed_shell = owner is left out: the port types the AI's shell as a cyborg, so the human shell is not stored there
        await deploy_init(AI);
        ADD_TRAIT(AI.mind, TRAIT_UNCONVERTABLE, REF(this));
        ADD_TRAIT(AI, TRAIT_MIND_TEMPORARILY_GONE, REF(this));
        await AI.mind.transfer_to(owner);
        to_chat(owner, span_boldbig(I18n.T("You are still considered a silicon/cyborg/AI. Follow your laws.")));
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:135
    public virtual async DmTask deploy_init(mob_living_silicon_ai AI = null)
    {
        //todo camera maybe
        mainframe = AI;
        connected_ai = AI;
        RegisterSignal(AI, COMSIG_QDELETING, nameof(ai_deleted));
        await undeployment_action.Grant(owner);
        update_med_hud_status(owner);

        owner.add_traits(Dm.list(TRAIT_SILICON_ACCESS, TRAIT_LOUD_BINARY), REF(this));

        obj_item_implant_radio implant = radio_weakref.resolve() as obj_item_implant_radio;
        if (implant.radio != null && AI.radio != null)
        {
            if (Dm.truthy(Dm.bitand((AI.radio as obj_item_radio).special_channels, RADIO_SPECIAL_SYNDIE)))
                implant.radio.make_syndie();
            implant.radio.subspace_transmission = TRUE;
            implant.radio.command = TRUE;
            implant.radio.channels = (AI.radio as obj_item_radio).channels;
            foreach (object channel in Dm.each(implant.radio.channels))
                LAZYSET(ref implant.radio.secure_radio_connections, channel, add_radio(implant.radio, GLOB.default_radio_channels[channel]));
        }
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:156
    [SignalHandler]
    public virtual void undeploy(datum source = null)
    {
        if (owner?.mind == null || mainframe == null)
            return;
        UnregisterSignal(owner, Dm.list(COMSIG_LIVING_DEATH, COMSIG_QDELETING));
        UnregisterSignal(mainframe, COMSIG_QDELETING);
        Waiting.NoSleep(mainframe.redeploy_action.Remove(mainframe));
        mainframe.redeploy_action.last_used_shell = null;
        Waiting.NoSleep(owner.mind.transfer_to(mainframe));
        // DEVIATION(DV624): mainframe.deployed_shell = null is left out, as it was never set to the human shell
        Waiting.NoSleep(undeployment_action.Remove(owner));
        if (mainframe.laws != null)
            mainframe.laws.show_laws(mainframe);
        if (mainframe.eyeobj != null)
            mainframe.eyeobj.setLoc(loc);
        REMOVE_TRAIT(mainframe.mind, TRAIT_UNCONVERTABLE, REF(this));
        REMOVE_TRAIT(mainframe, TRAIT_MIND_TEMPORARILY_GONE, REF(this));
        owner.remove_traits(Dm.list(TRAIT_SILICON_ACCESS, TRAIT_LOUD_BINARY), REF(this)); // we don't want randoms using our body as free AA, so we only have it when we active.

        obj_item_implant_radio implant = radio_weakref.resolve() as obj_item_implant_radio;
        if (implant != null)
            implant.radio.resetChannels();

        mainframe = null;
        update_med_hud_status(owner);
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:182
    public virtual object is_sufficiently_augmented()
    {
        object dot = TRUE;
        if (owner is not mob_living_carbon carb_owner)
            return dot;
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(carb_owner.organs))
        {
            if (Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_EXTERNAL)))
                continue;
            if (!Dm.truthy(IS_ROBOTIC_ORGAN(organ)) && !istype(organ, typeof(obj_item_organ_tongue))) //tongues are not in the exosuit fab and nobody is going to bother to find them so
                return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:193
    [SignalHandler]
    public virtual void on_organ_gain(datum source = null, obj_item_organ new_organ = null, object special = null)
    {
        if (!Dm.truthy(is_sufficiently_augmented()))
        {
            to_chat(owner, span_danger(I18n.T("Connection failure. Organics detected.")));
            undeploy();
        }
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/the_thing.dm:199
    [SignalHandler]
    public virtual void ai_deleted(datum source = null)
    {
        to_chat(owner, span_danger(I18n.T("Your core has been rendered inoperable...")));
        undeploy();
    }
}
