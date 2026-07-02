namespace InfraAis.Validation;
using System.Globalization;
public class AisValidator
{
    public static bool IsValidMmsi(long mmsi)
    {
        return mmsi >= 100_000_000 && mmsi <= 999_999_999;
    }

    public static bool IsValidPosition(double lat, double lon)
    {
        return lat >= -90 && lat <= 90
            && lon >= -180 && lon <= 180;
    }
    public static decimal? NormalizeSog(double sog)
    {
        return sog == 102.3 ? null : (decimal)sog;
    }

    public static decimal? NormalizeCog(double cog)
    {
        return cog == 360 ? null : (decimal)cog;
    }


    public static short? NormalizeHeading(int heading)
    {
        return heading == 511 ? null : (short)heading;
    }
    public static bool IsValidImo(int imo)
    {
        if (imo < 1_000_000 || imo > 9_999_999)
            return false;
        int sum = 0;
        int weight = 2;
        int remaining = imo / 10;
        for (int i = 0; i < 6; i++)
        {
            int digit = remaining % 10;
            sum += digit * weight;
            remaining /= 10;
            weight++;
        }

        int checkDigit = imo % 10;
        return sum % 10 == checkDigit;
    }
    public static bool TryParseTimestamp(string raw, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;


        var cleaned = raw.Replace(" UTC", "").Trim();


        cleaned = TrimFraction(cleaned);


        return DateTime.TryParseExact(
            cleaned,
            "yyyy-MM-dd HH:mm:ss.FFFFFFF zzz",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out utc);
    }


    private static string TrimFraction(string s)
    {
        int dot = s.IndexOf('.');
        if (dot == -1) return s;
        int end = dot + 1;
        while (end < s.Length && char.IsDigit(s[end]))
            end++;

        int fracLen = end - (dot + 1);
        if (fracLen <= 7) return s;


        return s.Substring(0, dot + 1 + 7) + s.Substring(end);
    }
    public static bool IsNotFutureSkewed(DateTime utc, int maxSkewMinutes)
    {
        return utc <= DateTime.UtcNow.AddMinutes(maxSkewMinutes);
    }
}