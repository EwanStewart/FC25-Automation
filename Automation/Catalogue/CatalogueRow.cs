namespace Automation.Catalogue;

public static class CatalogueRow
{
    private const string POSITION_SEPARATOR = ",";

    public static string? AlternatePositions(PlayerRecord player)
    {
        var joined = string.Join(POSITION_SEPARATOR, player.AlternatePositions);
        var result = joined.Length > 0 ? joined : null;

        return result;
    }
}
