// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/petrified_statue.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. A petrified mob (the genetic instability's stone meltdown, the flesh to stone touch, the stone
// smite, the rift fish) is held in a marble statue drawn as the mob's own picture in grey (its overlays copied, tg's colour matrix):
// godmode, mute and bloodless, stunned while it lasts (eight minutes, or the caller's time), its integrity the mob's health plus a
// hundred; when the statue is broken the mob is dusted (a carbon's brain tumbles out, petrified and grey), when it lapses or the
// statue is taken apart the mob comes out paralyzed ten seconds with the statue's damage. A corgi becomes the corgi statue. The
// staff of animation turns it into a living statue that keeps its mind. The var block is generated (D111).

// tg: code/game/objects/structures/petrified_statue.dm:1
public partial class obj_structure_statue_petrified : obj_structure_statue
{
    // tg: code/game/objects/structures/petrified_statue.dm:15
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/petrified_statue.dm:18
    [DmParams("mapload", "living", "statue_timer", "save_brain")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living living = Dm.arg<mob_living>(__extra, 0);
        object statue_timer = __extra.Length > 1 ? __extra[1] : null;
        object save_brain = __extra.Length > 2 ? __extra[2] : null;
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(statue_timer))
            timer = Dm.num(statue_timer);
        if (Dm.truthy(save_brain))
            brain = Dm.num(save_brain);
        if (living == null)
            return dot;
        petrified_mob = living;
        if (living.buckled != null)
            Waiting.NoSleep(living.buckled.unbuckle_mob(living, force: TRUE));
        living.visible_message(span_warning(I18n.T("[]'s skin rapidly turns to marble!", living)), span_userdanger(I18n.T("Your body freezes up! Can't... move... can't... think...")));
        Waiting.NoSleep(living.forceMove(this));
        living.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_MUTE, TRAIT_NOBLOOD), STATUE_MUTE);
        living.add_faction(FACTION_MIMIC); //Stops mimics from instaqdeling people in statues
        atom_integrity = Dm.num(living.health) + 100; //stoning damaged mobs will result in easier to shatter statues
        max_integrity = Dm.num(atom_integrity);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/structures/petrified_statue.dm:37
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (petrified_mob == null)
            STOP_PROCESSING(SSobj, this);
        timer -= Dm.num(seconds_per_tick) * SECONDS;
        // tg calls Stun on a null mob here too (a runtime that ends the tick's call); the port skips it and goes on to the timer
        if (petrified_mob != null)
            Waiting.NoSleep(petrified_mob.Stun(4 * SECONDS)); //So they can't do anything while petrified
        if (Dm.num(timer) <= 0)
        {
            STOP_PROCESSING(SSobj, this);
            qdel(this);
        }
        return null;
    }

    // tg: code/game/objects/structures/petrified_statue.dm:46
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/petrified_statue.dm:49
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == petrified_mob)
        {
            petrified_mob.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_MUTE, TRAIT_NOBLOOD), STATUE_MUTE);
            await petrified_mob.Paralyze(10 * SECONDS);
            await petrified_mob.take_overall_damage((Dm.num(petrified_mob.health) - Dm.num(atom_integrity) + 100)); //any new damage the statue incurred is transferred to the mob
            petrified_mob.remove_faction(FACTION_MIMIC);
            petrified_mob = null;
        }
        return dot;
    }

    // tg: code/game/objects/structures/petrified_statue.dm:58
    public override object Destroy(float? force = null)
    {
        turf dropoff_turf = drop_location() as turf;
        if (loc is mob_living_basic_statue statue_mob)
        {
            Waiting.NoSleep(forceMove(dropoff_turf));
            if (statue_mob.mind != null)
            {
                if (petrified_mob != null)
                {
                    Waiting.NoSleep(statue_mob.mind.transfer_to(petrified_mob));
                    to_chat(petrified_mob, span_notice(I18n.T("You slowly come back to your senses. You are in control of yourself again!")));
                }
            }
            qdel(statue_mob);
        }

        foreach (obj statue_contents in Dm.each<obj>(contents.Copy()))
            Waiting.NoSleep(statue_contents.forceMove(dropoff_turf));

        if (petrified_mob != null)
            Waiting.NoSleep(petrified_mob.forceMove(dropoff_turf));
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/petrified_statue.dm:75
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        string destruction_message = I18n.T("[] shatters!", this);
        if (!Dm.truthy(disassembled))
        {
            if (petrified_mob != null)
            {
                petrified_mob.investigate_log("has been dusted by statue deconstruction.", INVESTIGATE_DEATHS);
                if (iscarbon(petrified_mob) && Dm.truthy(brain))
                {
                    mob_living_carbon petrified_carbon = (mob_living_carbon)petrified_mob;
                    obj_item_organ_brain carbon_brain = petrified_carbon.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
                    await carbon_brain.Remove(petrified_carbon);
                    await carbon_brain.forceMove(get_turf(this));
                    carbon_brain.name = I18n.T("petrified []", carbon_brain.name);
                    carbon_brain.desc = I18n.T("[] This one seems a bit more... smooth than a normal brain. Probably'd still work.", carbon_brain.desc);
                    await carbon_brain.add_atom_colour(Dm.list(rgb(77, 77, 77), rgb(150, 150, 150), rgb(28, 28, 28), rgb(0, 0, 0)), FIXED_COLOUR_PRIORITY);
                    destruction_message = I18n.T("[] shatters, a solid brain tumbling out!", this);
                }
                await petrified_mob.dust();
            }
        }
        visible_message(span_danger(destruction_message));
    }

    // tg: code/game/objects/structures/petrified_statue.dm:92
    public override async DmTask<object> animate_atom_living(mob_living owner = null)
    {
        if (petrified_mob == null)
            return await base.animate_atom_living(owner);
        mob_living_basic_statue new_statue = Dm.@new<mob_living_basic_statue>(drop_location());
        new_statue.name = I18n.T("statue of []", petrified_mob.name);
        if (owner != null)
            await new_statue.befriend(owner);
        new_statue.icon = Dm.resource("icons/blanks/32x32.dmi");
        new_statue.icon_state = "nothing";
        new_statue.appearance_flags = Dm.num(Dm.bitor(new_statue.appearance_flags, KEEP_TOGETHER));
        new_statue.copy_overlays(this, cut_old: TRUE);
        new_statue.atom_colours = atom_colours.Copy();
        await new_statue.update_atom_colour();
        if (petrified_mob.mind != null)
            await petrified_mob.mind.transfer_to(new_statue);
        to_chat(new_statue, span_userdanger(I18n.T("You are an animate statue. You cannot move when monitored, but are nearly invincible and deadly when unobserved! [].", owner != null ? I18n.T("Do not harm [], your creator", owner) : "")));
        await forceMove(new_statue);
        return new_statue;
    }
}

