// Translated from tgstation (snapshot 1d99bd7f): code/modules/holiday/holidays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the holiday datum (its dates, greeting, station name, hat, mail, poster and colours) and every holiday's
// own procs, with poll_holiday. A holiday is celebrated when SSevents fills GLOB.holidays (ALLOW_HOLIDAYS) from world.realtime, which the
// host sets (DV3). The holidays' greetings are player text (I18n.T); their station names are data, as tg's name lists. The var blocks are
// generated (D111).

public partial class datum_controller_global_vars
{
    /// List of all holiday-related mail. Do not edit this directly, instead add to var/list/holiday_mail
    // tg: code/modules/holiday/holidays.dm:2  GLOBAL_LIST_INIT(holiday_mail, list())
    public DmList holiday_mail;
    public virtual void InitGlobalholiday_mail()
    {
        holiday_mail = Dm.list();
        Dm.append(ref gvars_datum_init_order, "holiday_mail");
    }
}

// tg: code/modules/holiday/holidays.dm:4
public partial class datum_holiday : datum
{
    // This proc gets run before the game starts when the holiday is activated. Do festive shit here.
    // tg: code/modules/holiday/holidays.dm:39
    public virtual object celebrate()
    {
        if (Dm.truthy(no_mail_holiday))
            SSeconomy.mail_blocked = TRUE;
        if (Dm.truthy(LAZYLEN(holiday_mail)) && !Dm.truthy(no_mail_holiday))
            Dm.append(ref GLOB.holiday_mail, holiday_mail);
        return null;
    }

    // When the round starts, this proc is ran to get a text message to display to everyone to wish them a happy holiday
    // tg: code/modules/holiday/holidays.dm:47
    public virtual object greet() => I18n.T("Have a happy []!", name);

    /// Returns special prefix station name on certain days. (See new_station_name())
    /// Turns "Experimental Research City" into "Christmas Research City"
    // tg: code/modules/holiday/holidays.dm:52
    public virtual object get_station_prefix() => "";

    /// Returns special names for the station name on certain days. (See new_station_name())
    /// Turns "Experimental Research City" into "Experimental Christmas City"
    // tg: code/modules/holiday/holidays.dm:57
    public virtual object get_station_name()
    {
        // By default get the first word of the Holiday and use that
        return copytext(name, 1, findtext(name, " "));
    }

    // Return 1 if this holidy should be celebrated today
    // tg: code/modules/holiday/holidays.dm:62
    public virtual object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        if (Dm.truthy(always_celebrate))
            return TRUE;

        float d = Dm.num(dd), m = Dm.num(mm);
        if (!Dm.truthy(end_day))
            end_day = begin_day;
        if (!Dm.truthy(end_month))
            end_month = begin_month;
        if (end_month > begin_month) //holiday spans multiple months in one year
        {
            if (m == end_month) //in final month
            {
                if (d <= end_day)
                    return TRUE;
            }
            else if (m == begin_month)//in first month
            {
                if (d >= begin_day)
                    return TRUE;
            }
            else if (m >= begin_month && m <= end_month) //holiday spans 3+ months and we're in the middle, day doesn't matter at all
                return TRUE;
        }
        else if (end_month == begin_month) // starts and stops in same month, simplest case
        {
            if (m == begin_month && d >= begin_day && d <= end_day)
                return TRUE;
        }
        else // starts in one year, ends in the next
        {
            if (m >= begin_month && d >= begin_day) // Holiday ends next year
                return TRUE;
            if (m <= end_month && d <= end_day) // Holiday started last year
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/holiday/holidays.dm:94
    public virtual object get_holiday_colors(atom thing_to_color = null, object pattern = null)
    {
        return get_decoration_color_from_pattern(thing_to_color, Dm.truthy(pattern) ? pattern : holiday_pattern, holiday_colors);
    }
}

// The actual holidays: the greetings and station names of one line

public partial class datum_holiday_fleet_day : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:113
    public override object greet() => I18n.T("This day commemorates another year of successful survival aboard the Mothic Grand Nomad Fleet. Moths galaxywide are encouraged to eat, drink, and be merry.");

    // tg: code/modules/holiday/holidays.dm:116
    public override object get_station_name() => pick("Moth", "Fleet", "Nomadic");
}

public partial class datum_holiday_groundhog : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:126
    public override object get_station_name() => pick("Deja Vu");
}

