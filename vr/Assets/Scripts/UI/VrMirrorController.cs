using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public static class VrMirrorController
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        // 헤드셋 쪽 렌더링은 그대로 두고,
        // Game 뷰 / PC 윈도우에 XR 미러링만 끔
        XRSettings.gameViewRenderMode = GameViewRenderMode.None;
    }
}