// tg: code/game/objects/structures/petrified_statue.dm:110
public partial class mob : atom_movable
{
    // tg: code/game/objects/structures/petrified_statue.dm:110
    public virtual async DmTask<object> petrify(object statue_timer = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/objects/structures/petrified_statue.dm:113
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/game/objects/structures/petrified_statue.dm:113
    [DmParams("statue_timer", "save_brain", "colorlist")]
    public override async DmTask<object> petrify(object statue_timer = null, params object[] __extra)
    {
        object save_brain = __extra.Length > 0 ? __extra[0] : null;
        object colorlist = __extra.Length > 1 ? __extra[1] : null;
        if (!isturf(loc))
            return FALSE;
        obj_structure_statue_petrified new_statue = Dm.@new<obj_structure_statue_petrified>(loc, this, statue_timer, save_brain);
        new_statue.name = I18n.T("statue of []", name);
        new_statue.icon = Dm.resource("icons/blanks/32x32.dmi");
        new_statue.icon_state = "nothing";
        new_statue.appearance_flags = Dm.num(Dm.bitor(new_statue.appearance_flags, KEEP_TOGETHER));
        new_statue.copy_overlays(this, cut_old: TRUE);
        await new_statue.add_atom_colour(Dm.truthy(colorlist) ? colorlist : Dm.list(rgb(77, 77, 77), rgb(150, 150, 150), rgb(28, 28, 28), rgb(0, 0, 0)), FIXED_COLOUR_PRIORITY);
        return TRUE;
    }
}

// tg: code/game/objects/structures/petrified_statue.dm:125
public partial class mob_living_basic_pet_dog_corgi : mob_living_basic_pet_dog
{
    // tg: code/game/objects/structures/petrified_statue.dm:125
    public override async DmTask<object> petrify(object statue_timer = null, params object[] __extra)
    {
        if (!isturf(loc))
            return FALSE;
        obj_structure_statue_petrified new_statue = Dm.@new<obj_structure_statue_petrified>(loc, this, statue_timer);
        new_statue.name = I18n.T("statue of a corgi");
        new_statue.icon_state = "corgi";
        new_statue.desc = I18n.T("If it takes forever, I will wait for you...");
        await DmTask.CompletedTask;
        return TRUE;
    }
}
