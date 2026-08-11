namespace InfraAis.Utils;

public static class NavStatusMapper
{
    public static string ToText(int? navStatus) => navStatus switch
    {
        0 => "Under way using engine",
        1 => "At anchor",
        2 => "Not under command",
        3 => "Restricted manoeuvrability",
        4 => "Constrained by her draught",
        5 => "Moored",
        6 => "Aground",
        7 => "Engaged in fishing",
        8 => "Under way sailing",
        14 => "AIS-SART active",
        15 => "Not defined",
        null => "Unknown",
        _ => "Reserved"
    };
}
