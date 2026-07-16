# AeroMind IQ — ML scoring service

Trains and serves a scikit-learn Isolation Forest that flags multi-dimensional
production anomalies (cycle time, temperature variance, units produced, yield) —
catching combinations that no single-column threshold would.

This is the one part of AeroMind IQ that isn't .NET: the rest of the app is C#/Semantic
Kernel, but the Isolation Forest genuinely needs scikit-learn, so **Python 3 is a
required prerequisite** to run the full pipeline from this milestone on.

## Setup

    cd ml
    python3 -m venv .venv
    source .venv/bin/activate
    pip install -r requirements.txt

## Train

Run whenever you want to (re)train against the current database contents:

    python train.py

Trains on all rows older than the most recent 4 hours (so it never trains on the seeded
anomaly window itself), and writes `model/scaler.joblib` + `model/isolation_forest.joblib`.
These are gitignored — regenerate them locally, don't commit them.

## Serve

    uvicorn service:app --port 8500

`POST /score` accepts a JSON array of production rows and returns, per row, `is_anomaly`
and the raw Isolation Forest decision score (more negative = more anomalous). The .NET
Auditor calls this once per cycle; if the service is unreachable it automatically falls
back to the original 3-sigma yield check instead of failing the whole cycle.