public partial class datum_holiday_nz : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:141
    public override object get_station_name() => pick("Aotearoa", "Kiwi", "Fish 'n' Chips", "Kākāpō", "Southern Cross");
}

public partial class datum_holiday_valentines : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:162
    public override object get_station_name() => pick("Love", "Amore", "Single", "Smootch", "Hug");
}

public partial class datum_holiday_random_kindness : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:215
    public override object greet() => I18n.T("Go do some random acts of kindness for a stranger!");
}

public partial class datum_holiday_pi : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:246
    public override object get_station_name() => pick("Sine", "Cosine", "Tangent", "Secant", "Cosecant", "Cotangent");
}

public partial class datum_holiday_no_this_is_patrick : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:266
    public override object get_station_name() => pick("Blarney", "Green", "Leprechaun", "Booze");

    // tg: code/modules/holiday/holidays.dm:269
    public override object greet() => I18n.T("Happy National Inebriation Day!");
}

public partial class datum_holiday_spess : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:299
    public override object greet() => I18n.T("On this day over 600 years ago, Comrade Yuri Gagarin first ventured into space!");
}

public partial class datum_holiday_fourtwenty : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:314
    public override object get_station_name() => pick("Snoop", "Blunt", "Toke", "Dank", "Cheech", "Chong");
}

public partial class datum_holiday_tea : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:323
    public override object get_station_name() => pick("Crumpet", "Assam", "Oolong", "Pu-erh", "Sweet Tea", "Green", "Black");
}

public partial class datum_holiday_anz : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:338
    public override object get_station_name() => pick("Australian", "New Zealand", "Poppy", "Southern Cross");
}

public partial class datum_holiday_chernobyl : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:353
    public override object greet() => I18n.T("On this day in 1986, the Chernobyl nuclear power plant melted down, causing one of the worst nuclear disasters in human history. Today serves as a reminder to the lives lost and to the rigorous safety standards our engineers must adhere to when providing power to the station.");
}

public partial class datum_holiday_draconic_day : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:376
    public override object greet() => I18n.T("On this day, Lizardkind celebrates their language with literature and other cultural works.");

    // tg: code/modules/holiday/holidays.dm:379
    public override object get_station_name() => pick("Draconic", "Literature", "Reading");
}

public partial class datum_holiday_firefighter : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:389
    public override object get_station_name() => pick("Burning", "Blazing", "Plasma", "Fire");
}

public partial class datum_holiday_bee : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:403
    public override object get_station_name() => pick("Bee", "Honey", "Hive", "Africanized", "Mead", "Buzz");
}

public partial class datum_holiday_goth : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:420
    public override object get_station_name() => pick("Goth", "Sanguine", "Tenebris", "Lacrimosa", "Umbra", "Noctis");
}

public partial class datum_holiday_atrakor_festival : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:436
    public override object greet() => I18n.T("On this day, the Lizards traditionally celebrate the Festival of Atrakor's Might, where they honour the moon god with lavishly adorned clothing, large portions of food, and a massive celebration into the night.");

    // tg: code/modules/holiday/holidays.dm:439
    public override object get_station_name() => pick("Moon", "Night Sky", "Celebration");
}

public partial class datum_holiday_ufo : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:523
    public override object get_station_name() => pick("Ayy", "Truth", "Tsoukalos", "Mulder", "Scully");
}

public partial class datum_holiday_usa : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:542
    public override object get_station_name() => pick("Independent", "American", "Burger", "Bald Eagle", "Star-Spangled", "Fireworks");
}

public partial class datum_holiday_france : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:565
    public override object get_station_name() => pick("Francais", "Fromage", "Zut", "Merde", "Sacrebleu");

    // tg: code/modules/holiday/holidays.dm:568
    public override object greet() => I18n.T("Do you hear the people sing?");
}

public partial class datum_holiday_hotdogday : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:577
    public override object greet() => I18n.T("Happy National Hot Dog Day!");
}

public partial class datum_holiday_wizards_day : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:587
    public override object get_station_name() => pick("Dungeon", "Elf", "Magic", "D20", "Edition");
}

public partial class datum_holiday_indigenous : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:606
    public override object get_station_name() => pick("Endangered language", "Word", "Language", "Language revitalization", "Potato", "Corn");
}

