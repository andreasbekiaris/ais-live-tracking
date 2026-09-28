using System.Globalization;
using System.Text;
using InfraAis.Models;
namespace InfraAis.Utils;

public static class CursorCodec
{
    private const char Separator = '|';

    public static string Encode(PositionCursor cursor)
    {
        DateTime timestamp = cursor.Timestamp;
        string textTimestamp = timestamp.ToString("o", CultureInfo.InvariantCulture);
        string encodedTimestamp = Convert.ToBase64String(Encoding.UTF8.GetBytes(textTimestamp));

        long id = cursor.Id;
        string textId = id.ToString();
        string encodedId = Convert.ToBase64String(Encoding.UTF8.GetBytes(textId));
        string encoded = string.Concat(encodedTimestamp, Separator, encodedId);

        return encoded;
    }

    public static PositionCursor? Decode(string encoded)
    {
       

        string[] parts = encoded.Split(Separator);
        if (parts.Length != 2) return null;

        byte[] timestampBytes;
        byte[] idBytes;

        try
        {
            timestampBytes = Convert.FromBase64String(parts[0]);
            idBytes = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        string timestampString = Encoding.UTF8.GetString(timestampBytes);
        string idString = Encoding.UTF8.GetString(idBytes);

        if (!DateTime.TryParse(timestampString, CultureInfo.InvariantCulture,
                               DateTimeStyles.RoundtripKind, out DateTime timestamp))
        {
            return null;
        }

        if (!long.TryParse(idString, out long id))
        {
            return null;
        }

        return new PositionCursor
        {
            Timestamp = timestamp,
            Id = id
        };
    }
}