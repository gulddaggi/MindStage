using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VowelController : MonoBehaviour
{
    public FaceBlendshapeRegistry registry;

    [Header("▶ Inspector Toggle")]
    [Tooltip("체크하면 아-에-이-오-우 루프 시작, 해제하면 비발화 표정 유지")]
    public bool talkToggle = false;   // 인스펙터 토글
    private bool _lastToggle = false; // 변경 감지용

    [Header("타이밍")]
    public float transitionSeconds = 0.18f;
    public float holdSeconds = 0.10f;

    [Header("Idle(비발화) 표정)")]
    [Range(0, 100)] public float idleBrowInner = 100f;
    [Range(0, 100)] public float idleWoo = 0f;
    [Range(0, 100)] public float idleBoo = 0f;
    [Range(0, 100)] public float idleJawOpen = 0f;
    [Range(0, 100)] public float idleSmile = 0f;

    private readonly List<Dictionary<string, float>> _vowels = new();
    private Coroutine _loopCo;
    private bool _isTalking;

    void Reset() { registry = GetComponent<FaceBlendshapeRegistry>(); }

    void Awake()
    {
        if (registry == null) registry = GetComponent<FaceBlendshapeRegistry>();

        // ===== 모음 포즈 정의 =====
        _vowels.Clear();

        _vowels.Add(new Dictionary<string, float>
        {
            ["jawOpen"] = 40f,
            ["smile"] = 15f,
            ["boo"] = 0f,
            ["woo"] = 0f,
            ["browInner"] = 0f  // 아
        });
        _vowels.Add(new Dictionary<string, float>
        {
            ["jawOpen"] = 35f,
            ["smile"] = 40f,
            ["boo"] = 0f,
            ["woo"] = 0f,
            ["browInner"] = 0f  // 에
        });
        _vowels.Add(new Dictionary<string, float>
        {
            ["jawOpen"] = 20f,
            ["smile"] = 50f,
            ["boo"] = 0f,
            ["woo"] = 0f,
            ["browInner"] = 0f  // 이
        });
        _vowels.Add(new Dictionary<string, float>
        {
            ["jawOpen"] = 50f,
            ["smile"] = 5f,
            ["boo"] = 10f,
            ["woo"] = 40f,
            ["browInner"] = 0f  // 오
        });
        _vowels.Add(new Dictionary<string, float>
        {
            ["jawOpen"] = 0f,
            ["smile"] = 0f,
            ["boo"] = 20f,
            ["woo"] = 35f,
            ["browInner"] = 0f  // 우
        });
    }

    void Start()
    {
        ApplyIdleImmediate();
        _lastToggle = talkToggle;
        if (talkToggle) StartVowelLoop();  // 플레이 시작 시 토글이 이미 켜져 있으면 곧장 시작
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (talkToggle != _lastToggle)
        {
            SetTalking(talkToggle);   // 토글 변화 감지해 반영
            _lastToggle = talkToggle;
        }
    }

    // ===== 외부/토글에서 호출되는 API =====
    public void StartVowelLoop()
    {
        if (_isTalking) return;
        _isTalking = true;
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = StartCoroutine(VowelLoop());
    }

    public void StopVowelLoop()
    {
        _isTalking = false;
        if (_loopCo != null) StopCoroutine(_loopCo);
        _loopCo = null;
        StartCoroutine(registry.LerpToPose(BuildIdlePose(), 0.15f));
    }

    public void SetTalking(bool talking)
    {
        if (talking) StartVowelLoop();
        else StopVowelLoop();
    }

    // ===== 내부 =====
    private IEnumerator VowelLoop()
    {
        int i = 0;
        while (_isTalking)
        {
            var pose = _vowels[i];
            yield return registry.LerpToPose(pose, transitionSeconds);
            yield return new WaitForSeconds(holdSeconds);
            i = (i + 1) % _vowels.Count;
        }
    }

    private Dictionary<string, float> BuildIdlePose() => new Dictionary<string, float>
    {
        ["browInner"] = idleBrowInner,
        ["woo"] = idleWoo,
        ["boo"] = idleBoo,
        ["jawOpen"] = idleJawOpen,
        ["smile"] = idleSmile,
    };

    private void ApplyIdleImmediate()
    {
        var idle = BuildIdlePose();
        foreach (var kv in idle)
            registry.SetWeight(kv.Key, kv.Value);
    }
}
