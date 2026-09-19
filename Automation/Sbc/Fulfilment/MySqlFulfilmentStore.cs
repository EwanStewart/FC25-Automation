using MySql.Data.MySqlClient;

namespace Automation.Sbc.Fulfilment;

public sealed class MySqlFulfilmentStore : IFulfilmentStore
{
    private const string GAP_COLUMNS =
        "slot_index, position, specification, quality, min_rating, max_rating, nation_id, league_id, club_id, rare_flag, estimated_cost, card_ceiling, outcome, simulated, searched, candidates_seen, trade_id, item_id, asset_id, bid_amount, final_price, detail";

    private readonly MySqlAccess access_;

    public MySqlFulfilmentStore()
        : this(Database.Connection)
    {
    }

    public MySqlFulfilmentStore(string connectionString)
    {
        access_ = new MySqlAccess(connectionString);
    }

    public int Queue(int approvalId, int challengeId, bool dryRun, long estimatedCost,
        IReadOnlyList<GapRecord> gaps)
    {
        const string query =
            "INSERT INTO SbcFulfilments (approval_id, state, dry_run, spend_ceiling, estimated_cost) VALUES (@approval, 'pending', @dry, @ceiling, @cost) AS incoming ON DUPLICATE KEY UPDATE state = 'pending', dry_run = incoming.dry_run, spend_ceiling = incoming.spend_ceiling, estimated_cost = incoming.estimated_cost; SELECT id FROM SbcFulfilments WHERE approval_id = @approval;";
        var id = access_.Scalar(query, new Dictionary<string, object>
        {
            ["@approval"] = approvalId,
            ["@dry"] = dryRun,
            ["@ceiling"] = SpendLedger.CeilingFor(estimatedCost),
            ["@cost"] = estimatedCost
        });

        foreach (var gap in gaps) SaveGap(id, gap);

        Remember(id, challengeId);

        return id;
    }

    public IReadOnlyList<FulfilmentRun> Queued()
    {
        const string query =
            "SELECT f.id, f.approval_id, a.challenge_id, f.state, f.dry_run, f.spend_ceiling, f.estimated_cost, f.detail FROM SbcFulfilments f JOIN SbcApprovals a ON a.id = f.approval_id WHERE f.state IN ('pending', 'placing', 'buying') ORDER BY f.id";

        return access_.Read(query, [], reader => new FulfilmentRun(reader.GetInt32(0), reader.GetInt32(1),
            reader.GetInt32(2), State(reader.GetString(3)), reader.GetBoolean(4), reader.GetInt64(5),
            reader.GetInt64(6), reader.GetString(7)));
    }

    public IReadOnlyList<GapRecord> Gaps(int fulfilmentId)
    {
        var query = $"SELECT {GAP_COLUMNS} FROM SbcFulfilmentGaps WHERE fulfilment_id = @id ORDER BY slot_index";

        return access_.Read(query, new Dictionary<string, object> { ["@id"] = fulfilmentId }, ReadGap);
    }

