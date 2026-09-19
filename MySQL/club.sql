CREATE DATABASE IF NOT EXISTS fc25
CHARACTER SET utf8mb4
COLLATE utf8mb4_general_ci;

USE fc25;

CREATE TABLE IF NOT EXISTS ClubPlayers (
    id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    asset_id INT NOT NULL,
    resource_id INT NOT NULL,
    rating INT NOT NULL,
    preferred_position VARCHAR(8) NOT NULL,
    possible_positions VARCHAR(64) NOT NULL,
    team_id INT NOT NULL,
    league_id INT NOT NULL,
    nation INT NOT NULL,
    rare_flag INT NOT NULL,
    card_sub_type_id INT NOT NULL,
    untradeable TINYINT(1) NOT NULL,
    item_state VARCHAR(16) NOT NULL,
    pile INT NOT NULL,
    market_average INT NOT NULL,
    captured_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX ClubPlayersRating (rating),
    INDEX ClubPlayersTeam (team_id),
    INDEX ClubPlayersLeague (league_id),
    INDEX ClubPlayersNation (nation),
    INDEX ClubPlayersPosition (preferred_position),
    INDEX ClubPlayersUntradeable (untradeable),
    INDEX ClubPlayersAsset (asset_id),
    INDEX ClubPlayersResource (resource_id)
);

CREATE TABLE IF NOT EXISTS ClubSnapshots (
    id INT AUTO_INCREMENT PRIMARY KEY,
    item_count INT NOT NULL,
    outcome VARCHAR(24) NOT NULL,
    detail VARCHAR(255) NULL,
    captured_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
