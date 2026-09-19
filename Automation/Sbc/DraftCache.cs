using System.Text.Json;

namespace Automation.Sbc;

public sealed record CachedDraft(DraftedChallenge Drafted, DateTime SolvedAt);

public sealed class DraftCache
{
    private static readonly JsonSerializerOptions Format = new() { IncludeFields = true };

    private readonly MySqlAccess access_;

    public DraftCache()
        : this(Database.Connection)
    {
    }

    public DraftCache(string connectionString)
    {
        access_ = new MySqlAccess(connectionString);
    }

    public IReadOnlyList<CachedDraft> Read()
    {
        const string query = "SELECT challenge_id, solved_at, payload FROM SbcDrafts ORDER BY challenge_id";

        return access_.Read(query, [], reader => Revive(reader.GetDateTime(1), reader.GetString(2)))
            .Where(cached => cached != null)
            .Select(cached => cached!)
            .ToList();
    }

    public void Invalidate()
    {
        access_.Execute("DELETE FROM SbcDrafts", []);
    }

    public void Write(IReadOnlyList<DraftedChallenge> drafted, DateTime solvedAt)
    {
        access_.Execute("DELETE FROM SbcDrafts", []);

        foreach (var draft in drafted)
            access_.Execute(
                "INSERT INTO SbcDrafts (challenge_id, solved_at, payload) VALUES (@challenge, @solved, @payload)",
                new Dictionary<string, object>
                {
                    ["@challenge"] = draft.Challenge.ChallengeId,
                    ["@solved"] = solvedAt,
                    ["@payload"] = JsonSerializer.Serialize(draft, Format)
                });
    }

    private static CachedDraft? Revive(DateTime solvedAt, string payload)
    {
        CachedDraft? result = null;

        try
        {
            var drafted = JsonSerializer.Deserialize<DraftedChallenge>(payload, Format);

            if (drafted != null) result = new CachedDraft(drafted, solvedAt);
        }
        catch (JsonException)
        {
            result = null;
        }

        return result;
    }
}
