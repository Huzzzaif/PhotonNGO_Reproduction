using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MigrationLab
{
    public static class LabLog
    {
        public static string Run = "unassigned";
        public static string Scenario = "exploration";
        public static string FilePath { get; private set; }
        private static StreamWriter writer;
        private static readonly string process = Guid.NewGuid().ToString("N").Substring(0, 8);

        private static string Quote(object value) => "\"" + (value == null ? "" :
            Convert.ToString(value, CultureInfo.InvariantCulture)).Replace("\"", "\"\"") + "\"";

        public static void Write(string eventName, string detail = "", bool? pass = null)
        {
            try
            {
                if (writer == null)
                {
                    string directory = Path.Combine(Application.persistentDataPath, "MigrationLabLogs");
                    Directory.CreateDirectory(directory);
                    FilePath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + process + ".csv");
                    writer = new StreamWriter(FilePath, false) { AutoFlush = true };
                    writer.WriteLine("utc,elapsed_seconds,run,scenario,process,role,client_id,scene,network_stop,applied_stop,anchor_error_m,event,pass,detail,shared_count");
                    Debug.Log("Migration Lab log: " + FilePath);
                }
                var b = LabBootstrap.Instance;
                var n = b == null ? null : b.Manager;
                var s = LabSession.Instance;
                string role = n == null || !n.IsListening ? "offline" : n.IsHost ? "host" : "client";
                object[] fields = { DateTime.UtcNow.ToString("o"), Time.realtimeSinceStartupAsDouble,
                    Run, Scenario, process, role, n == null ? "" : n.LocalClientId.ToString(),
                    SceneManager.GetActiveScene().name, s == null ? -1 : s.Stop.Value,
                    b == null ? -1 : b.AppliedStop, b == null ? -1 : b.AnchorError,
                    eventName, pass.HasValue ? (pass.Value ? "1" : "0") : "", detail,
                    s == null ? -1 : s.InteractionCount.Value };
                writer.WriteLine(string.Join(",", Array.ConvertAll(fields, Quote)));
            }
            catch (Exception e) { Debug.LogError("Cannot write test record: " + e.Message); }
        }

        public static void Close()
        {
            if (writer == null) return;
            writer.Dispose(); writer = null;
        }
    }
}
