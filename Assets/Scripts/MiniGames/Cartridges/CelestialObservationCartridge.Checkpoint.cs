using UnityEngine;

namespace MiniGames
{
    public sealed partial class CelestialObservationCartridge
    {
        // Restore the compact completed result, never transient calibration/controller state.
        private void RestoreRecordingPhase()
        {
            _stage = ObservationStage.ReadyToRecord;
            _instrumentCenter01 = _pendingObservation.instrumentCenter01;
            _zoom = _pendingObservation.instrumentZoom;
            _bearingControlDegrees = _pendingObservation.bearingControlDegrees;
            _initialRotationErrorDegrees = _pendingObservation.instrumentRotationDegrees - _bearingControlDegrees;
            _plateControl = _plateSolution;
            _lensControl = _lensSolution;
            _prismControl = _prismSolution;
            _patternObjects.Clear();
            _patternIds.Clear();
            _traceIds.Clear();
            foreach (string id in _pendingObservation.patternObjectStableIds)
            {
                var p = FindProjected(id);
                if (p == null) continue;
                _patternObjects.Add(p);
                _patternIds.Add(id);
                _traceIds.Add(id);
            }
            BuildDecoderBasePoints();
            SetStatus("Completed observation restored. Load paper to record it, or recalibrate to abandon it.", 4f);
        }
    }
}
