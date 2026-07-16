-- AeroMind IQ seed data: synthetic manufacturing production data with one injected anomaly.

CREATE TABLE production_runs (
    run_id                SERIAL PRIMARY KEY,
    line_id               INT NOT NULL,
    shift                 TEXT NOT NULL,
    started_at            TIMESTAMPTZ NOT NULL,
    units_produced        INT NOT NULL,
    units_defective       INT NOT NULL,
    yield_pct             NUMERIC(5, 2) NOT NULL,
    cycle_time_sec        NUMERIC(6, 2) NOT NULL,
    temperature_variance  NUMERIC(5, 2) NOT NULL
);

CREATE INDEX idx_production_runs_line_started ON production_runs (line_id, started_at);

-- 30 days of hourly synthetic rows for 3 production lines: yield hovering ~96-99%,
-- cycle time ~43-47s, temperature variance ~1.0-2.0 (a stable, unremarkable process).
INSERT INTO production_runs (line_id, shift, started_at, units_produced, units_defective, yield_pct, cycle_time_sec, temperature_variance)
SELECT
    line_id,
    CASE
        WHEN EXTRACT(HOUR FROM hour_slot)::INT BETWEEN 0 AND 7 THEN 'night'
        WHEN EXTRACT(HOUR FROM hour_slot)::INT BETWEEN 8 AND 15 THEN 'day'
        ELSE 'evening'
    END AS shift,
    hour_slot AS started_at,
    units_produced,
    ROUND(units_produced * (1 - yield_pct / 100.0))::INT AS units_defective,
    yield_pct,
    cycle_time_sec,
    temperature_variance
FROM (
    SELECT
        line_id,
        hour_slot,
        (500 + FLOOR(RANDOM() * 120))::INT AS units_produced,
        ROUND((97.5 + (RANDOM() - 0.5) * 3.0)::NUMERIC, 2) AS yield_pct,
        ROUND((45.0 + (RANDOM() - 0.5) * 4.0)::NUMERIC, 2) AS cycle_time_sec,
        ROUND((1.5 + RANDOM() * 1.0)::NUMERIC, 2) AS temperature_variance
    FROM generate_series(1, 3) AS line_id
    CROSS JOIN generate_series(
        NOW() - INTERVAL '30 days',
        NOW() - INTERVAL '1 hour',
        INTERVAL '1 hour'
    ) AS hour_slot
) AS baseline;

-- Injected anomaly: line 2 suffers a sharp yield collapse in the most recent 3 hours,
-- accompanied by a cycle-time slowdown and a temperature-variance spike — a genuinely
-- multi-dimensional signature (e.g. a cooling-system fault causing both slower cycles and
-- more defects), not just a single-column yield drop. Well beyond 3 standard deviations
-- on yield alone, so both the 3-sigma check and the Isolation Forest have something to
-- flag, and the Reporter has richer evidence to reason about.
UPDATE production_runs
SET
    yield_pct = ROUND((68.0 + (RANDOM() - 0.5) * 4.0)::NUMERIC, 2),
    units_defective = ROUND(units_produced * (1 - (68.0 + (RANDOM() - 0.5) * 4.0) / 100.0))::INT,
    cycle_time_sec = ROUND((80.0 + (RANDOM() - 0.5) * 10.0)::NUMERIC, 2),
    temperature_variance = ROUND((8.0 + (RANDOM() - 0.5) * 2.0)::NUMERIC, 2)
WHERE line_id = 2
  AND started_at >= NOW() - INTERVAL '4 hours'
  AND started_at < NOW() - INTERVAL '1 hour';

-- Read-only role for Agent B (the Fetcher). Defense-in-depth: even if prompt-level
-- guardrails failed, this DB role physically cannot write.
CREATE ROLE aeromind_reader LOGIN PASSWORD 'aeromind_reader_pw';
GRANT CONNECT ON DATABASE aeromindiq TO aeromind_reader;
GRANT USAGE ON SCHEMA public TO aeromind_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO aeromind_reader;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO aeromind_reader;
