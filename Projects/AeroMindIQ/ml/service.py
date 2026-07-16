"""FastAPI scoring service wrapping the trained Isolation Forest.

Run with:
    uvicorn service:app --port 8500

The .NET Auditor POSTs a batch of recent production_runs feature rows to /score and
gets back, per row, whether the Isolation Forest flags it as a multi-dimensional
outlier (model.predict() == -1) plus the raw decision_function() score.
"""

import os
from contextlib import asynccontextmanager

import joblib
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

FEATURES = ["cycle_time_sec", "temperature_variance", "units_produced", "yield_pct"]
MODEL_DIR = os.path.join(os.path.dirname(__file__), "model")

_state: dict = {"scaler": None, "model": None}


@asynccontextmanager
async def lifespan(_: FastAPI):
    scaler_path = os.path.join(MODEL_DIR, "scaler.joblib")
    model_path = os.path.join(MODEL_DIR, "isolation_forest.joblib")
    if not (os.path.exists(scaler_path) and os.path.exists(model_path)):
        raise RuntimeError(
            "No trained model found. Run `python train.py` before starting the service."
        )
    _state["scaler"] = joblib.load(scaler_path)
    _state["model"] = joblib.load(model_path)
    yield
    _state.clear()


app = FastAPI(title="AeroMind IQ — Isolation Forest scoring service", lifespan=lifespan)


class ProductionRow(BaseModel):
    line_id: int
    started_at: str
    cycle_time_sec: float
    temperature_variance: float
    units_produced: float
    yield_pct: float


class ScoredRow(BaseModel):
    line_id: int
    started_at: str
    is_anomaly: bool
    score: float


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "model_loaded": _state["model"] is not None}


@app.post("/score", response_model=list[ScoredRow])
def score(rows: list[ProductionRow]) -> list[ScoredRow]:
    model = _state["model"]
    scaler = _state["scaler"]
    if model is None or scaler is None:
        raise HTTPException(status_code=503, detail="Model not loaded")
    if not rows:
        return []

    features = [[getattr(row, f) for f in FEATURES] for row in rows]
    features_scaled = scaler.transform(features)

    predictions = model.predict(features_scaled)
    scores = model.decision_function(features_scaled)

    return [
        ScoredRow(
            line_id=row.line_id,
            started_at=row.started_at,
            is_anomaly=bool(pred == -1),
            score=float(s),
        )
        for row, pred, s in zip(rows, predictions, scores)
    ]