    public IReadOnlyList<PlacementRecord> Placements(int fulfilmentId)
    {
        const string query =
            "SELECT slot_index, position, source, club_player_id, player_name, outcome, simulated, detail FROM SbcFulfilmentPlacements WHERE fulfilment_id = @id ORDER BY slot_index";

        return access_.Read(query, new Dictionary<string, object> { ["@id"] = fulfilmentId },
            reader => new PlacementRecord(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3), reader.GetString(4),
                Enum.Parse<PlacementOutcome>(reader.GetString(5), true), reader.GetBoolean(6),
                reader.GetString(7)));
    }

    public void SaveGap(int fulfilmentId, GapRecord gap)
    {
        const string query =
            "INSERT INTO SbcFulfilmentGaps (fulfilment_id, slot_index, position, specification, quality, min_rating, max_rating, nation_id, league_id, club_id, rare_flag, estimated_cost, card_ceiling, outcome, simulated, searched, candidates_seen, trade_id, item_id, asset_id, bid_amount, final_price, detail, attempted_at, resolved_at) VALUES (@fulfilment, @slot, @position, @specification, @quality, @minimum, @maximum, @nation, @league, @club, @rare, @estimate, @ceiling, @outcome, @simulated, @searched, @seen, @trade, @item, @asset, @bid, @final, @detail, @attempted, @resolved) AS incoming ON DUPLICATE KEY UPDATE outcome = incoming.outcome, simulated = incoming.simulated, searched = incoming.searched, candidates_seen = incoming.candidates_seen, trade_id = incoming.trade_id, item_id = incoming.item_id, asset_id = incoming.asset_id, bid_amount = incoming.bid_amount, final_price = incoming.final_price, detail = incoming.detail, card_ceiling = incoming.card_ceiling, attempted_at = COALESCE(SbcFulfilmentGaps.attempted_at, incoming.attempted_at), resolved_at = incoming.resolved_at";

        access_.Execute(query, GapParameters(fulfilmentId, gap));
    }

    public void SavePlacement(int fulfilmentId, PlacementRecord placement)
    {
        const string query =
            "INSERT INTO SbcFulfilmentPlacements (fulfilment_id, slot_index, position, source, club_player_id, player_name, outcome, simulated, detail) VALUES (@fulfilment, @slot, @position, @source, @club, @name, @outcome, @simulated, @detail) AS incoming ON DUPLICATE KEY UPDATE source = incoming.source, club_player_id = incoming.club_player_id, player_name = incoming.player_name, outcome = incoming.outcome, simulated = incoming.simulated, detail = incoming.detail";

        access_.Execute(query, new Dictionary<string, object>
        {
            ["@fulfilment"] = fulfilmentId,
            ["@slot"] = placement.SlotIndex,
            ["@position"] = placement.Position,
            ["@source"] = placement.Source,
            ["@club"] = placement.ClubPlayerId.HasValue ? placement.ClubPlayerId.Value : DBNull.Value,
            ["@name"] = Trimmed(placement.PlayerName, 255),
            ["@outcome"] = placement.Outcome.ToString().ToLowerInvariant(),
            ["@simulated"] = placement.Simulated,
            ["@detail"] = Trimmed(placement.Detail, 512)
        });
    }

    public void SaveState(int fulfilmentId, FulfilmentState state, string detail)
    {
        const string query = "UPDATE SbcFulfilments SET state = @state, detail = @detail WHERE id = @id";

        access_.Execute(query, new Dictionary<string, object>
        {
            ["@id"] = fulfilmentId,
            ["@state"] = state.ToString().ToLowerInvariant(),
            ["@detail"] = Trimmed(detail, 512)
        });
    }

    private void Remember(int fulfilmentId, int challengeId)
    {
        const string query = "UPDATE SbcFulfilments SET detail = @detail WHERE id = @id AND detail = ''";

        access_.Execute(query, new Dictionary<string, object>
        {
            ["@id"] = fulfilmentId,
            ["@detail"] = $"queued for challenge {challengeId}"
        });
    }

    private static Dictionary<string, object> GapParameters(int fulfilmentId, GapRecord gap)
    {
        return new Dictionary<string, object>
        {
            ["@fulfilment"] = fulfilmentId,
            ["@slot"] = gap.SlotIndex,
            ["@position"] = gap.Position,
            ["@specification"] = Trimmed(gap.Specification.Describe(), 255),
            ["@quality"] = gap.Specification.Quality.ToString(),
            ["@minimum"] = gap.Specification.MinimumRating,
            ["@maximum"] = gap.Specification.MaximumRating,
            ["@nation"] = Optional(gap.Specification.NationId),
            ["@league"] = Optional(gap.Specification.LeagueId),
            ["@club"] = Optional(gap.Specification.ClubId),
            ["@rare"] = Optional(gap.Specification.RareFlag),
            ["@estimate"] = gap.Specification.EstimatedCost,
            ["@ceiling"] = gap.CardCeiling,
            ["@outcome"] = gap.Outcome.ToString().ToLowerInvariant(),
            ["@simulated"] = gap.Simulated,
            ["@searched"] = Trimmed(gap.Searched, 255),
            ["@seen"] = gap.CandidatesSeen,
            ["@trade"] = gap.TradeId is null ? DBNull.Value : gap.TradeId,
            ["@item"] = Optional(gap.ItemId),
            ["@asset"] = Optional(gap.AssetId),
            ["@bid"] = Optional(gap.BidAmount),
            ["@final"] = Optional(gap.FinalPrice),
            ["@detail"] = Trimmed(gap.Detail, 512),
            ["@attempted"] = gap.BidAmount.HasValue ? DateTime.UtcNow : DBNull.Value,
            ["@resolved"] = GapProgress.Settled(gap.Outcome) ? DateTime.UtcNow : DBNull.Value
        };
    }

    private static GapRecord ReadGap(MySqlDataReader reader)
    {
        MarketSpecification specification = new(reader.GetString(1), Enum.Parse<PlayerQuality>(reader.GetString(3),
                true), reader.GetInt32(4), reader.GetInt32(5), Number(reader, 6), Number(reader, 7),
            Number(reader, 8), Number(reader, 9), reader.GetInt32(10));

        return new GapRecord(reader.GetInt32(0), reader.GetString(1), specification, reader.GetInt32(11),
            Enum.Parse<GapOutcome>(reader.GetString(12), true), reader.GetBoolean(13), reader.GetString(14),
            reader.GetInt32(15), reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetInt64(17), Number(reader, 18), Number(reader, 19),
            Number(reader, 20), reader.GetString(21));
    }

    private static int? Number(MySqlDataReader reader, int column)
    {
        return reader.IsDBNull(column) ? null : reader.GetInt32(column);
    }

    private static object Optional(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object Optional(long? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static string Trimmed(string text, int length)
    {
        return text.Length > length ? text[..length] : text;
    }

    private static FulfilmentState State(string name)
    {
        return Enum.Parse<FulfilmentState>(name, true);
    }
}
