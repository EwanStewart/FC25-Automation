USE fc25;

CREATE TABLE IF NOT EXISTS Players (
    definition_id INT NOT NULL,
    resource_id BIGINT NULL,
    name VARCHAR(255) NOT NULL,
    common_name VARCHAR(255) NULL,
    rating INT NULL,
    preferred_position VARCHAR(8) NULL,
    alternate_positions VARCHAR(64) NULL,
    club_id INT NULL,
    league_id INT NULL,
    nation_id INT NULL,
    rarity_id INT NULL,
    card_colour VARCHAR(8) NULL,
    last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (definition_id),
    INDEX idx_players_resource (resource_id),
    INDEX idx_players_rating (rating),
    INDEX idx_players_club (club_id),
    INDEX idx_players_league (league_id),
    INDEX idx_players_nation (nation_id),
    INDEX idx_players_position (preferred_position),
    INDEX idx_players_name (name)
);

CREATE TABLE IF NOT EXISTS CatalogueImports (
    id INT AUTO_INCREMENT PRIMARY KEY,
    source VARCHAR(32) NOT NULL,
    item_count INT NOT NULL DEFAULT 0,
    last_page INT NOT NULL DEFAULT 0,
    page_total INT NULL,
    outcome VARCHAR(16) NOT NULL DEFAULT 'running',
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    finished_at TIMESTAMP NULL,
    INDEX idx_catalogue_imports_source (source, id),
    INDEX idx_catalogue_imports_outcome (outcome)
);