public partial class datum_holiday_ukraine : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:617
    public override object get_station_name() => pick("Kyiv", "Ukraine");
}

public partial class datum_holiday_tiziran_unification : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:630
    public override object greet() => I18n.T("On this day over 400 years ago, Lizardkind first united under a single banner, ready to face the stars as one unified people.");

    // tg: code/modules/holiday/holidays.dm:633
    public override object get_station_name() => pick("Tizira", "Lizard", "Imperial");
}

public partial class datum_holiday_ianbirthday : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:652
    public override object greet() => I18n.T("Happy birthday, Ian!");

    // tg: code/modules/holiday/holidays.dm:655
    public override object get_station_name() => pick("Ian", "Corgi", "Erro");
}

public partial class datum_holiday_pirate : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:668
    public override object greet() => I18n.T("Ye be talkin' like a pirate today or else ye'r walkin' tha plank, matey!");

    // tg: code/modules/holiday/holidays.dm:671
    public override object get_station_name() => pick("Yarr", "Scurvy", "Yo-ho-ho");
}

public partial class datum_holiday_animal : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:689
    public override object get_station_name() => pick("Parrot", "Corgi", "Cat", "Pug", "Goat", "Fox");
}

public partial class datum_holiday_un_day : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:710
    public override object greet() => I18n.T("On this day in 1945, the United Nations was founded, laying the foundation for humanity's united government!");

    // tg: code/modules/holiday/holidays.dm:713
    public override object get_station_name() => pick("United", "Cooperation", "Humanitarian");
}

public partial class datum_holiday_halloween : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:728
    public override object greet() => I18n.T("Have a spooky Halloween!");

    // tg: code/modules/holiday/holidays.dm:731
    public override object get_station_name() => pick("Bone-Rattling", "Mr. Bones' Own", "2SPOOKY", "Spooky", "Scary", "Skeletons");
}

public partial class datum_holiday_vegan : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:742
    public override object get_station_name() => pick("Tofu", "Tempeh", "Seitan", "Tofurkey");
}

public partial class datum_holiday_october_revolution : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:756
    public override object get_station_name() => pick("Communist", "Soviet", "Bolshevik", "Socialist", "Red", "Workers'");
}

public partial class datum_holiday_remembrance_day : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:770
    public override object greet() => I18n.T("Lest we forget.");

    // tg: code/modules/holiday/holidays.dm:773
    public override object get_station_name() => pick("Peace", "Armistice", "Poppy");
}

public partial class datum_holiday_lifeday : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:781
    public override object get_station_name() => pick("Itchy", "Lumpy", "Malla", "Kazook");
}

public partial class datum_holiday_holy_lights : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:826
    public override object greet() => I18n.T("The Festival of Holy Lights is the final day of the Ethereal calendar. It is typically a day of prayer followed by celebration to close out the year in style.");

    // tg: code/modules/holiday/holidays.dm:829
    public override object get_station_name() => pick("Ethereal", "Lantern", "Holy");
}

public partial class datum_holiday_festive_season : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:841
    public override object greet() => I18n.T("Have a nice festive season!");
}

public partial class datum_holiday_xmas : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:902
    public override object greet() => I18n.T("Have a merry Christmas!");
}

public partial class datum_holiday_new_year : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:946
    public override object get_station_name() => pick("Party", "New", "Hangover", "Resolution", "Auld");
}

public partial class datum_holiday_friday_thirteenth : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:959
    public override object get_station_name() => pick("Mike", "Friday", "Evil", "Myers", "Murder", "Deathly", "Stabby");
}

public partial class datum_holiday_programmers : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:976
    public override object get_station_name() => pick("span>", "DEBUG: ", "null", "/list", "EVENT PREFIX NOT FOUND");
}

public partial class datum_holiday_islamic_ramadan : datum_holiday_islamic
{
    // tg: code/modules/holiday/holidays.dm:994
    public override object get_station_name() => pick("Haram", "Halaal", "Jihad", "Muslim", "Al", "Mohammad", "Rashidun", "Umayyad", "Abbasid", "Abdul", "Fatimid", "Ayyubid", "Almohad", "Abu");
}

public partial class datum_holiday_hebrew_hanukkah : datum_holiday_hebrew
{
    // tg: code/modules/holiday/holidays.dm:1022
    public override object get_station_name() => pick("Dreidel", "Menorah", "Latkes", "Gelt");
}

