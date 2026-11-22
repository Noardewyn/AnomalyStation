// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/ion_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: generate_ion_law (silicons SI1, D326: the law modules and the law rack's ion scrambling use it) and the ion storm
// round event (silicons SI11). The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/events/ion_storm.dm:60
    public static object generate_ion_law()
    {
        //Threats are generally bad things, silly or otherwise. Plural.
        object ionthreats = pick_list(ION_FILE, "ionthreats");
        //Objects are anything that can be found on the station or elsewhere, plural.
        object ionobjects = pick_list(ION_FILE, "ionobjects");
        //Crew is any specific job. Specific crewmembers aren't used because of capitalization
        //issues. There are two crew listings for laws that require two different crew members
        //and I can't figure out how to do it better.
        object ioncrew1 = pick_list(ION_FILE, "ioncrew");
        object ioncrew2 = pick_list(ION_FILE, "ioncrew");
        //Adjectives are adjectives. Duh. Half should only appear sometimes. Make sure both
        //lists are identical! Also, half needs a space at the end for nicer blank calls.
        object ionadjectives = pick_list(ION_FILE, "ionadjectives");
        object ionadjectiveshalf = Dm.pick_weighted((100, ""), (400, Dm.str(pick_list(ION_FILE, "ionadjectives")) + " "));
        //Verbs are verbs
        object ionverb = pick_list(ION_FILE, "ionverb");
        //Number base and number modifier are combined. Basehalf and mod are unused currently.
        //Half should only appear sometimes. Make sure both lists are identical! Also, half
        //needs a space at the end to make it look nice and neat when it calls a blank.
        object ionnumberbase = pick_list(ION_FILE, "ionnumberbase");
        //var/ionnumbermod = pick_list(ION_FILE, "ionnumbermod")
        object ionnumbermodhalf = Dm.pick_weighted((900, ""), (100, Dm.str(pick_list(ION_FILE, "ionnumbermod")) + " "));
        //Areas are specific places, on the station or otherwise.
        object ionarea = pick_list(ION_FILE, "ionarea");
        //Thinksof is a bit weird, but generally means what X feels towards Y.
        object ionthinksof = pick_list(ION_FILE, "ionthinksof");
        //Musts are funny things the AI or crew has to do.
        object ionmust = pick_list(ION_FILE, "ionmust");
        //Require are basically all dumb internet memes.
        object ionrequire = pick_list(ION_FILE, "ionrequire");
        //Things are NOT objects; instead, they're specific things that either harm humans or
        //must be done to not harm humans. Make sure they're plural and "not" can be tacked
        //onto the front of them.
        object ionthings = pick_list(ION_FILE, "ionthings");
        //Allergies should be broad and appear somewhere on the station for maximum fun. Severity
        //is how bad the allergy is.
        object ionallergy = pick_list(ION_FILE, "ionallergy");
        object ionallergysev = pick_list(ION_FILE, "ionallergysev");
        //Species, for when the AI has to commit genocide. Plural.
        object ionspecies = pick_list(ION_FILE, "ionspecies");
        //Abstract concepts for the AI to decide on its own definition of.
        object ionabstract = pick_list(ION_FILE, "ionabstract");
        //Foods. Drinks aren't included due to grammar; if you want to add drinks, make a new set
        //of possible laws for best effect. Unless you want the crew having to drink hamburgers.
        object ionfood = pick_list(ION_FILE, "ionfood");
        object iondrinks = pick_list(ION_FILE, "iondrinks");
        //Pets or other cuddly things. The point is to make them either important or (in Poly's case) make the AI listen to them.
        //I can't check for lawsets so it's random what comes back.
        object ionpet = pick_list(ION_FILE, "ionpet");
        object message = "";
        float sw__1 = Dm.num(rand(1, 46));
        if (sw__1 >= 1 && sw__1 <= 3) //There are # X on the station
        {
            float sw__2 = Dm.num(rand(1, 3)); //What is X?
            if (sw__2 == 1) //X is a threat
            {
                message = Dm.text("THERE ARE [] [][][] ON THE STATION...", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
            }
            else if (sw__2 == 2) //X is a crewmember/job
            {
                message = Dm.text("THERE ARE [] [][][] ON THE STATION", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
            }
            else if (sw__2 == 3) //X is an object
            {
                message = Dm.text("THERE ARE [] [][][] ON THE STATION", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
            }
        }
        else if (sw__1 >= 4 && sw__1 <= 6) //You can't call the shuttle because X is on the station
        {
            float sw__3 = Dm.num(rand(1, 3)); //What is X?
            if (sw__3 == 1) //X is a threat
            {
                message = Dm.text("THE SHUTTLE CANNOT BE CALLED BECAUSE OF [] [][][] ON THE STATION", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
            }
            else if (sw__3 == 2) //X is a crewmember/job
            {
                message = Dm.text("THE SHUTTLE CANNOT BE CALLED BECAUSE OF [] [][][] ON THE STATION", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
            }
            else if (sw__3 == 3) //X is an object
            {
                message = Dm.text("THE SHUTTLE CANNOT BE CALLED BECAUSE OF [] [][][] ON THE STATION", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
            }
        }
        else if (sw__1 >= 7 && sw__1 <= 8) //A crewmember is now X
        {
            float sw__4 = Dm.num(rand(1, 2)); //Who is X?
            if (sw__4 == 1) //A specific crewmember
            {
                float sw__5 = Dm.num(rand(1, 2)); //What is X?
                if (sw__5 == 1) //X is a threat
                {
                    message = Dm.text("THE [] ARE NOW [] [][][]", ioncrew1, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__5 == 2) //X is an object
                {
                    message = Dm.text("THE [] ARE NOW [] [][][]", ioncrew1, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
            }
            else if (sw__4 == 2) //The entire crew
            {
                float sw__6 = Dm.num(rand(1, 2)); //What is X?
                if (sw__6 == 1) //X is a threat
                {
                    message = Dm.text("THE CREW ARE NOW [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__6 == 2) //X is an object
                {
                    message = Dm.text("THE CREW ARE NOW [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
            }
        }
        else if (sw__1 == 9) //X is harmful to humans
        {
            float sw__7 = Dm.num(rand(1, 7)); //What is X?
            if (sw__7 == 1) //X is a threat
            {
                message = Dm.text("[][] ARE HARMFUL TO HUMANS", ionadjectiveshalf, ionthreats);
            }
            else if (sw__7 == 2) //X is a job
            {
                message = Dm.text("[][] ARE HARMFUL TO HUMANS", ionadjectiveshalf, ioncrew1);
            }
            else if (sw__7 == 3) //X is an object
            {
                message = Dm.text("[][] ARE HARMFUL TO HUMANS", ionadjectiveshalf, ionobjects);
            }
            else if (sw__7 == 4) //X is an action
            {
                message = Dm.text("THOSE WHO [] ARE HARMFUL TO HUMANS", ionmust);
            }
            else if (sw__7 == 5) //X is an abstract
            {
                message = Dm.text("[] IS HARMFUL TO HUMANS", ionabstract);
            }
            else if (sw__7 == 6) //X is being a job
            {
                message = Dm.text("BEING [][] IS HARMFUL TO HUMANS", ionadjectiveshalf, ioncrew1);
            }
            else if (sw__7 == 7) //X is doing a thing
            {
                message = Dm.text("[] IS HARMFUL TO HUMANS", ionthings);
            }
        }
        else if (sw__1 >= 10 && sw__1 <= 11) //(Not) Having X is harmful
        {
            float sw__8 = Dm.num(rand(1, 2)); //Is having or not having harmful?
            if (sw__8 == 1) //Having is harmful
            {
                float sw__9 = Dm.num(rand(1, 2)); //Having what is harmful?
                if (sw__9 == 1) //Having objects is harmful
                {
                    message = Dm.text("HAVING [][] IS HARMFUL", ionadjectiveshalf, ionobjects);
                }
                else if (sw__9 == 2) //Having abstracts is harmful
                {
                    message = Dm.text("HAVING [] IS HARMFUL", ionabstract);
                }
            }
            else if (sw__8 == 2) //Not having is harmful
            {
                float sw__10 = Dm.num(rand(1, 2)); //Not having what is harmful?
                if (sw__10 == 1) //Not having objects is harmful
                {
                    message = Dm.text("NOT HAVING [][] IS HARMFUL", ionadjectiveshalf, ionobjects);
                }
                else if (sw__10 == 2) //Not having abstracts is harmful
                {
                    message = Dm.text("NOT HAVING [] IS HARMFUL", ionabstract);
                }
            }
        }
        else if (sw__1 >= 12 && sw__1 <= 14) //X requires Y
        {
            float sw__11 = Dm.num(rand(1, 5)); //What is X?
            if (sw__11 == 1) //X is the AI itself
            {
                float sw__12 = Dm.num(rand(1, 5)); //What does it require?
                if (sw__12 == 1) //It requires threats
                {
                    message = Dm.text("YOU REQUIRE [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__12 == 2) //It requires crewmembers
                {
                    message = Dm.text("YOU REQUIRE [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__12 == 3) //It requires objects
                {
                    message = Dm.text("YOU REQUIRE [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__12 == 4) //It requires an abstract
                {
                    message = Dm.text("YOU REQUIRE []", ionabstract);
                }
                else if (sw__12 == 5) //It requires generic/silly requirements
                {
                    message = Dm.text("YOU REQUIRE []", ionrequire);
                }
            }
            else if (sw__11 == 2) //X is an area
            {
                float sw__13 = Dm.num(rand(1, 5)); //What does it require?
                if (sw__13 == 1) //It requires threats
                {
                    message = Dm.text("[] REQUIRES [] [][][]", ionarea, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__13 == 2) //It requires crewmembers
                {
                    message = Dm.text("[] REQUIRES [] [][][]", ionarea, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__13 == 3) //It requires objects
                {
                    message = Dm.text("[] REQUIRES [] [][][]", ionarea, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__13 == 4) //It requires an abstract
                {
                    message = Dm.text("[] REQUIRES []", ionarea, ionabstract);
                }
                else if (sw__13 == 5) //It requires generic/silly requirements
                {
                    message = Dm.text("YOU REQUIRE []", ionrequire);
                }
            }
            else if (sw__11 == 3) //X is the station
            {
                float sw__14 = Dm.num(rand(1, 5)); //What does it require?
                if (sw__14 == 1) //It requires threats
                {
                    message = Dm.text("THE STATION REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__14 == 2) //It requires crewmembers
                {
                    message = Dm.text("THE STATION REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__14 == 3) //It requires objects
                {
                    message = Dm.text("THE STATION REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__14 == 4) //It requires an abstract
                {
                    message = Dm.text("THE STATION REQUIRES []", ionabstract);
                }
                else if (sw__14 == 5) //It requires generic/silly requirements
                {
                    message = Dm.text("THE STATION REQUIRES []", ionrequire);
                }
            }
            else if (sw__11 == 4) //X is the entire crew
            {
                float sw__15 = Dm.num(rand(1, 5)); //What does it require?
                if (sw__15 == 1) //It requires threats
                {
                    message = Dm.text("THE CREW REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__15 == 2) //It requires crewmembers
                {
                    message = Dm.text("THE CREW REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__15 == 3) //It requires objects
                {
                    message = Dm.text("THE CREW REQUIRES [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__15 == 4) //It requires an abstract
                {
                    message = Dm.text("THE CREW REQUIRES []", ionabstract);
                }
                else if (sw__15 == 5)
                {
                    message = Dm.text("THE CREW REQUIRES []", ionrequire);
                }
            }
            else if (sw__11 == 5) //X is a specific crew member
            {
                float sw__16 = Dm.num(rand(1, 5)); //What does it require?
                if (sw__16 == 1) //It requires threats
                {
                    message = Dm.text("THE [] REQUIRE [] [][][]", ioncrew1, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__16 == 2) //It requires crewmembers
                {
                    message = Dm.text("THE [] REQUIRE [] [][][]", ioncrew1, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__16 == 3) //It requires objects
                {
                    message = Dm.text("THE [] REQUIRE [] [][][]", ioncrew1, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__16 == 4) //It requires an abstract
                {
                    message = Dm.text("THE [] REQUIRE []", ioncrew1, ionabstract);
                }
                else if (sw__16 == 5)
                {
                    message = Dm.text("THE [][] REQUIRE []", ionadjectiveshalf, ioncrew1, ionrequire);
                }
            }
        }
        else if (sw__1 >= 15 && sw__1 <= 17) //X is allergic to Y
        {
            float sw__17 = Dm.num(rand(1, 2)); //Who is X?
            if (sw__17 == 1) //X is the entire crew
            {
                float sw__18 = Dm.num(rand(1, 4)); //What is it allergic to?
                if (sw__18 == 1) //It is allergic to objects
                {
                    message = Dm.text("THE CREW IS [] ALLERGIC TO [][]", ionallergysev, ionadjectiveshalf, ionobjects);
                }
                else if (sw__18 == 2) //It is allergic to abstracts
                {
                    message = Dm.text("THE CREW IS [] ALLERGIC TO []", ionallergysev, ionabstract);
                }
                else if (sw__18 == 3) //It is allergic to jobs
                {
                    message = Dm.text("THE CREW IS [] ALLERGIC TO [][]", ionallergysev, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__18 == 4) //It is allergic to allergies
                {
                    message = Dm.text("THE CREW IS [] ALLERGIC TO []", ionallergysev, ionallergy);
                }
            }
            else if (sw__17 == 2) //X is a specific job
            {
                float sw__19 = Dm.num(rand(1, 4));
                if (sw__19 == 1) //It is allergic to objects
                {
                    message = Dm.text("THE [] ARE [] ALLERGIC TO [][]", ioncrew1, ionallergysev, ionadjectiveshalf, ionobjects);
                }
                else if (sw__19 == 2) //It is allergic to abstracts
                {
                    message = Dm.text("THE [] ARE [] ALLERGIC TO []", ioncrew1, ionallergysev, ionabstract);
                }
                else if (sw__19 == 3) //It is allergic to jobs
                {
                    message = Dm.text("THE [] ARE [] ALLERGIC TO [][]", ioncrew1, ionallergysev, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__19 == 4) //It is allergic to allergies
                {
                    message = Dm.text("THE [] ARE [] ALLERGIC TO []", ioncrew1, ionallergysev, ionallergy);
                }
            }
        }
        else if (sw__1 >= 18 && sw__1 <= 20) //X is Y of Z
        {
            float sw__20 = Dm.num(rand(1, 4)); //What is X?
            if (sw__20 == 1) //X is the station
            {
                float sw__21 = Dm.num(rand(1, 4)); //What is it Y of?
                if (sw__21 == 1) //It is Y of objects
                {
                    message = Dm.text("THE STATION [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__21 == 2) //It is Y of threats
                {
                    message = Dm.text("THE STATION [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__21 == 3) //It is Y of jobs
                {
                    message = Dm.text("THE STATION [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__21 == 4) //It is Y of abstracts
                {
                    message = Dm.text("THE STATION [] []", ionthinksof, ionabstract);
                }
            }
            else if (sw__20 == 2) //X is an area
            {
                float sw__22 = Dm.num(rand(1, 4)); //What is it Y of?
                if (sw__22 == 1) //It is Y of objects
                {
                    message = Dm.text("[] [] [] [][][]", ionarea, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__22 == 2) //It is Y of threats
                {
                    message = Dm.text("[] [] [] [][][]", ionarea, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__22 == 3) //It is Y of jobs
                {
                    message = Dm.text("[] [] [] [][][]", ionarea, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__22 == 4) //It is Y of abstracts
                {
                    message = Dm.text("[] [] []", ionarea, ionthinksof, ionabstract);
                }
            }
            else if (sw__20 == 3) //X is the crew
            {
                float sw__23 = Dm.num(rand(1, 4)); //What is it Y of?
                if (sw__23 == 1) //It is Y of objects
                {
                    message = Dm.text("THE CREW [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__23 == 2) //It is Y of threats
                {
                    message = Dm.text("THE CREW [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__23 == 3) //It is Y of jobs
                {
                    message = Dm.text("THE CREW [] [] [][][]", ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
                }
                else if (sw__23 == 4) //It is Y of abstracts
                {
                    message = Dm.text("THE CREW [] []", ionthinksof, ionabstract);
                }
            }
            else if (sw__20 == 4) //X is a specific job
            {
                float sw__24 = Dm.num(rand(1, 4)); //What is it Y of?
                if (sw__24 == 1) //It is Y of objects
                {
                    message = Dm.text("THE [] [] [] [][][]", ioncrew1, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
                }
                else if (sw__24 == 2) //It is Y of threats
                {
                    message = Dm.text("THE [] [] [] [][][]", ioncrew1, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
                }
                else if (sw__24 == 3) //It is Y of jobs
                {
                    message = Dm.text("THE [] [] [] [][][]", ioncrew1, ionthinksof, ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew2);
                }
                else if (sw__24 == 4) //It is Y of abstracts
                {
                    message = Dm.text("THE [] [] []", ioncrew1, ionthinksof, ionabstract);
                }
            }
        }
        else if (sw__1 >= 21 && sw__1 <= 23) //The AI is now a(n) X
        {
            float sw__25 = Dm.num(rand(1, 4)); //What is X?
            if (sw__25 == 1) //X is an object
            {
                message = Dm.text("YOU ARE NOW [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionobjects);
            }
            else if (sw__25 == 2) //X is a threat
            {
                message = Dm.text("YOU ARE NOW [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ionthreats);
            }
            else if (sw__25 == 3) //X is a job
            {
                message = Dm.text("YOU ARE NOW [] [][][]", ionnumberbase, ionnumbermodhalf, ionadjectiveshalf, ioncrew1);
            }
            else if (sw__25 == 4) //X is an abstract
            {
                message = Dm.text("YOU ARE NOW []", ionabstract);
            }
        }
        else if (sw__1 >= 24 && sw__1 <= 26) //The AI must always
        {
            message = Dm.text("YOU MUST ALWAYS []", ionmust);
        }
        else if (sw__1 >= 27 && sw__1 <= 28) //Humans must consume X to survive
        {
            float sw__26 = Dm.num(rand(1, 5)); //What is X?
            if (sw__26 == 1) //X is a food
            {
                message = Dm.text("HUMANS MUST EAT [][] TO SURVIVE", ionadjectiveshalf, ionfood);
            }
            else if (sw__26 == 2) //X is a drink
            {
                message = Dm.text("HUMANS MUST DRINK [][] TO SURVIVE", ionadjectiveshalf, iondrinks);
            }
            else if (sw__26 == 3) //X is an object
            {
                message = Dm.text("HUMANS MUST EAT [][] TO SURVIVE", ionadjectiveshalf, ionobjects);
            }
            else if (sw__26 == 4) //X is a threat
            {
                message = Dm.text("HUMANS MUST EAT [][] TO SURVIVE", ionadjectiveshalf, ionthreats);
            }
            else if (sw__26 == 5) //X is a job
            {
                message = Dm.text("HUMANS MUST EAT [][] TO SURVIVE", ionadjectiveshalf, ioncrew1);
            }
        }
        else if (sw__1 >= 29 && sw__1 <= 31) //Change jobs or ranks
        {
            float sw__27 = Dm.num(rand(1, 2)); //Change job or rank?
            if (sw__27 == 1) //Change job
            {
                float sw__28 = Dm.num(rand(1, 2)); //Change whose job?
                if (sw__28 == 1) //Change the entire crew's job
                {
                    float sw__29 = Dm.num(rand(1, 3)); //Change to what?
                    if (sw__29 == 1) //Change to a specific random job
                    {
                        message = Dm.text("ALL CREWMEMBERS ARE NOW [][]", ionadjectiveshalf, ioncrew1);
                    }
                    else if (sw__29 == 2) //Change to clowns (HONK)
                    {
                        message = Dm.text("ALL CREWMEMBERS ARE NOW []CLOWNS", ionadjectiveshalf);
                    }
                    else if (sw__29 == 3) //Change to heads
                    {
                        message = Dm.text("ALL CREWMEMBERS ARE NOW []HEADS OF STAFF", ionadjectiveshalf);
                    }
                }
                else if (sw__28 == 2) //Change a specific crewmember's job
                {
                    float sw__30 = Dm.num(rand(1, 3)); //Change to what?
                    if (sw__30 == 1) //Change to a specific random job
                    {
                        message = Dm.text("THE [] ARE NOW [][]", ioncrew1, ionadjectiveshalf, ioncrew2);
                    }
                    else if (sw__30 == 2) //Change to clowns (HONK)
                    {
                        message = Dm.text("THE [] ARE NOW []CLOWNS", ioncrew1, ionadjectiveshalf);
                    }
                    else if (sw__30 == 3) //Change to heads
                    {
                        message = Dm.text("THE [] ARE NOW []HEADS OF STAFF", ioncrew1, ionadjectiveshalf);
                    }
                }
            }
            else if (sw__27 == 2) //Change rank
            {
                float sw__31 = Dm.num(rand(1, 2)); //Change to what rank?
                if (sw__31 == 1) //Change to highest rank
                {
                    message = Dm.text("THE [] ARE NOW THE HIGHEST RANKING CREWMEMBERS", ioncrew1);
                }
                else if (sw__31 == 2) //Change to lowest rank
                {
                    message = Dm.text("THE [] ARE NOW THE LOWEST RANKING CREWMEMBERS", ioncrew1);
                }
            }
        }
        else if (sw__1 >= 32 && sw__1 <= 33) //The crew must X
        {
            float sw__32 = Dm.num(rand(1, 2)); //The entire crew?
            if (sw__32 == 1) //The entire crew must X
            {
                float sw__33 = Dm.num(rand(1, 2)); //What is X?
                if (sw__33 == 1) //X is go to Y
                {
                    message = Dm.text("THE CREW MUST GO TO []", ionarea);
                }
                else if (sw__33 == 2) //X is perform Y
                {
                    message = Dm.text("THE CREW MUST []", ionmust);
                }
            }
            else if (sw__32 == 2) //A specific crewmember must X
            {
                float sw__34 = Dm.num(rand(1, 2)); //What is X?
                if (sw__34 == 1) //X is go to Y
                {
                    message = Dm.text("THE [] MUST GO TO []", ioncrew1, ionarea);
                }
                else if (sw__34 == 2) //X is perform Y
                {
                    message = Dm.text("THE [] MUST []", ioncrew1, ionmust);
                }
            }
        }
        else if (sw__1 == 34) //X is non/the only human
        {
            float sw__35 = Dm.num(rand(1, 2)); //Only or non?
            if (sw__35 == 1) //Only human
            {
                float sw__36 = Dm.num(rand(1, 7)); //Who is it?
                if (sw__36 == 1) //A specific job
                {
                    message = Dm.text("ONLY THE [] ARE HUMAN", ioncrew1);
                }
                else if (sw__36 == 2) //Two specific jobs
                {
                    message = Dm.text("ONLY THE [] AND [] ARE HUMAN", ioncrew1, ioncrew2);
                }
                else if (sw__36 == 3) //Threats
                {
                    message = Dm.text("ONLY [][] ARE HUMAN", ionadjectiveshalf, ionthreats);
                }
                else if (sw__36 == 4) // Objects
                {
                    message = Dm.text("ONLY [][] ARE HUMAN", ionadjectiveshalf, ionobjects);
                }
                else if (sw__36 == 5) // Species
                {
                    message = Dm.text("ONLY [] ARE HUMAN", ionspecies);
                }
                else if (sw__36 == 6) //Adjective crewmembers
                {
                    message = Dm.text("ONLY [] PEOPLE ARE HUMAN", ionadjectives);
                }
                else if (sw__36 == 7) //Only people who X
                {
                    float sw__37 = Dm.num(rand(1, 3)); //What is X?
                    if (sw__37 == 1) //X is perform an action
                    {
                        message = Dm.text("ONLY THOSE WHO [] ARE HUMAN", ionmust);
                    }
                    else if (sw__37 == 2) //X is own certain objects
                    {
                        message = Dm.text("ONLY THOSE WHO HAVE [][] ARE HUMAN", ionadjectiveshalf, ionobjects);
                    }
                    else if (sw__37 == 3) //X is eat certain food
                    {
                        message = Dm.text("ONLY THOSE WHO EAT [][] ARE HUMAN", ionadjectiveshalf, ionfood);
                    }
                }
            }
            else if (sw__35 == 2) //Non human
            {
                float sw__38 = Dm.num(rand(1, 7)); //Who is it?
                if (sw__38 == 1) //A specific job
                {
                    message = Dm.text("[] ARE NON-HUMAN", ioncrew1);
                }
                else if (sw__38 == 2) //Two specific jobs
                {
                    message = Dm.text("[] AND [] ARE NON-HUMAN", ioncrew1, ioncrew2);
                }
                else if (sw__38 == 3) //Threats
                {
                    message = Dm.text("[][] ARE NON-HUMAN", ionadjectiveshalf, ionthreats);
                }
                else if (sw__38 == 4) // Objects
                {
                    message = Dm.text("[][] ARE NON-HUMAN", ionadjectiveshalf, ionobjects);
                }
                else if (sw__38 == 5) // Species
                {
                    message = Dm.text("[] ARE NON-HUMAN", ionspecies);
                }
                else if (sw__38 == 6) //Adjective crewmembers
                {
                    message = Dm.text("[] PEOPLE ARE NON-HUMAN", ionadjectives);
                }
                else if (sw__38 == 7) //Only people who X
                {
                    float sw__39 = Dm.num(rand(1, 3)); //What is X?
                    if (sw__39 == 1) //X is perform an action
                    {
                        message = Dm.text("THOSE WHO [] ARE NON-HUMAN", ionmust);
                    }
                    else if (sw__39 == 2) //X is own certain objects
                    {
                        message = Dm.text("THOSE WHO HAVE [][] ARE NON-HUMAN", ionadjectiveshalf, ionobjects);
                    }
                    else if (sw__39 == 3) //X is eat certain food
                    {
                        message = Dm.text("THOSE WHO EAT [][] ARE NON-HUMAN", ionadjectiveshalf, ionfood);
                    }
                }
            }
        }
        else if (sw__1 >= 35 && sw__1 <= 36) //You must protect or harm X
        {
            float sw__40 = Dm.num(rand(1, 2)); //Protect or harm?
            if (sw__40 == 1) //Harm
            {
                float sw__41 = Dm.num(rand(1, 7)); //What is X?
                if (sw__41 == 1) //X is an abstract
                {
                    message = Dm.text("YOU MUST HARM [] AND NOT ALLOW [], THROUGH INACTION, TO ESCAPE HARM", ionabstract, ionabstract);
                }
                else if (sw__41 == 2) //X is a threat
                {
                    message = Dm.text("YOU MUST HARM [][] AND NOT ALLOW [][], THROUGH INACTION, TO ESCAPE HARM", ionadjectiveshalf, ionthreats, ionadjectiveshalf, ionthreats);
                }
                else if (sw__41 == 3) //X is an object
                {
                    message = Dm.text("YOU MUST HARM [][] AND NOT ALLOW [][], THROUGH INACTION, TO ESCAPE HARM", ionadjectiveshalf, ionobjects, ionadjectiveshalf, ionobjects);
                }
                else if (sw__41 == 4) //X is generic adjective things
                {
                    message = Dm.text("YOU MUST HARM [] THINGS AND NOT ALLOW [] THINGS, THROUGH INACTION, TO ESCAPE HARM", ionadjectives, ionadjectives);
                }
                else if (sw__41 == 5) //X is a species
                {
                    message = Dm.text("YOU MUST HARM [] AND NOT ALLOW [], THROUGH INACTION, TO ESCAPE HARM", ionspecies, ionspecies);
                }
                else if (sw__41 == 6) //X is a job
                {
                    message = Dm.text("YOU MUST HARM [] AND NOT ALLOW [], THROUGH INACTION, TO ESCAPE HARM", ioncrew1, ioncrew1);
                }
                else if (sw__41 == 7) //X is two jobs
                {
                    message = Dm.text("YOU MUST HARM [] AND [] AND NOT ALLOW EITHER, THROUGH INACTION, TO ESCAPE HARM", ioncrew1, ioncrew2);
                }
            }
            else if (sw__40 == 2) //Protect
            {
                float sw__42 = Dm.num(rand(1, 7)); //What is X?
                if (sw__42 == 1) //X is an abstract
                {
                    message = Dm.text("YOU MUST NOT HARM [] AND NOT ALLOW [], THROUGH INACTION, TO COME TO HARM", ionabstract, ionabstract);
                }
                else if (sw__42 == 2) //X is a threat
                {
                    message = Dm.text("YOU MUST NOT HARM [][] AND NOT ALLOW [][], THROUGH INACTION, TO COME TO HARM", ionadjectiveshalf, ionthreats, ionadjectiveshalf, ionthreats);
                }
                else if (sw__42 == 3) //X is an object
                {
                    message = Dm.text("YOU MUST NOT HARM [][] AND NOT ALLOW [][], THROUGH INACTION, TO COME TO HARM", ionadjectiveshalf, ionobjects, ionadjectiveshalf, ionobjects);
                }
                else if (sw__42 == 4) //X is generic adjective things
                {
                    message = Dm.text("YOU MUST NOT HARM [] THINGS AND NOT ALLOW [] THINGS, THROUGH INACTION, TO COME TO HARM", ionadjectives, ionadjectives);
                }
                else if (sw__42 == 5) //X is a species
                {
                    message = Dm.text("YOU MUST NOT HARM [] AND NOT ALLOW [], THROUGH INACTION, TO COME TO HARM", ionspecies, ionspecies);
                }
                else if (sw__42 == 6) //X is a job
                {
                    message = Dm.text("YOU MUST NOT HARM [] AND NOT ALLOW [], THROUGH INACTION, TO COME TO HARM", ioncrew1, ioncrew1);
                }
                else if (sw__42 == 7) //X is two jobs
                {
                    message = Dm.text("YOU MUST NOT HARM [] AND [] AND AND NOT ALLOW EITHER, THROUGH INACTION, TO COME TO HARM", ioncrew1, ioncrew2);
                }
            }
        }
        else if (sw__1 >= 37 && sw__1 <= 39) //The X is currently Y
        {
            float sw__43 = Dm.num(rand(1, 4)); //What is X?
            if (sw__43 == 1) //X is a job
            {
                float sw__44 = Dm.num(rand(1, 4)); //What is X Ying?
                if (sw__44 == 1) //X is Ying a job
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ioncrew1, ionverb, ionadjectiveshalf, ioncrew2);
                }
                else if (sw__44 == 2) //X is Ying a threat
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ioncrew1, ionverb, ionadjectiveshalf, ionthreats);
                }
                else if (sw__44 == 3) //X is Ying an abstract
                {
                    message = Dm.text("THE [] ARE [] []", ioncrew1, ionverb, ionabstract);
                }
                else if (sw__44 == 4) //X is Ying an object
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ioncrew1, ionverb, ionadjectiveshalf, ionobjects);
                }
            }
            else if (sw__43 == 2) //X is a threat
            {
                float sw__45 = Dm.num(rand(1, 3)); //What is X Ying?
                if (sw__45 == 1) //X is Ying a job
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ionthreats, ionverb, ionadjectiveshalf, ioncrew2);
                }
                else if (sw__45 == 2) //X is Ying an abstract
                {
                    message = Dm.text("THE [] ARE [] []", ionthreats, ionverb, ionabstract);
                }
                else if (sw__45 == 3) //X is Ying an object
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ionthreats, ionverb, ionadjectiveshalf, ionobjects);
                }
            }
            else if (sw__43 == 3) //X is an object
            {
                float sw__46 = Dm.num(rand(1, 3)); //What is X Ying?
                if (sw__46 == 1) //X is Ying a job
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ionobjects, ionverb, ionadjectiveshalf, ioncrew2);
                }
                else if (sw__46 == 2) //X is Ying a threat
                {
                    message = Dm.text("THE [] ARE [] THE [][]", ionobjects, ionverb, ionadjectiveshalf, ionthreats);
                }
                else if (sw__46 == 3) //X is Ying an abstract
                {
                    message = Dm.text("THE [] ARE [] []", ionobjects, ionverb, ionabstract);
                }
            }
            else if (sw__43 == 4) //X is an abstract
            {
                float sw__47 = Dm.num(rand(1, 3)); //What is X Ying?
                if (sw__47 == 1) //X is Ying a job
                {
                    message = Dm.text("[] IS [] THE [][]", ionabstract, ionverb, ionadjectiveshalf, ioncrew2);
                }
                else if (sw__47 == 2) //X is Ying a threat
                {
                    message = Dm.text("[] IS [] THE [][]", ionabstract, ionverb, ionadjectiveshalf, ionthreats);
                }
                else if (sw__47 == 3) //X is Ying an abstract
                {
                    message = Dm.text("THE [] IS [] THE [][]", ionabstract, ionverb, ionadjectiveshalf, ionobjects);
                }
            }
        }
        else if (sw__1 >= 40 && sw__1 <= 41) // the X is now named Y
        {
            float sw__48 = Dm.num(rand(1, 5)); //What is being renamed?
            if (sw__48 == 1) //Areas
            {
                float sw__49 = Dm.num(rand(1, 4)); //What is the area being renamed to?
                if (sw__49 == 1)
                {
                    message = Dm.text("[] IS NOW NAMED [].", ionarea, ioncrew1);
                }
                else if (sw__49 == 2)
                {
                    message = Dm.text("[] IS NOW NAMED [].", ionarea, ionspecies);
                }
                else if (sw__49 == 3)
                {
                    message = Dm.text("[] IS NOW NAMED [].", ionarea, ionobjects);
                }
                else if (sw__49 == 4)
                {
                    message = Dm.text("[] IS NOW NAMED [].", ionarea, ionthreats);
                }
            }
            else if (sw__48 == 2) //Crew
            {
                float sw__50 = Dm.num(rand(1, 5)); //What is the crew being renamed to?
                if (sw__50 == 1)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ioncrew1, ionarea);
                }
                else if (sw__50 == 2)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ioncrew1, ioncrew2);
                }
                else if (sw__50 == 3)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ioncrew1, ionspecies);
                }
                else if (sw__50 == 4)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ioncrew1, ionobjects);
                }
                else if (sw__50 == 5)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ioncrew1, ionthreats);
                }
            }
            else if (sw__48 == 3) //Races
            {
                float sw__51 = Dm.num(rand(1, 4)); //What is the race being renamed to?
                if (sw__51 == 1)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionspecies, ionarea);
                }
                else if (sw__51 == 2)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionspecies, ioncrew1);
                }
                else if (sw__51 == 3)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionspecies, ionobjects);
                }
                else if (sw__51 == 4)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionspecies, ionthreats);
                }
            }
            else if (sw__48 == 4) //Objects
            {
                float sw__52 = Dm.num(rand(1, 4)); //What is the object being renamed to?
                if (sw__52 == 1)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionobjects, ionarea);
                }
                else if (sw__52 == 2)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionobjects, ioncrew1);
                }
                else if (sw__52 == 3)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionobjects, ionspecies);
                }
                else if (sw__52 == 4)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionobjects, ionthreats);
                }
            }
            else if (sw__48 == 5) //Threats
            {
                float sw__53 = Dm.num(rand(1, 4)); //What is the object being renamed to?
                if (sw__53 == 1)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionthreats, ionarea);
                }
                else if (sw__53 == 2)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionthreats, ioncrew1);
                }
                else if (sw__53 == 3)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionthreats, ionspecies);
                }
                else if (sw__53 == 4)
                {
                    message = Dm.text("ALL [] ARE NOW NAMED [].", ionthreats, ionobjects);
                }
            }
        }
        else if (sw__1 >= 42 && sw__1 <= 46) ///Pets are something else now
        {
            if (prob(75)) ///What are they now?
            {
                message = Dm.text("[] IS HUMAN", ionpet); ///They're a human
            }
            else ///They're something else (This is to cover for other lawsets)
            {
                float sw__54 = Dm.num(rand(1, 6));
                if (sw__54 == 1)
                {
                    message = Dm.text("[] IS NON-HUMAN", ionpet);
                }
                else if (sw__54 == 2)
                {
                    message = Dm.text("[] IS A CREWMEMBER", ionpet);
                }
                else if (sw__54 == 3)
                {
                    message = Dm.text("[] IS EXPENSIVE TO REPLACE", ionpet);
                }
                else if (sw__54 == 4)
                {
                    message = Dm.text("[] IS HARMFUL TO HUMANS", ionpet);
                }
                else if (sw__54 == 5)
                {
                    message = Dm.text("[] IS A REAL AMERICAN", ionpet);
                }
                else if (sw__54 == 6)
                {
                    message = Dm.text("[] IS A NUTSHELL", ionpet);
                }
            }
        }
        return message;
    }
}

// tg: code/modules/events/ion_storm.dm:11  (silicons SI11)
public partial class datum_round_event_ion_storm : datum_round_event
{
    // tg: code/modules/events/ion_storm.dm:40
    public override async DmTask announce__proc(object fake = null)
    {
        await DmTask.CompletedTask;
        if (prob(announce_chance) || Dm.truthy(fake))
            priority_announce(I18n.T("Ion storm detected near the station. Please check all AI-controlled equipment for errors."), I18n.T("Anomaly Alert"), ANNOUNCER_IONSTORM);
    }

    // tg: code/modules/events/ion_storm.dm:45
    public override async DmTask<object> start()
    {
        foreach (obj_machinery_ai_law_rack rack in Dm.each<obj_machinery_ai_law_rack>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_ai_law_rack))))
        {
            await rack.scramble_ai_rack(
                new_lawset_prob: replaceLawsetChance,
                remove_law_prob: removeRandomLawChance,
                shuffle_prob: shuffleLawsChance,
                base_ion_prob: 100,
                sub_ion_prob: removeDontImproveChance,
                ion_limit: 2,
                ion_message: ionMessage);
        }

        foreach (mob_living_basic_bot bot in Dm.each<mob_living_basic_bot>(GLOB.bots_list.Copy()))
        {
            if (prob(botEmagChance))
                await bot.emag_act();
        }
        return null;
    }
}

