CREATE DATABASE IF NOT EXISTS fc25
CHARACTER SET utf8mb4
COLLATE utf8mb4_general_ci;

USE fc25;

CREATE TABLE IF NOT EXISTS SbcFulfilments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    approval_id INT NOT NULL,
    state VARCHAR(16) NOT NULL DEFAULT 'pending',
    dry_run TINYINT(1) NOT NULL DEFAULT 1,
    spend_ceiling BIGINT NOT NULL,
    estimated_cost BIGINT NOT NULL,
    detail VARCHAR(512) NOT NULL DEFAULT '',
    queued_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY SbcFulfilmentsApproval (approval_id),
    INDEX SbcFulfilmentsState (state)
);

CREATE TABLE IF NOT EXISTS SbcFulfilmentGaps (
    id INT AUTO_INCREMENT PRIMARY KEY,
    fulfilment_id INT NOT NULL,
    slot_index INT NOT NULL,
    position VARCHAR(8) NOT NULL,
    specification VARCHAR(255) NOT NULL,
    quality VARCHAR(8) NOT NULL,
    min_rating INT NOT NULL,
    max_rating INT NOT NULL,
    nation_id INT NULL,
    league_id INT NULL,
    club_id INT NULL,
    rare_flag INT NULL,
    estimated_cost INT NOT NULL,
    card_ceiling INT NOT NULL,
    outcome VARCHAR(24) NOT NULL DEFAULT 'pending',
    simulated TINYINT(1) NOT NULL DEFAULT 1,
    searched VARCHAR(255) NOT NULL DEFAULT '',
    candidates_seen INT NOT NULL DEFAULT 0,
    trade_id VARCHAR(32) NULL,
    item_id BIGINT NULL,
    asset_id INT NULL,
    bid_amount INT NULL,
    final_price INT NULL,
    detail VARCHAR(512) NOT NULL DEFAULT '',
    attempted_at TIMESTAMP NULL,
    resolved_at TIMESTAMP NULL,
    UNIQUE KEY SbcFulfilmentGapsSlot (fulfilment_id, slot_index),
    INDEX SbcFulfilmentGapsOutcome (outcome),
    INDEX SbcFulfilmentGapsTrade (trade_id)
);

CREATE TABLE IF NOT EXISTS SbcFulfilmentPlacements (
    id INT AUTO_INCREMENT PRIMARY KEY,
    fulfilment_id INT NOT NULL,
    slot_index INT NOT NULL,
    position VARCHAR(8) NOT NULL,
    source VARCHAR(16) NOT NULL,
    club_player_id BIGINT NULL,
    player_name VARCHAR(255) NOT NULL,
    outcome VARCHAR(24) NOT NULL DEFAULT 'pending',
    simulated TINYINT(1) NOT NULL DEFAULT 1,
    detail VARCHAR(512) NOT NULL DEFAULT '',
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY SbcFulfilmentPlacementsSlot (fulfilment_id, slot_index)
);