public partial class datum_holiday_hebrew_passover : datum_holiday_hebrew
{
    // tg: code/modules/holiday/holidays.dm:1031
    public override object get_station_name() => pick("Matzah", "Moses", "Red Sea");
}

public partial class datum_holiday_easter : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:1088
    public override object greet() => I18n.T("Greetings! Have a Happy Easter and keep an eye out for Easter Bunnies!");

    // tg: code/modules/holiday/holidays.dm:1091
    public override object get_station_name() => pick("Fluffy", "Bunny", "Easter", "Egg");
}

// The holidays' procs of more than one line

// tg: code/modules/holiday/holidays.dm:144
public partial class datum_holiday_nz : datum_holiday
{
    public override object greet()
    {
        float nz_age = Dm.num(text2num(time2text(world.timeofday, "YYYY", TIMEZONE_NZST))) - 1840;
        return I18n.T("On this day [] years ago, New Zealand's Treaty of Waitangi, the founding document of the nation, was signed!", nz_age);
    }
}

// tg: code/modules/holiday/holidays.dm:180
public partial class datum_holiday_birthday : datum_holiday
{
    public override object greet()
    {
        float game_age = Dm.num(text2num(time2text(world.timeofday, "YYYY", world.timezone))) - 2003;
        string Fact = null;
        switch ((int)game_age)
        {
            case 16: Fact = I18n.T(" SS13 is now old enough to drive!"); break;
            case 18: Fact = I18n.T(" SS13 is now legal!"); break;
            case 21: Fact = I18n.T(" SS13 can now drink!"); break;
            case 26: Fact = I18n.T(" SS13 can now rent a car!"); break;
            case 30: Fact = I18n.T(" SS13 can now go home and be a family man!"); break;
            case 35: Fact = I18n.T(" SS13 can now run for President of the United States!"); break;
            case 40: Fact = I18n.T(" SS13 can now suffer a midlife crisis!"); break;
            case 50: Fact = I18n.T(" Happy golden anniversary!"); break;
            case 65: Fact = I18n.T(" SS13 can now start thinking about retirement!"); break;
        }
        if (!Dm.truthy(Fact))
            Fact = I18n.T(" SS13 is now [] years old!", game_age);

        return I18n.T("Say 'Happy Birthday' to Space Station 13, first publicly playable on February 16th, 2003![]", Fact);
    }
}

// tg: code/modules/holiday/holidays.dm:285
public partial class datum_holiday_april_fools : datum_holiday
{
    public override object celebrate()
    {
        object dot = base.celebrate();
        SSjob.set_overflow_role(typeof(datum_job_clown));
        SSticker.set_lobby_music(Dm.resource("sound/music/lobby_music/clown.ogg"), @override: TRUE);
        return dot;
    }

    // tg: code/modules/holiday/holidays.dm:290
    public override object get_holiday_colors(atom thing_to_color = null, object pattern = null) => Dm.text("#[]", random_short_color());
}

// tg: code/modules/holiday/holidays.dm:347
public partial class datum_holiday_chernobyl : datum_holiday
{
    public override object get_station_name()
    {
        if (prob(3))
            return "Not Great, Not Terrible";
        return pick("Atomic", "Nuclear", "Radiation", "Plutonium", "Uranium", "Corium", "Zirconium", "Graphite", "Scram", "Explosion");
    }
}

// tg: code/modules/holiday/holidays.dm:484
public partial class datum_holiday_pride_week : datum_holiday
{
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (prob(30))
            return;
        holiday_colors = pick(LESBIAN_FLAG_COLORS(), GAY_MAN_FLAG_COLORS(), TRANS_FLAG_COLORS(), BI_FLAG_COLORS(), ACE_FLAG_COLORS(), PAN_FLAG_COLORS()) as DmList;
    }

    // tg: code/modules/holiday/holidays.dm:490
    public override object get_station_prefix()
    {
        if (prob(10))
            return pick("Gay", "Trans", "Bi", "Lesbian", "Pan", "Ace", "Aro", "Nonbinary", "Enby", "Asexual", "Aromantic", "Agender", "Intersex");
        return pick("Rainbow", "Pride", "Queer");
    }

    // tg: code/modules/holiday/holidays.dm:495
    public override object get_station_name() => "";
}

