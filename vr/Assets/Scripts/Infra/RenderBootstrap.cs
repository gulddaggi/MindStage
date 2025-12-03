using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RenderBootstrap
{
    // 여기에 제외할 씬 이름을 넣어두면 됨
    static readonly HashSet<string> Excluded = new HashSet<string>
    {
        "Interview"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var s = SceneManager.GetActiveScene();
        if (!IsExcluded(s))
        {
            ApplyGlobalEnv();
            ApplyToAllCameras(s);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (IsExcluded(scene)) return;

        ApplyGlobalEnv();
        ApplyToAllCameras(scene);
    }

    static bool IsExcluded(Scene scene)
    {
        if (!scene.IsValid()) return false;

        // 정확히 일치 or 접두어(Interview*) 모두 제외
        if (Excluded.Contains(scene.name)) return true;
        //if (scene.name.StartsWith("Interview")) return true;
        return false;
    }

    // 해당 "씬"의 카메라만 처리 (DontDestroyOnLoad에 있는 건 건드리지 않음)
    static void ApplyToAllCameras(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
        }
    }

    // Skybox/환경광/반사 등을 검정으로
    static void ApplyGlobalEnv()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;
    }
}