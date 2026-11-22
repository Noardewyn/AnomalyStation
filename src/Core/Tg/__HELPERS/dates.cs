// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/dates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file (the leap year and Easter's date, the holidays').

//Curse you calenders...
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/dates.dm:2
    public static bool IsLeapYear(float y) => (y % 4 == 0 && (y % 100 != 0 || y % 400 == 0));

    //Y, eg: 2017, 2018, 2019, in num form (not string)
    //etc. Between 1583 and 4099
    //Adapted from a free algorithm written in BASIC (https://www.assa.org.au/edm#Computer)
    // tg: code/__HELPERS/dates.dm:8
    public static DmList EasterDate(float y)
    {
        float FirstDig, Remain19, temp; //Intermediate Results
        float tA, tB, tC, tD, tE; //Table A-E results
        float d, m; //Day and Month returned

        FirstDig = round(y / 100);
        Remain19 = y % 19;

        temp = round((FirstDig - 15) / 2) + 202 - 11 * Remain19;

        switch ((int)FirstDig)
        {
            case 21: case 24: case 25: case 27: case 28: case 29: case 30: case 31: case 32: case 34: case 35: case 38:
                temp -= 1;
                break;
            case 33: case 36: case 37: case 39: case 40:
                temp -= 2;
                break;
        }
        temp %= 30;

        tA = temp + 21;
        if (temp == 29)
            tA -= 1;
        if (temp == 28 && (Remain19 > 10))
            tA -= 1;
        tB = (tA - 19) % 7;

        tC = (40 - FirstDig) % 4;
        if (tC == 3)
            tC += 1;
        if (tC > 1)
            tC += 1;
        temp = y % 100;
        tD = (temp + round(temp / 4)) % 7;

        tE = ((20 - tB - tC - tD) % 7) + 1;
        d = tA + tE;
        if (d > 31)
        {
            d -= 31;
            m = 4;
        }
        else
            m = 3;
        return Dm.list(Dm.kv("day", d), Dm.kv("month", m));
    }
}