// tg: code/modules/holiday/holidays.dm:596
public partial class datum_holiday_friendship : datum_holiday
{
    public override object greet() => I18n.T("Have a magical []!", name);
}

// tg: code/modules/holiday/holidays.dm:679
public partial class datum_holiday_questions : datum_holiday
{
    public override object greet() => I18n.T("Are you having a happy []?", name);
}

// tg: code/modules/holiday/holidays.dm:812
public partial class datum_holiday_hello : datum_holiday
{
    public override object greet() => I18n.T("[]! []", pick(Dm.list(I18n.T("Aloha"), I18n.T("Bonjour"), I18n.T("Hello"), I18n.T("Hi"), I18n.T("Greetings"), I18n.T("Salutations"),
        I18n.T("Bienvenidos"), I18n.T("Hola"), I18n.T("Howdy"), I18n.T("Ni hao"), I18n.T("Guten Tag"), I18n.T("Konnichiwa"), I18n.T("G'day cunt"))), base.greet());
}

// tg: code/modules/holiday/holidays.dm:855
public partial class datum_holiday_monkey : datum_holiday
{
    public override object celebrate()
    {
        object dot = base.celebrate();
        SSstation.setup_trait(typeof(datum_station_trait_job_pun_pun)); // tg: :857  (gaps R2: Pun Pun is a Crewmember)
        return dot;
    }
}

// tg: code/modules/holiday/holidays.dm:863
public partial class datum_holiday_xmas : datum_holiday
{
    // tg: code/modules/holiday/holidays.dm:882
    public override object get_station_name() => pick(
        "Bible",
        "Birthday",
        "Chimney",
        "Claus",
        "Crucifixion",
        "Elf",
        "Fir",
        "Ho Ho Ho",
        "Jesus",
        "Jolly",
        "Merry",
        "Present",
        "Sack",
        "Santa",
        "Sleigh",
        "Yule");

    // tg: code/modules/holiday/holidays.dm:905
    public override object celebrate()
    {
        object dot = base.celebrate();
        SSticker.OnRoundstart(Dm.@new<datum_callback>(this, nameof(roundstart_celebrate)));
        DmList xmas_loot = Dm.list(
            Dm.kv(typeof(obj_item_clothing_head_costume_santa), 1),
            Dm.kv(typeof(obj_item_gift_anything), 1),
            Dm.kv(typeof(obj_item_toy_xmas_cracker), 3));
        var entry = Dm.list();
        entry[xmas_loot] = maint_holiday_weight;
        Dm.append(ref GLOB.maintenance_loot, entry);
        return dot;
    }

    // tg: code/modules/holiday/holidays.dm:916
    public virtual void roundstart_celebrate()
    {
        foreach (obj_machinery_computer_security_telescreen_entertainment Monitor in Dm.each_cast<obj_machinery_computer_security_telescreen_entertainment>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_security_telescreen_entertainment))))
            Monitor.icon_state_on = "entertainment_xmas";

        foreach (mob_living_basic_pet_dog_corgi_ian Ian in Dm.each<mob_living_basic_pet_dog_corgi_ian>(GLOB.mob_living_list))
            Waiting.NoSleep(Ian.place_on_head(Dm.@new<obj_item_clothing_head_helmet_space_santahat>(Ian)));

        var pack = SSshuttle.supply_packs[typeof(datum_supply_pack_costumes_toys_christmas)] as datum_supply_pack;
        pack.order_flags = Dm.bitor(pack.order_flags, ORDER_SPECIAL_ENABLED);
    }
}

// MOVING DATES

// tg: code/modules/holiday/holidays.dm:954
public partial class datum_holiday_friday_thirteenth : datum_holiday
{
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        if (Dm.num(dd) == 13 && Dm.eq(ddd, FRIDAY))
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/holiday/holidays.dm:966
public partial class datum_holiday_programmers : datum_holiday
{
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null) //Programmer's day falls on the 2^8th day of the year
    {
        float y = Dm.num(yyyy);
        if (Dm.num(mm) == 9)
        {
            if (y / 4 == round(y / 4)) //Note: Won't work right on September 12th, 2200 (at least it's a Friday!)
            {
                if (Dm.num(dd) == 12)
                    return TRUE;
            }
            else
            {
                if (Dm.num(dd) == 13)
                    return TRUE;
            }
        }
        return FALSE;
    }
}

