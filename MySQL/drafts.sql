USE fc25;

CREATE TABLE IF NOT EXISTS SbcDrafts (
    challenge_id INT NOT NULL,
    solved_at DATETIME NOT NULL,
    payload LONGTEXT NOT NULL,
    PRIMARY KEY (challenge_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
