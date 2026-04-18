using System;
using System.IO;
using UnityEngine;

namespace RRX.Core
{
    /// <summary>Minimal JSON Lines log to persistentDataPath (Phase 1 — no cloud).</summary>
    public sealed class SessionTelemetry
    {
        readonly string _sessionId = Guid.NewGuid().ToString("N");
        readonly string _build;
        readonly object _sync = new object();

        StreamWriter _writer;

        public SessionTelemetry()
        {
            _build = Application.version;
        }

        public void Begin()
        {
            lock (_sync)
            {
                try
                {
                    var dir = Application.persistentDataPath;
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, $"rrx_session_{_sessionId}.jsonl");
                    _writer = new StreamWriter(path, append: false) { AutoFlush = true };
                    Write(new TelemetryEvent(
                        _sessionId,
                        _build,
                        Time.realtimeSinceStartup,
                        -1,
                        ScenarioState.Arrival,
                        ScenarioAction.None,
                        "session_start",
                        0));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RRX] Telemetry init failed: {e.Message}");
                }
            }
        }

        public void End()
        {
            lock (_sync)
            {
                try
                {
                    _writer?.Dispose();
                    _writer = null;
                }
                catch { /* ignored */ }
            }
        }

        public void Write(in TelemetryEvent evt)
        {
            lock (_sync)
            {
                if (_writer == null) return;
                try
                {
                    var json = JsonUtility.ToJson(evt);
                    _writer.WriteLine(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RRX] Telemetry write failed: {e.Message}");
                }
            }
        }

        public string SessionId => _sessionId;
    }

    [Serializable]
    public struct TelemetryEvent
    {
        public string session_id;
        public string build;
        public float ts_ms;
        public int checkpoint_idx;
        public ScenarioState node_id;
        public ScenarioAction action_id;
        public string result;
        public int rewind_count;

        public TelemetryEvent(
            string sessionId,
            string build,
            float realtimeSinceStartup,
            int checkpointIdx,
            ScenarioState node,
            ScenarioAction action,
            string result,
            int rewindCount)
        {
            session_id = sessionId;
            this.build = build;
            ts_ms = realtimeSinceStartup * 1000f;
            checkpoint_idx = checkpointIdx;
            node_id = node;
            action_id = action;
            this.result = result;
            rewind_count = rewindCount;
        }
    }
}