// ISLAMIC

// tg: code/modules/holiday/holidays.dm:984
public partial class datum_holiday_islamic : datum_holiday
{
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        var cal = Dm.@new<datum_foreign_calendar_islamic>(yyyy, mm, dd);
        return base.shouldCelebrate(cal.dd, cal.mm, cal.yyyy, ddd);
    }
}

// HEBREW

// tg: code/modules/holiday/holidays.dm:1008
public partial class datum_holiday_hebrew : datum_holiday
{
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        var cal = Dm.@new<datum_foreign_calendar_hebrew>(yyyy, mm, dd);
        return base.shouldCelebrate(cal.dd, cal.mm, cal.yyyy, ddd);
    }
}

// tg: code/modules/holiday/holidays.dm:1019
public partial class datum_holiday_hebrew_hanukkah : datum_holiday_hebrew
{
    public override object greet() => I18n.T("Happy []!", pick(I18n.T("Hanukkah"), I18n.T("Chanukah")));
}

// EASTER (this having its own spot should be understandable)

// tg: code/modules/holiday/holidays.dm:1037
public partial class datum_holiday_easter : datum_holiday
{

    // tg: code/modules/holiday/holidays.dm:1054
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        if (!Dm.truthy(begin_month))
        {
            current_year = Dm.num(text2num(time2text(world.timeofday, "YYYY", world.timezone)));
            DmList easterResults = EasterDate(current_year + year_offset);

            begin_day = Dm.num(easterResults["day"]);
            begin_month = Dm.num(easterResults["month"]);

            end_day = begin_day + days_extra;
            end_month = begin_month;
            if (end_day >= 32 && end_month == MARCH) //begins in march, ends in april
            {
                end_day -= 31;
                end_month++;
            }
            if (end_day >= 31 && end_month == APRIL) //begins in april, ends in june
            {
                end_day -= 30;
                end_month++;
            }

            begin_day -= days_early;
            if (begin_day <= 0)
            {
                if (begin_month == APRIL)
                {
                    begin_day += 31;
                    begin_month--; //begins in march, ends in april
                }
            }
        }
        return base.shouldCelebrate(dd, mm, yyyy, ddd);
    }

    // tg: code/modules/holiday/holidays.dm:1079
    public override object celebrate()
    {
        object dot = base.celebrate();
        DmList easter_loot = Dm.list(
            Dm.kv(typeof(obj_item_surprise_egg), 15),
            Dm.kv(typeof(obj_item_storage_basket_easter), 15));
        var entry = Dm.list();
        entry[easter_loot] = maint_holiday_weight;
        Dm.append(ref GLOB.maintenance_loot, entry);
        return dot;
    }
}

public static partial class GlobalProcs
{
    /// Takes a holiday datum, a starting month, ending month, max amount of days to test in, and min/max year as input
    /// Returns a list in the form list("yyyy/m/d", ...) representing all days the holiday runs on in the tested range
    // tg: code/modules/holiday/holidays.dm:1096
    public static DmList poll_holiday(Type path = null, float min_month = 0, float max_month = 0, float min_year = 0, float max_year = 0, float max_day = 0)
    {
        var deets = Dm.list();
        for (float year = min_year; year <= max_year; year++)
        {
            for (float month = min_month; month <= max_month; month++)
            {
                for (float day = 1; day <= max_day; day++)
                {
                    var new_day = (datum_holiday)Dm.@new(path);
                    if (Dm.truthy(new_day.shouldCelebrate(day, month, year, iso_to_weekday(day_of_month(year, month, day)))))
                        Dm.append(deets, Dm.text("[]/[]/[]", year, month, day));
                }
            }
        }
        return deets;
    }

    /// Does the same as [/proc/poll_holiday], but prints the output to admins instead of returning it
    // tg: code/modules/holiday/holidays.dm:1107
    public static void print_holiday(Type path = null, float min_month = 0, float max_month = 0, float min_year = 0, float max_year = 0, float max_day = 0)
    {
        DmList deets = poll_holiday(path, min_month, max_month, min_year, max_year, max_day);
        message_admins(Dm.text("The accepted dates for [] in the input range []-[]/[]-[]/1-[] are []", path, min_year, max_year, min_month, max_month, max_day, deets.Join("\n")));
    }
}
