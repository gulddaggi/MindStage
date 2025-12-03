using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

namespace App.Infra
{
    [Serializable]
    public class LocalSettings
    {
        // Mic
        public string micDevice;
        public int micSampleRate = 48000;

        // Watch
        public string watchInstallationId;
        public string watchSerial;

        // Speaker
        public string speakerDevice = "System Default"; // 자리표시
        public float speakerVolume = 1f;
    }

    public static class LocalSettingsStore
    {
        static string FilePath =>
            Path.Combine(Application.persistentDataPath, "settings.json");

        public static LocalSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<LocalSettings>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Debug.LogWarning($"[Settings] Load fail: {e}"); }
            return new LocalSettings();
        }

        public static void Save(LocalSettings s)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(s, true));
#if UNITY_EDITOR
                Debug.Log($"[Settings] saved -> {FilePath}");
#endif
            }
            catch (Exception e) { Debug.LogWarning($"[Settings] Save fail: {e}"); }
        }
    }
}
