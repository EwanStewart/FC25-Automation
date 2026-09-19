namespace Automation.Sbc.Fulfilment;

public enum FulfilmentMode
{
    Dry,
    PlaceLive,
    Live
}

public static class FulfilmentModes
{
    public static FulfilmentMode Chosen(bool fulfilLive, bool placeLive)
    {
        var result = FulfilmentMode.Dry;

        if (fulfilLive) result = FulfilmentMode.Live;
        else if (placeLive) result = FulfilmentMode.PlaceLive;

        return result;
    }

    public static FulfilmentMode Stored(bool dryRun)
    {
        return dryRun ? FulfilmentMode.Dry : FulfilmentMode.Live;
    }

    public static string Simulation(FulfilmentMode mode)
    {
        return mode == FulfilmentMode.Dry ? "dry run" : "simulated buying";
    }

    public static string Describe(FulfilmentMode mode)
    {
        return $"buying {Word(mode == FulfilmentMode.Live)}, placement {Word(mode != FulfilmentMode.Dry)}";
    }

    private static string Word(bool live)
    {
        return live ? "live" : "simulated";
    }
}
