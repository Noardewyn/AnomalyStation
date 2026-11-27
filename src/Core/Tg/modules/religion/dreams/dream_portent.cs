// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/dream_portent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's portent rite (the chaplain falls asleep and dreams a portent of the round's state), its moods, the
// specific portent dream and the vague portent the sect's sleepers may dream. The var blocks are generated (D111).

// tg: code/modules/religion/dreams/dream_portent.dm:1
public partial class datum_religion_rites_dream_portent : datum_religion_rites
{
    // tg: code/modules/religion/dreams/dream_portent.dm:10
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ritual_invocations = Dm.list(
            I18n.T("O great shepherd [], grant me a vision of the future!..", GLOB.deity),
            I18n.T("That our flock may persevere through the trials to come..."));
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:17
    public override object can_afford(mob_living user = null)
    {
        if (!Dm.truthy(base.can_afford(user)))
            return FALSE;
        if (!iscarbon(user))
        {
            to_chat(user, span_warning(I18n.T("You are not the sort of creature that can receive a portent.")));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:25
    public override async DmTask<object> invoke_effect(mob_living user = null, atom religious_tool = null)
    {
        if (!Dm.truthy(await user.SetSleeping(10 * SECONDS)))
        {
            to_chat(user, span_warning(I18n.T("You fail to fall asleep.")));
            return FALSE;
        }

        user.visible_message(span_notice(I18n.T("[] suddenly falls into a deep slumber, [] eyes fluttering...", user, user.p_their())));
        await user.adjust_drowsiness(30 * SECONDS);
        return await base.invoke_effect(user, religious_tool);
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:34
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        RegisterSignal(user, COMSIG_PRE_DREAMING, nameof(add_portent));
        RegisterSignal(user, COMSIG_START_DREAMING, nameof(check_portent));
        RegisterSignal(user, COMSIG_END_DREAMING, nameof(end_portent));
        RegisterSignal(user, COMSIG_MOB_APPLY_DAMAGE, nameof(interrupt_portent));
        _addtimer(Dm.@new<datum_callback>(this, nameof(force_dream), user), rand(2, 5) * SECONDS, TIMER_DELETE_ME, file: "code/modules/religion/dreams/dream_portent.dm", line: 40); // force the dream to start immediately
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:42
    public virtual void force_dream(mob_living dreamer = null)
    {
        if (!iscarbon(dreamer) || HAS_TRAIT(dreamer, TRAIT_DREAMING))
            return; // dreamed naturally already
        ((mob_living_carbon)dreamer).dream();
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:47
    [SignalHandler]
    public virtual void add_portent(mob_living dreamer = null, DmList dream_pool = null)
    {
        // removes any pre-existing vague portents in the dream pool so we can give the real deal
        foreach (datum_dream_random_vague_portent existing in Dm.each<datum_dream_random_vague_portent>(dream_pool))
            Dm.remove(ref dream_pool, existing);

        dream_pool[Dm.@new<datum_dream_specific_portent>()] = 2000f;
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:56
    [SignalHandler]
    public virtual void check_portent(mob_living dreamer = null, datum_dream current_dream = null)
    {
        if (current_dream is datum_dream_specific_portent)
            return;

        to_chat(dreamer, span_cyan(I18n.T("Your mind wanders, yet you receive no clear vision... You must try again later.")));
        refund(0.8f);
        Waiting.NoSleep(dreamer.adjust_drowsiness(10 * SECONDS));
        Waiting.NoSleep(dreamer.add_mood_event("dream_failed", typeof(datum_mood_event_dream_failed)));
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:67
    [SignalHandler]
    public virtual void interrupt_portent(mob_living dreamer = null, object damage_amount = null, params object[] __extra)
    {
        if (!prob(Dm.num(damage_amount) * 10)) // higher damage = higher chance to interrupt
            return;

        to_chat(dreamer, span_warning(I18n.T("Your dream is interrupted as you are harmed!")));
        Waiting.NoSleep(dreamer.SetSleeping(0));
        Waiting.NoSleep(dreamer.adjust_drowsiness(10 * SECONDS));
        Waiting.NoSleep(dreamer.add_mood_event("dream_interrupted", typeof(datum_mood_event_dream_interrupted)));
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:78
    [SignalHandler]
    public virtual void end_portent(mob_living dreamer = null, datum_dream current_dream = null)
    {
        qdel(this);
    }
}

// tg: code/modules/religion/dreams/dream_portent.dm:93
public partial class datum_dream_specific_portent : datum_dream
{
    // tg: code/modules/religion/dreams/dream_portent.dm:97
    public override object GenerateDream(mob_living_carbon dreamer = null)
    {
        DmList dot = Dm.list();
        Dm.append(ref dot, span_cyan(I18n.T("a portent of the future")));

        DmList portent_types = Dm.list(
            Dm.kv(I18n.T("[] greets you warmly", GLOB.deity), I18n.T("[] bids you farewell, though you feel their presence watch over you", GLOB.deity)),
            Dm.kv(I18n.T("a crystal ball reveals a vision of the future"), I18n.T("ultimately, the crystal ball returns to its normal, opaque state")),
            Dm.kv(I18n.T("a divine light blinds you, revealing glimpses of what is to come"), I18n.T("finally, the light fades, leaving you with a lingering warmth")),
            Dm.kv(I18n.T("a full moon illuminates the sky"), I18n.T("the moon crosses the horizon, bringing forth a new dawn")),
            Dm.kv(I18n.T("a mysterious figure appears, cloaked in shadow"), I18n.T("they depart, leaving you with a sense of []", pick(I18n.T("wonder"), I18n.T("dread"), I18n.T("curiosity"), I18n.T("foreboding")))),
            Dm.kv(I18n.T("an incomprehensible entity envelops you, showing you visions of the past, present, and future"), I18n.T("the entity releases you, leaving you with a sense of awe and fear")),
            Dm.kv(I18n.T("an old [] approaches you, offering cryptic advice", pick(I18n.T("man"), I18n.T("woman"), I18n.T("prophet"), I18n.T("oracle"))), I18n.T("they vanish before you can ask any questions")),
            Dm.kv(I18n.T("the stars align in a way you've never seen before"), I18n.T("finally, the stars return to their normal constellations")),
            Dm.kv(I18n.T("the trees ahead parts to reveal a hidden path"), I18n.T("ultimately, you lose the path as the trees sway back into place")),
            Dm.kv(I18n.T("walking through a featureless landscape, shapes begin to form"), I18n.T("finally, the shapes fade away, leaving you alone in the void")),
            Dm.kv(I18n.T("you see yourself sleeping peacefully"), I18n.T("finally, you see yourself waking up calmly")),
            Dm.kv(I18n.T("your third eye opens to reveal a hidden truth"), I18n.T("finally, your third eye closes, but the vision lingers in your mind")));
        object picked_portent = pick(portent_types);

        Dm.append(ref dot, span_cyan(Dm.str(picked_portent)));
        foreach (object part in Dm.each(get_portent(dreamer)))
            Dm.append(ref dot, span_cyan(Dm.str(part)));
        Dm.append(ref dot, span_cyan(Dm.str(portent_types[picked_portent])));
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_portent.dm:122
    public virtual object get_portent(mob_living_carbon dreamer = null)
    {
        if (prob(1))
        {
            GLOB.religious_sect.adjust_favor(25, dreamer);
            return pick(Dm.list(
                Dm.list(I18n.T("reply hazy"), I18n.T("try again later")),
                Dm.list(I18n.T("ask again later")),
                Dm.list(I18n.T("better not tell you now")),
                Dm.list(I18n.T("cannot predict now")),
                Dm.list(I18n.T("concentrate and ask again"))));
        }

        foreach (datum_antagonist_nightmare nightmare in Dm.each<datum_antagonist_nightmare>(GLOB.antagonists))
        {
            if (nightmare.owner?.current != null && Dm.eq(nightmare.owner.current.stat, STABLE))
            {
                return pick(Dm.list(
                    Dm.list(I18n.T("you have a terrible nightmare"), I18n.T("filled with indescribable horrors"), I18n.T("leaving you with a lingering sense of dread")),
                    Dm.list(I18n.T("you have a terrible nightmare"), I18n.T("filled with visions of your own death"), I18n.T("leaving you with a lingering sense of doom")),
                    Dm.list(I18n.T("you have a terrible nightmare"), I18n.T("filled with horrible memories of your past"), I18n.T("leaving you with a lingering sense of sadness")),
                    Dm.list(I18n.T("you have a terrible nightmare"), I18n.T("filled with the influence of an uncertain presence"), I18n.T("leaving you with a lingering sense of anxiety")),
                    Dm.list(I18n.T("you have a terrible nightmare"), I18n.T("filled with stabbing pain and suffocating darkness"), I18n.T("leaving you with a lingering sense of panic"))));
            }
        }

        foreach (datum_team_cult cult in Dm.each<datum_team_cult>(GLOB.antagonist_teams))
        {
            if (Dm.truthy(cult.cult_ascendent))
                return Dm.list(I18n.T("the Blood Geometer, Nar'sie, invades your dream"), I18n.T("her pressence overwhelming and suffocating"), I18n.T("she eyes you greedily"));
        }

        foreach (datum_antagonist_heretic heretic in Dm.each<datum_antagonist_heretic>(GLOB.antagonists))
        {
            if (!Dm.truthy(heretic.ascended) || heretic.owner?.current == null)
                continue;
            if (IS_UNCONSCIOUS_OR_CRIT(heretic.owner.current as mob_living))
            {
                return Dm.list(
                    I18n.T("the doors of the Mansus loom ahead of you"),
                    I18n.T("intricately decorated - but cracked, broken, and sealed shut"),
                    I18n.T("a great [] locked away for good", IS_HERETIC(dreamer) ? I18n.T("force") : I18n.T("evil")));
            }

            DmList heretic_text = Dm.list(I18n.T("the doors of the Mansus loom ahead of you"), I18n.T("intricately decorated and ajar"), I18n.T("you look through the crack"));
            object route = prob(75) ? heretic.heretic_path.route : null;
            if (Dm.eq(route, PATH_ASH))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you see a barren wasteland"));
                Dm.append(ref heretic_text, I18n.T("all life long gone, scorched to ash and dust"));
                Dm.append(ref heretic_text, I18n.T("you can hardly breathe through the smog"));
            }
            else if (Dm.eq(route, PATH_FLESH))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you see a vast sea of blood"));
                Dm.append(ref heretic_text, I18n.T("the screams of the drowning fill the air"));
                Dm.append(ref heretic_text, I18n.T("the blood laps at your feet"));
            }
            else if (Dm.eq(route, PATH_VOID))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, a vast emptiness stretches out in all directions"));
                Dm.append(ref heretic_text, I18n.T("the silence is deafening"));
                Dm.append(ref heretic_text, I18n.T("you know you are not alone"));
            }
            else if (Dm.eq(route, PATH_COSMIC))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you see the birth and death of stars, galaxies colliding in a cosmic dance"));
                Dm.append(ref heretic_text, I18n.T("the beauty of it all is overwhelming"));
                Dm.append(ref heretic_text, I18n.T("you feel insignificant"));
            }
            else if (Dm.eq(route, PATH_BLADE))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you see a great battle unfolding"));
                Dm.append(ref heretic_text, I18n.T("countless warriors grappling in an endless war"));
                Dm.append(ref heretic_text, I18n.T("the sound of clashing steel and cries of the fallen fill the air"));
            }
            else if (Dm.eq(route, PATH_LOCK))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you see an endless labyrinth"));
                Dm.append(ref heretic_text, I18n.T("the walls shifting and changing as you navigate it"));
                Dm.append(ref heretic_text, I18n.T("no matter which turn you take, you cannot find an exit"));
            }
            else if (Dm.eq(route, PATH_MOON))
            {
                Dm.append(ref heretic_text, I18n.T("beyond it, you bear witness to a grand carnival"));
                Dm.append(ref heretic_text, I18n.T("filled with strange sights and smells, but endless joy and laughter"));
                Dm.append(ref heretic_text, I18n.T("you can't shake the feeling something is wrong"));
            }
            else
            {
                Dm.append(ref heretic_text, I18n.T("what lies beyond cannot be comprehended"));
                Dm.append(ref heretic_text, I18n.T("the sheer magnitude of it overwhelms you"));
                Dm.append(ref heretic_text, I18n.T("you feel a strange mix of awe and terror"));
            }

            return heretic_text;
        }

        foreach (datum_antagonist_wizard wizard in Dm.each<datum_antagonist_wizard>(GLOB.antagonists))
        {
            if (wizard.owner?.current == null || IS_UNCONSCIOUS_OR_CRIT(wizard.owner.current as mob_living))
                return null;
            if (Dm.num(wizard.ritual?.times_completed) < GRAND_RITUAL_RUNES_WARNING_POTENCY)
            {
                if (prob(1))
                {
                    return Dm.list(
                        I18n.T("a garish fool puts on a show"),
                        I18n.T("many lament their antics, but some are amused"),
                        I18n.T("they seem to have no sense of right or wrong"),
                        I18n.T("what an asshole"));
                }
                return null;
            }

            if (wizard.ritual.times_completed == GRAND_RITUAL_FINALE_COUNT)
            {
                return Dm.list(
                    I18n.T("the magician appears once more"),
                    I18n.T("they ignore you, bowing to an unseen audience"),
                    I18n.T("you hear a crowd cheering and applause"),
                    I18n.T("a bright light envelops you, blinding you"),
                    I18n.T("when your vision returns, the magician is gone"));
            }

            if (wizard.ritual.times_completed == GRAND_RITUAL_IMMINENT_FINALE_POTENCY)
            {
                return Dm.list(
                    I18n.T("the magician greets you once more, grinning"),
                    I18n.T("they tell you the grand finale is near"),
                    I18n.T("you can feel the air around you crackling with magical energy"),
                    I18n.T("they magician winks, promising an unforgettable show"),
                    I18n.T("before you can question, they vanish once again"));
            }

            return Dm.list(
                I18n.T("a robed figure manifests in your dream"),
                I18n.T("they introduce themselves as the magician"),
                I18n.T("a demonstration of their powers leaves you in awe"),
                I18n.T("they depart as suddenly as they arrived"));
        }

        foreach (obj_machinery_nuclearbomb bomb in Dm.each_cast<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            if (Dm.truthy(bomb.timing))
            {
                return pick(Dm.list(
                    Dm.list(I18n.T("you see"), I18n.T("a supernova"), I18n.T("bright and blinding"), I18n.T("consuming everything in an instant")),
                    Dm.list(I18n.T("you see"), I18n.T("a mushroom cloud on the horizon"), I18n.T("a sign of devastation and ruin")),
                    Dm.list(I18n.T("you see"), I18n.T("a ticking clock"), I18n.T("counting down to an inevitable disaster")),
                    Dm.list(I18n.T("you see"), I18n.T("a looming tower atop a rocky mountain"), I18n.T("lightning strikes, bringing it down"), I18n.T("this reads as active calamity"))));
            }
        }

        foreach (obj_item_disk_nuclear nuke_disk in Dm.each_cast<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks))
        {
            area disk_loc = get_area(nuke_disk) as area;
            if (disk_loc is not area_station && disk_loc is not area_space)
            {
                return pick(Dm.list(
                    Dm.list(I18n.T("you see"), I18n.T("a dying star"), I18n.T("slowly dimming"), I18n.T("on the verge of collapse")),
                    Dm.list(I18n.T("you see"), I18n.T("a fool, dancing aimlessly"), I18n.T("they hold a ticking bomb"), I18n.T("a sign of recklessness")),
                    Dm.list(I18n.T("you see"), I18n.T("an inverted man"), I18n.T("swaying gently by one ankle"), I18n.T("this reads as suspense")),
                    Dm.list(I18n.T("you see"), I18n.T("a looming tower atop a rocky mountain"), I18n.T("it rains heavily, there is distant thunder"), I18n.T("this reads as imminent calamity"))));
            }
        }

        foreach (mob_living_carbon_human clone in Dm.each_cast<mob_living_carbon_human>(GLOB.human_list))
        {
            if (clone != dreamer && Dm.eq(clone.real_name, dreamer.real_name) && !IS_UNCONSCIOUS_OR_CRIT(clone) && prob(50))
            {
                return Dm.list(
                    I18n.T("you see yourself"),
                    I18n.T("in a foggy mirror"),
                    I18n.T("the reflection is warped and distorted"),
                    I18n.T("but unquestionably you"));
            }
        }

        if (prob(Dm.length(dreamer.get_all_orbiters()) * 20))
        {
            return Dm.list(
                I18n.T("you feel a ghostly []", pick(I18n.T("presence"), I18n.T("entity"), I18n.T("figure"))),
                I18n.T("it seems to be trying to communicate with you"),
                I18n.T("yet you can't comprehend its message"),
                I18n.T("a sense of sadness and longing washes over you"));
        }

        if (IS_HERETIC(dreamer) && prob(50))
        {
            datum_antagonist_heretic heretic = GET_HERETIC(dreamer);
            if (prob(75) && !Dm.truthy(heretic.feast_of_owls))
            {
                object route = heretic.heretic_path?.route;
                if (Dm.eq(route, PATH_START))
                    return Dm.list(I18n.T("you see"), I18n.T("a fork in the road ahead"), I18n.T("the path before you uncertain and full of potential"));
                if (Dm.eq(route, PATH_ASH))
                    return Dm.list(I18n.T("you see"), I18n.T("a barren wasteland ahead"), I18n.T("burned trees line the horizon"), I18n.T("the air thick with smoke and ash"), I18n.T("a bleak and desolate sight"));
                if (Dm.eq(route, PATH_FLESH))
                    return Dm.list(I18n.T("you see"), I18n.T("a legion of amalgamations ahead"), I18n.T("twisted and grotesque"), I18n.T("marching in unison towards an unknown destination"));
                if (Dm.eq(route, PATH_VOID))
                    return Dm.list(I18n.T("you see"), I18n.T("nothing"), I18n.T("a void that seems to swallow all light and hope"), I18n.T("the silence deafening and oppressive"));
                if (Dm.eq(route, PATH_COSMIC))
                    return Dm.list(I18n.T("you see"), I18n.T("the birth of a new star"), I18n.T("radiant and reaching out"), I18n.T("you are not alone"));
                if (Dm.eq(route, PATH_BLADE))
                    return Dm.list(I18n.T("you see"), I18n.T("a towering fortress ahead"), I18n.T("its walls lined with stalwart defenders"), I18n.T("each and every one bowing in respect to you"));
                if (Dm.eq(route, PATH_LOCK))
                    return Dm.list(I18n.T("you see"), I18n.T("and endless labyrinth"), I18n.T("the walls shifting and changing as you navigate it"), I18n.T("a test of your resolve and cunning"));
                if (Dm.eq(route, PATH_MOON))
                    return Dm.list(I18n.T("you see"), I18n.T("an everlasting carnival"), I18n.T("the air filled with joy and laughter"), I18n.T("but with an undercurrent of melancholy and longing"));
            }

            return Dm.list(I18n.T("you see"), I18n.T("a large door ahead"), I18n.T("intricately decorated and emanating a powerful aura"), I18n.T("but never opening"), I18n.T("no matter how long you wait"));
        }

        float dead = 0;
        foreach (mob deceased in Dm.each_cast<mob>(GLOB.player_list))
        {
            if (deceased.stat == DEAD)
                dead += 1;
        }

        float dead_share = Dm.num(Dm.div(dead, Dm.length(GLOB.joined_player_list)));
        if (dead_share >= 0.25f && dead_share <= 0.5f)
        {
            return pick(Dm.list(
                Dm.list(I18n.T("you find yourself"), I18n.T("in a small graveyard"), I18n.T("humble in size but lovingly maintained"), I18n.T("with fresh flowers on the graves")),
                Dm.list(I18n.T("you see"), I18n.T("a vision of spirits"), I18n.T("floating throughout the station"))));
        }
        if (dead_share >= 0.5f && dead_share <= 0.75f)
        {
            return pick(Dm.list(
                Dm.list(I18n.T("you find yourself"), I18n.T("in a dimly lit hallway"), I18n.T("with a sense of dread in the air")),
                Dm.list(I18n.T("you see"), I18n.T("a vision of an encroaching darkness"), I18n.T("threatening you eerily"))));
        }
        if (dead_share >= 0.75f && dead_share <= 0.9f)
        {
            return pick(Dm.list(
                Dm.list(I18n.T("you find yourself"), I18n.T("in a lonely ballroom"), I18n.T("barely lit with flickering lights")),
                Dm.list(I18n.T("you see"), I18n.T("a picture of a silent battlefield"), I18n.T("no clear victor, but heavy losses on all sides"))));
        }
        if (dead_share >= 0.9f && dead_share <= 1)
        {
            return pick(Dm.list(
                Dm.list(I18n.T("you find yourself"), I18n.T("alone"), I18n.T("no sight but your darkness"), I18n.T("no sound but your heartbeat"), I18n.T("a bleak and hopeless vision")),
                Dm.list(I18n.T("you see"), I18n.T("a vision of yourself, alone"), I18n.T("in a desolate wasteland"), I18n.T("with no signs of life or hope in sight"))));
        }

        float max_law_changes = 0;
        foreach (mob_living_silicon_ai ai in Dm.each_cast<mob_living_silicon_ai>(GLOB.ai_list))
            max_law_changes = max(max_law_changes, ai.law_change_counter);

        if (prob(clamp((max_law_changes - 10) * 10, 0, 50)))
        {
            return pick(Dm.list(
                Dm.list(I18n.T("you see"), I18n.T("a twisted and sickly tree"), I18n.T("with branches that seem to reach into every aspect of the station"), I18n.T("its roots drip with a inky black liquid")),
                Dm.list(I18n.T("you see"), I18n.T("a corrupt political figure"), I18n.T("surrounded by sycophants and puppets"), I18n.T("pulling the strings from behind the scenes")),
                Dm.list(I18n.T("you see"), I18n.T("buzzing electronics"), I18n.T("wires that seem to snake off into the distance"), I18n.T("it bathes you in red light and static"))));
        }

        if (datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED())
            return Dm.list(I18n.T("you see"), I18n.T("a new beginning on the horizon"), I18n.T("it feels warm"));

        if (datum_controller_subsystem_shuttle.EMERGENCY_PAST_POINT_OF_NO_RETURN())
            return Dm.list(I18n.T("you see"), I18n.T("salvation just out of reach"));

        if (prob(75) || Dm.truthy(GLOB.communications_controller.announced_greenshift))
        {
            if (Dm.length(GLOB.admins) >= 5)
                return Dm.list(I18n.T("you see"), I18n.T("a gathering of powerful beings in the distance"), I18n.T("their intentions unclear"));

            float tier = Dm.num(SSdynamic.current_tier.tier);
            if (tier == 0)
                return Dm.list(I18n.T("you see"), I18n.T("a flourishing field"), I18n.T("teeming with life and vitality"), I18n.T("a symbol of hope for the future"));
            if (tier == 1)
            {
                return pick(Dm.list(
                    Dm.list(I18n.T("you see"), I18n.T("a lone figure in the distance"), I18n.T("shrouded in mystery")),
                    Dm.list(I18n.T("you see"), I18n.T("a [] stalking through the shadows", pick(I18n.T("beast"), I18n.T("monster"), I18n.T("animal"))), I18n.T("its intentions unknown")),
                    Dm.list(I18n.T("you see"), I18n.T("a [] rolling in", pick(I18n.T("fog"), I18n.T("haze"), I18n.T("cloud"))), I18n.T("obscuring everything in its path"))));
            }
            if (tier == 2)
            {
                return pick(Dm.list(
                    Dm.list(I18n.T("you find yourself"), I18n.T("in a branching forest"), I18n.T("dark and with many potential paths")),
                    Dm.list(I18n.T("you find yourself"), I18n.T("in an old city"), I18n.T("still bustling with activity"), I18n.T("but with an omnipresent feel of decay"))));
            }
            if (tier == 3)
                return Dm.list(I18n.T("you find yourself"), I18n.T("in a burning city"), I18n.T("flames reaching high"), I18n.T("but with everyone doing their best to survive"));
            if (tier == 4)
                return Dm.list(I18n.T("you find yourself"), I18n.T("in a chaotic battlefield"), I18n.T("with no clear sides or victors"), I18n.T("only endless conflict and suffering"));
        }

        return Dm.list(I18n.T("you see"), I18n.T("nothing of note"), I18n.T("but have a lingering feeling of unease about the future"));
    }
}

// tg: code/modules/religion/dreams/dream_portent.dm:360
public partial class datum_dream_random_vague_portent : datum_dream_random
{
    // tg: code/modules/religion/dreams/dream_portent.dm:364
    public override object get_dream_nouns(mob_living_carbon dreamer = null)
    {
        DmList antags = Dm.list();
        foreach (datum_antagonist antag in Dm.each_cast<datum_antagonist>(GLOB.antagonists))
            Dm.combine(ref antags, LOWER_TEXT(Dm.truthy(antag.jobban_flag) ? antag.jobban_flag : antag.pref_flag));

        if (prob(80) || !Dm.truthy(Dm.length(antags)))
        {
            foreach (System.Type ruleset in Dm.each_cast<System.Type>(subtypesof(typeof(datum_dynamic_ruleset))))
                Dm.combine(ref antags, LOWER_TEXT(Dm.truthy(Dm.initial(ruleset, (datum_dynamic_ruleset p) => p.jobban_flag)) ? Dm.initial(ruleset, (datum_dynamic_ruleset p) => p.jobban_flag) : Dm.initial(ruleset, (datum_dynamic_ruleset p) => p.pref_flag)));
        }

        // chance to make nightmares the focus of the dream
        object nightmare_id = Dm.truthy(Dm.initial(typeof(datum_antagonist_nightmare), (datum_antagonist_nightmare p) => p.jobban_flag)) ? Dm.initial(typeof(datum_antagonist_nightmare), (datum_antagonist_nightmare p) => p.jobban_flag) : Dm.initial(typeof(datum_antagonist_nightmare), (datum_antagonist_nightmare p) => p.pref_flag);
        if (prob(20) && Dm.@in(LOWER_TEXT(nightmare_id), antags))
            return Dm.list(LOWER_TEXT(nightmare_id));

        return antags;
    }
}
