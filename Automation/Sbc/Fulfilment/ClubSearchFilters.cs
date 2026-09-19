namespace Automation.Sbc.Fulfilment;

public sealed record FilterControl(int Index, string Label, string Image, bool Clearable);

public static class ClubSearchFilters
{
    public const int NOTHING = -1;

    private const string POSITION_MARK = "/positions/";

    public static int PositionControl(IReadOnlyList<FilterControl> controls)
    {
        var wanted = Wanted(controls);

        if (wanted is not null) ForbiddenControls.Require(wanted.Label);

        return wanted?.Index ?? NOTHING;
    }

    private static FilterControl? Wanted(IReadOnlyList<FilterControl> controls)
    {
        return controls.FirstOrDefault(control => control.Clearable &&
            control.Image.Contains(POSITION_MARK, StringComparison.OrdinalIgnoreCase));
    }
}
