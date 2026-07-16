"""Trains an Isolation Forest on a clean historical baseline of production_runs data.

Run manually whenever you want to (re)train the model — this is an offline/periodic
step, not something the scoring service or the .NET app triggers automatically:

    python train.py

Reads the DB connection string from the DATABASE_URL env var, falling back to the same
local Postgres the .NET app uses by default (aeromind_admin, since training needs to see
all rows including ones outside the read-only role's normal query patterns).
"""

import os

import joblib
import pandas as pd
import psycopg2
from sklearn.ensemble import IsolationForest
from sklearn.preprocessing import StandardScaler

FEATURES = ["cycle_time_sec", "temperature_variance", "units_produced", "yield_pct"]
MODEL_DIR = os.path.join(os.path.dirname(__file__), "model")

DEFAULT_DATABASE_URL = "postgresql://aeromind_admin:aeromind_admin_pw@localhost:5432/aeromindiq"


def load_baseline() -> pd.DataFrame:
    database_url = os.environ.get("DATABASE_URL", DEFAULT_DATABASE_URL)
    query = f"""
        SELECT {", ".join(FEATURES)}
        FROM production_runs
        WHERE started_at < NOW() - INTERVAL '4 hours'
    """
    with psycopg2.connect(database_url) as conn:
        return pd.read_sql(query, conn)


def main() -> None:
    df = load_baseline()
    if len(df) < 50:
        raise SystemExit(
            f"Only {len(df)} baseline rows found (excluding the most recent 4 hours) — "
            "need more history before training."
        )

    scaler = StandardScaler()
    features_scaled = scaler.fit_transform(df[FEATURES])

    model = IsolationForest(n_estimators=200, contamination="auto", random_state=42)
    model.fit(features_scaled)

    os.makedirs(MODEL_DIR, exist_ok=True)
    joblib.dump(scaler, os.path.join(MODEL_DIR, "scaler.joblib"))
    joblib.dump(model, os.path.join(MODEL_DIR, "isolation_forest.joblib"))

    print(f"Trained on {len(df)} baseline rows. Model written to {MODEL_DIR}/")


if __name__ == "__main__":
    main()
