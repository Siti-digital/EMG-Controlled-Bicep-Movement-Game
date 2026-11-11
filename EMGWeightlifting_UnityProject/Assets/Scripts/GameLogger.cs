using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class GameLogger : MonoBehaviour
{
    public ArmController arm;
    public UDPReceiver receiver;
    public string subjectId = "S1";

    StreamWriter writer;
    string mode, level, input, filter, currentRoundId;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    string LogDir
    {
        get
        {
            string dir = Path.Combine(Application.dataPath, "..", "GameLogs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public void BeginRound(string modeName, string levelName)
    {
        EndRound();
        mode = modeName;
        level = levelName;
        // Conditions are fixed for the whole round
        input = arm.useKeyboard ? "KEY" : "EMG";
        filter = arm.useKeyboard ? "NA" : receiver.FilterTag;
        currentRoundId = $"{subjectId}_{mode}_{level}_{input}_{filter}_{DateTime.Now:yyyyMMdd_HHmmss_fff}";

        writer = new StreamWriter(Path.Combine(LogDir, currentRoundId + ".csv"));
        writer.WriteLine("unix_time,round_time,mode,level,input,filter,activation,elbow_angle,target_angle,in_zone,scoring,sender_time");
    }

    public void Log(float roundTime, float target, bool inZone, bool scoring)
    {
        if (writer == null) return;
        double unix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        writer.WriteLine(string.Join(",",
            unix.ToString("F3", CI),
            roundTime.ToString("F3", CI),
            mode, level, input, filter,
            arm.CurrentActivation.ToString("F4", CI),
            arm.CurrentAngle.ToString("F2", CI),
            target.ToString("F1", CI),
            inZone ? 1 : 0,
            scoring ? 1 : 0,
            receiver.LastSenderTime.ToString("F3", CI)));
    }

    public void EndRound()
    {
        if (writer == null) return;
        writer.Flush();
        writer.Close();
        writer = null;
    }

    public void WriteSummary(string modeName, string levelName, float inZonePct, int reps, int repAttempts)
    {
        string path = Path.Combine(LogDir, "summary.csv");
        bool newFile = !File.Exists(path);
        using (var sw = new StreamWriter(path, true))
        {
            if (newFile)
            {
                sw.WriteLine("date_time,subject,round_id,mode,level,input,filter,time_in_zone_pct,reps,rep_attempts");
            }
            sw.WriteLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), subjectId,
                currentRoundId,
                modeName, levelName, input, filter,
                inZonePct.ToString("F1", CI), reps, repAttempts));
        }
    }

    void OnDestroy()
    {
        EndRound();
    }
}
