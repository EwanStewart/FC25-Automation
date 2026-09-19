CREATE DATABASE IF NOT EXISTS fc25
CHARACTER SET utf8mb4
COLLATE utf8mb4_general_ci;

USE fc25;

CREATE TABLE IF NOT EXISTS SbcChallenges (
    challenge_id INT NOT NULL PRIMARY KEY,
    set_id INT NOT NULL,
    name VARCHAR(128) NOT NULL,
    formation VARCHAR(16) NOT NULL,
    requirements TEXT NOT NULL,
    captured_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX SbcChallengesSet (set_id)
);

CREATE TABLE IF NOT EXISTS SbcApprovals (
    id INT AUTO_INCREMENT PRIMARY KEY,
    challenge_id INT NOT NULL,
    challenge_name VARCHAR(128) NOT NULL,
    formation VARCHAR(16) NOT NULL,
    squad_rating INT NOT NULL,
    chemistry INT NOT NULL,
    chemistry_estimated TINYINT(1) NOT NULL DEFAULT 1,
    purchase_count INT NOT NULL,
    estimated_cost BIGINT NOT NULL,
    state VARCHAR(16) NOT NULL DEFAULT 'approved',
    approved_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX SbcApprovalsChallenge (challenge_id),
    INDEX SbcApprovalsState (state)
);

CREATE TABLE IF NOT EXISTS SbcApprovalSlots (
    id INT AUTO_INCREMENT PRIMARY KEY,
    approval_id INT NOT NULL,
    slot_index INT NOT NULL,
    position VARCHAR(8) NOT NULL,
    club_player_id BIGINT NULL,
    player_name VARCHAR(255) NOT NULL,
    rating INT NOT NULL,
    owned TINYINT(1) NOT NULL,
    chemistry INT NOT NULL,
    specification VARCHAR(255) NULL,
    estimated_cost INT NOT NULL,
    INDEX SbcApprovalSlotsApproval (approval_id)
);
