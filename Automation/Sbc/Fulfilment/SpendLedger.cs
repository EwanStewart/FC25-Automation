namespace Automation.Sbc.Fulfilment;

public sealed class SpendLedger
{
    public const int CEILING_UPLIFT_PERCENT = 50;

    private readonly long ceiling_;
    private readonly Dictionary<int, long> committed_ = new();
    private readonly Dictionary<int, long> permitted_ = new();

    public SpendLedger(long ceiling, IEnumerable<(int Slot, long Amount)> standing)
    {
        ceiling_ = ceiling;

        foreach (var (slot, amount) in standing) committed_[slot] = amount;
    }

    public long Ceiling => Math.Max(ceiling_, permitted_.Values.Sum());

    public long Approved => ceiling_;

    public long Committed => committed_.Values.Sum();

    public long Remaining => Math.Max(0, Ceiling - Committed);

    public static long CeilingFor(long estimatedCost)
    {
        return estimatedCost + estimatedCost * CEILING_UPLIFT_PERCENT / 100;
    }

    public long Standing(int slot)
    {
        return committed_.GetValueOrDefault(slot);
    }

    public bool Allows(int slot, long amount)
    {
        return Committed - Standing(slot) + amount <= Ceiling;
    }

    public void Permit(int slot, long ceiling)
    {
        permitted_[slot] = ceiling;
    }

    public void Commit(int slot, long amount)
    {
        committed_[slot] = amount;
    }
}
