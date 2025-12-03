/************************************************************************************
Filename    :   OVRLipSyncContextMorphTarget_Dual.cs
Content     :   Bridges Oculus LipSync visemes to TWO skinned meshes (e.g., head + teeth)
************************************************************************************/
using UnityEngine;
using System.Linq;

public class OVRLipSyncContextMorphTarget : MonoBehaviour
{
    [Header("Targets (assign in Inspector)")]
    [Tooltip("Primary SkinnedMeshRenderer (e.g., head)")]
    public SkinnedMeshRenderer skinnedMeshRendererA = null;

    [Tooltip("Secondary SkinnedMeshRenderer (optional, e.g., teeth)")]
    public SkinnedMeshRenderer skinnedMeshRendererB = null;

    [Header("Viseme → BlendShape Indices")]
    [Tooltip("For renderer A: BlendShape index per viseme (-1 = not used)")]
    public int[] visemeToBlendTargetsA =
        Enumerable.Repeat(-1, OVRLipSync.VisemeCount).ToArray();

    [Tooltip("For renderer B: BlendShape index per viseme (-1 = not used)")]
    public int[] visemeToBlendTargetsB =
        Enumerable.Repeat(-1, OVRLipSync.VisemeCount).ToArray();

    [Header("Laughter")]
    [Tooltip("Renderer A laughter BlendShape index (-1 = not used)")]
    public int laughterBlendTargetA = -1;

    [Tooltip("Renderer B laughter BlendShape index (-1 = not used)")]
    public int laughterBlendTargetB = -1;

    [Range(0.0f, 1.0f)]
    [Tooltip("Laughter probability threshold above which the laughter blendshape will be activated")]
    public float laughterThreshold = 0.5f;

    [Range(0.0f, 3.0f)]
    [Tooltip("Laughter animation linear multiplier, the final output will be clamped to 1.0")]
    public float laughterMultiplier = 1.5f;

    [Header("Debug / Smoothing")]
    [Tooltip("Enable using the test keys below to manually trigger each viseme.")]
    public bool enableVisemeTestKeys = false;

    [Tooltip("Test keys used to manually trigger an individual viseme - QWERTY row")]
    public KeyCode[] visemeTestKeys =
    {
        KeyCode.BackQuote,
        KeyCode.Tab,
        KeyCode.Q,
        KeyCode.W,
        KeyCode.E,
        KeyCode.R,
        KeyCode.T,
        KeyCode.Y,
        KeyCode.U,
        KeyCode.I,
        KeyCode.O,
        KeyCode.P,
        KeyCode.LeftBracket,
        KeyCode.RightBracket,
        KeyCode.Backslash,
    };

    [Tooltip("Test key used to manually trigger laughter and visualise the results")]
    public KeyCode laughterKey = KeyCode.CapsLock;

    [Range(1, 100)]
    [Tooltip("Smoothing of 1 = only current predicted viseme, 100 = very smooth")]
    public int smoothAmount = 70;

    // PRIVATE
    private OVRLipSyncContextBase lipsyncContext = null;

    void Start()
    {
        if (skinnedMeshRendererA == null && skinnedMeshRendererB == null)
        {
            Debug.LogError("[OVRLipSyncContextMorphTarget] Assign at least one SkinnedMeshRenderer.");
            enabled = false;
            return;
        }

        lipsyncContext = GetComponent<OVRLipSyncContextBase>();
        if (lipsyncContext == null)
        {
            Debug.LogError("[OVRLipSyncContextMorphTarget] No OVRLipSyncContext component on this object!");
            enabled = false;
            return;
        }

        lipsyncContext.Smoothing = smoothAmount;

        // 배열 길이 방어
        if (visemeToBlendTargetsA == null || visemeToBlendTargetsA.Length != OVRLipSync.VisemeCount)
            visemeToBlendTargetsA = Enumerable.Repeat(-1, OVRLipSync.VisemeCount).ToArray();
        if (visemeToBlendTargetsB == null || visemeToBlendTargetsB.Length != OVRLipSync.VisemeCount)
            visemeToBlendTargetsB = Enumerable.Repeat(-1, OVRLipSync.VisemeCount).ToArray();
    }

    void Update()
    {
        if (lipsyncContext == null) return;

        OVRLipSync.Frame frame = lipsyncContext.GetCurrentPhonemeFrame();
        if (frame != null)
        {
            // Viseme → blendshapes
            ApplyVisemes(skinnedMeshRendererA, visemeToBlendTargetsA, frame);
            ApplyVisemes(skinnedMeshRendererB, visemeToBlendTargetsB, frame);

            // Laughter → blendshape
            ApplyLaughter(skinnedMeshRendererA, laughterBlendTargetA, frame);
            ApplyLaughter(skinnedMeshRendererB, laughterBlendTargetB, frame);
        }

        if (enableVisemeTestKeys)
            CheckForKeys(frame);

        if (smoothAmount != lipsyncContext.Smoothing)
            lipsyncContext.Smoothing = smoothAmount;
    }

    // --- Helpers --------------------------------------------------------------

    void ApplyVisemes(SkinnedMeshRenderer smr, int[] map, OVRLipSync.Frame frame)
    {
        if (smr == null || map == null) return;
        var mesh = smr.sharedMesh;
        if (mesh == null) return;

        int bsCount = mesh.blendShapeCount;
        int len = Mathf.Min(map.Length, frame.Visemes.Length);

        for (int i = 0; i < len; i++)
        {
            int idx = map[i];
            if (idx >= 0 && idx < bsCount)
            {
                smr.SetBlendShapeWeight(idx, frame.Visemes[i] * 100f);
            }
        }
    }

    void ApplyLaughter(SkinnedMeshRenderer smr, int laughterIndex, OVRLipSync.Frame frame)
    {
        if (smr == null || laughterIndex < 0) return;
        var mesh = smr.sharedMesh;
        if (mesh == null) return;
        if (laughterIndex >= mesh.blendShapeCount) return;

        float score = frame.laughterScore; // [0..1]
        score = score < laughterThreshold ? 0f : score - laughterThreshold;
        score = Mathf.Min(score * laughterMultiplier, 1f);
        score *= (1f / Mathf.Max(0.0001f, laughterThreshold));

        smr.SetBlendShapeWeight(laughterIndex, score * 100f);
    }

    void CheckForKeys(OVRLipSync.Frame frame)
    {
        // 단순 디버그: 키 입력으로 viseme 강제 (렌더러 A/B 모두에 반영)
        for (int i = 0; i < Mathf.Min(visemeTestKeys.Length, OVRLipSync.VisemeCount); ++i)
        {
            if (Input.GetKeyDown(visemeTestKeys[i]))
                lipsyncContext.SetVisemeBlend(i, 100);
            if (Input.GetKeyUp(visemeTestKeys[i]))
                lipsyncContext.SetVisemeBlend(i, 0);
        }

        if (Input.GetKeyDown(laughterKey))
            lipsyncContext.SetLaughterBlend(100);
        if (Input.GetKeyUp(laughterKey))
            lipsyncContext.SetLaughterBlend(0);
    }
}
