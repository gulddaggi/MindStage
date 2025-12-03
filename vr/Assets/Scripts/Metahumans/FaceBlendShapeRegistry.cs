using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FaceBlendshapeRegistry : MonoBehaviour
{
    [Tooltip("블렌드셰이프를 스캔할 루트. 비우면 자신의 Transform 기준으로 스캔")]
    public Transform scanRoot;

    // "jawOpen", "browInner", "smile", "boo", "woo", "eyeBlink_L", "eyeBlink_R" ...
    private readonly Dictionary<string, List<(SkinnedMeshRenderer smr, int index)>> _map
        = new Dictionary<string, List<(SkinnedMeshRenderer, int)>>();

    // 현재 값을 캐시(부드러운 Lerp에 사용)
    private readonly Dictionary<string, float> _current = new Dictionary<string, float>();

    void Awake()
    {
        if (scanRoot == null) scanRoot = transform;
        BuildMap();
    }

    private void BuildMap()
    {
        _map.Clear();
        var smrs = scanRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null) continue;
            int count = mesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string full = mesh.GetBlendShapeName(i);
                // 마지막 '.' 뒤를 키로 사용 (없으면 전체)
                int dot = full.LastIndexOf('.');
                string key = (dot >= 0 && dot < full.Length - 1) ? full.Substring(dot + 1) : full;

                if (!_map.TryGetValue(key, out var list))
                {
                    list = new List<(SkinnedMeshRenderer, int)>();
                    _map[key] = list;
                }
                list.Add((smr, i));
                if (!_current.ContainsKey(key)) _current[key] = smr.GetBlendShapeWeight(i);
            }
        }
    }

    /// <summary> 같은 키를 가진 모든 메시의 블렌드셰이프를 동일 값으로 세팅 </summary>
    public void SetWeight(string key, float weight)
    {
        if (!_map.TryGetValue(key, out var list)) return;
        weight = Mathf.Clamp(weight, 0f, 100f);
        foreach (var (smr, idx) in list)
            smr.SetBlendShapeWeight(idx, weight);
        _current[key] = weight;
    }

    public float GetWeight(string key) => _current.TryGetValue(key, out var v) ? v : 0f;

    /// <summary> 여러 키를 동시에 부드럽게 보간 </summary>
    public IEnumerator LerpToPose(Dictionary<string, float> target, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        // 시작값 스냅샷
        var start = new Dictionary<string, float>();
        foreach (var kv in target)
            start[kv.Key] = GetWeight(kv.Key);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            foreach (var kv in target)
            {
                float s = start[kv.Key];
                float e = Mathf.Clamp(kv.Value, 0f, 100f);
                SetWeight(kv.Key, Mathf.LerpUnclamped(s, e, u));
            }
            yield return null;
        }
        foreach (var kv in target)
            SetWeight(kv.Key, kv.Value);
    }
}
