using UnityEngine;
using System.Collections;

/// <summary>
/// 왼/오른쪽 눈을 각각 다른 BlendShape 인덱스로 깜빡이는 고급 버전.
/// - 기본 랜덤 깜빡임
/// - 가끔 더블 블링크
/// - 피곤 모드일 때 더 자주 깜빡임
/// - 가끔 긴 눈 감기(눈 마르는 느낌)
/// - 외부에서 강제로 깜빡임 / 더블 / 롱 블링크 트리거 가능
/// </summary>
public class EyeBlink : MonoBehaviour
{
    [Header("Target Renderers")]
    [Tooltip("눈 블렌드쉐이프가 들어있는 SkinnedMeshRenderer들 (보통 head_mesh 하나면 됨)")]
    public SkinnedMeshRenderer[] renderers;

    [Header("BlendShape Indices")]
    [Tooltip("왼쪽 눈 깜빡임 BlendShape Index (예: EyeBlink_L)")]
    public int blinkLeftIndex = -1;

    [Tooltip("오른쪽 눈 깜빡임 BlendShape Index (예: EyeBlink_R)")]
    public int blinkRightIndex = -1;

    [Header("Base Blink Timing (seconds)")]
    [Tooltip("기본 깜빡임 간 최소 / 최대 간격 (랜덤)")]
    public Vector2 intervalSeconds = new Vector2(2.5f, 5.0f);

    [Tooltip("눈 감는 데 걸리는 시간")]
    public float closeDuration = 0.07f;

    [Tooltip("눈 뜨는 데 걸리는 시간")]
    public float openDuration = 0.08f;

    [Tooltip("완전히 감은 상태 유지 시간")]
    public float closedHoldDuration = 0.03f;

    [Header("Tired Mode (피곤/졸림 모드)")]
    [Tooltip("피곤 모드인지 여부 (On이면 더 자주 깜빡임)")]
    public bool tiredMode = false;

    [Tooltip("피곤 모드일 때 간격 배수 (0.5 = 절반 간격, 더 자주 깜빡)")]
    public float tiredIntervalScale = 0.5f;

    [Header("Special Blink Patterns")]
    [Range(0f, 1f)]
    [Tooltip("랜덤 깜빡임 중 긴 눈 감기(롱 블링크)가 나올 확률")]
    public float longBlinkChance = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("랜덤 깜빡임 중 더블 블링크가 나올 확률 (longBlinkChance와 합이 1 이하 추천)")]
    public float doubleBlinkChance = 0.15f;

    [Tooltip("롱 블링크 시 완전히 감은 상태 유지 시간")]
    public float longClosedHoldDuration = 0.25f;

    [Tooltip("더블 블링크 시 두 번 사이의 간격")]
    public float doubleBlinkGap = 0.05f;

    private Coroutine blinkLoopRoutine;
    private bool _isBlinking = false; // 현재 깜빡임 진행 중인지

    private void OnEnable()
    {
        blinkLoopRoutine = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        if (blinkLoopRoutine != null)
            StopCoroutine(blinkLoopRoutine);
    }

    // -------------------- 메인 루프 ----------------------------

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            // 피곤 모드일 경우 간격 줄이기
            Vector2 interval = intervalSeconds;
            if (tiredMode)
            {
                interval = new Vector2(
                    intervalSeconds.x * tiredIntervalScale,
                    intervalSeconds.y * tiredIntervalScale
                );
            }

            float wait = Random.Range(interval.x, interval.y);
            yield return new WaitForSeconds(wait);

            if (_isBlinking)
                continue; // 이미 강제 블링크나 다른 패턴이 진행 중이면 다음 루프까지 대기

            // 어떤 패턴으로 깜빡일지 랜덤 선택
            float r = Random.value;
            if (r < longBlinkChance)
            {
                yield return LongBlinkRoutine();
            }
            else if (r < longBlinkChance + doubleBlinkChance)
            {
                yield return DoubleBlinkRoutine();
            }
            else
            {
                yield return SingleBlinkRoutine(closedHoldDuration);
            }
        }
    }

    // -------------------- 패턴들 ----------------------------

    IEnumerator SingleBlinkRoutine(float holdDuration)
    {
        if (!CanBlink()) yield break;

        _isBlinking = true;
        yield return BlinkOnce(100f, closeDuration, holdDuration, openDuration);
        _isBlinking = false;
    }

    IEnumerator DoubleBlinkRoutine()
    {
        if (!CanBlink()) yield break;

        _isBlinking = true;
        // 첫 번째 깜빡임
        yield return BlinkOnce(100f, closeDuration, closedHoldDuration, openDuration);
        // 짧은 간격
        yield return new WaitForSeconds(doubleBlinkGap);
        // 두 번째 깜빡임 (조금 더 짧게 해도 됨)
        yield return BlinkOnce(100f, closeDuration * 0.8f, closedHoldDuration * 0.8f, openDuration);
        _isBlinking = false;
    }

    IEnumerator LongBlinkRoutine()
    {
        if (!CanBlink()) yield break;

        _isBlinking = true;
        // 눈 마르는 느낌: 닫는/여는 시간은 그대로, 감고 있는 시간만 길게
        yield return BlinkOnce(100f, closeDuration, longClosedHoldDuration, openDuration);
        _isBlinking = false;
    }

    /// <summary>
    /// 기본 단일 깜빡임 수행 (targetWeight까지 닫고, hold, 다시 0으로 열기)
    /// </summary>
    IEnumerator BlinkOnce(float targetWeight, float closeDur, float holdDur, float openDur)
    {
        if (!HasValidSetup()) yield break;

        // 현재 weight (왼/오 기준)
        float startLeft = GetCurrentWeight(blinkLeftIndex);
        float startRight = GetCurrentWeight(blinkRightIndex);

        // 1) 감기
        float t = 0f;
        while (t < closeDur)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / closeDur);

            float wLeft = Mathf.Lerp(startLeft, targetWeight, lerp);
            float wRight = Mathf.Lerp(startRight, targetWeight, lerp);

            ApplyWeights(wLeft, wRight);
            yield return null;
        }

        ApplyWeights(targetWeight, targetWeight);

        // 2) 감은 상태 유지
        if (holdDur > 0f)
            yield return new WaitForSeconds(holdDur);

        // 3) 다시 뜨기
        t = 0f;
        while (t < openDur)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / openDur);

            float wLeft = Mathf.Lerp(targetWeight, 0f, lerp);
            float wRight = Mathf.Lerp(targetWeight, 0f, lerp);

            ApplyWeights(wLeft, wRight);
            yield return null;
        }

        ApplyWeights(0f, 0f);
    }

    // -------------------- 외부에서 호출할 수 있는 API ----------------------------

    /// <summary>
    /// 외부(TTS 시작, 아이컨택 등)에서 "한 번 깜빡여!" 라고 부르고 싶을 때 사용.
    /// 랜덤 루프와 별개로, 가능한 경우 즉시 단일 블링크 수행.
    /// </summary>
    public void TriggerBlink()
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(ManualSingleBlink());
    }

    /// <summary>
    /// 외부에서 긴 눈 감기(롱 블링크)를 강제로 실행하고 싶을 때.
    /// </summary>
    public void TriggerLongBlink()
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(ManualLongBlink());
    }

    /// <summary>
    /// 외부에서 더블 블링크를 강제로 실행하고 싶을 때.
    /// </summary>
    public void TriggerDoubleBlink()
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(ManualDoubleBlink());
    }

    IEnumerator ManualSingleBlink()
    {
        if (_isBlinking || !CanBlink()) yield break;
        _isBlinking = true;
        yield return BlinkOnce(100f, closeDuration, closedHoldDuration, openDuration);
        _isBlinking = false;
    }

    IEnumerator ManualLongBlink()
    {
        if (_isBlinking || !CanBlink()) yield break;
        _isBlinking = true;
        yield return BlinkOnce(100f, closeDuration, longClosedHoldDuration, openDuration);
        _isBlinking = false;
    }

    IEnumerator ManualDoubleBlink()
    {
        if (_isBlinking || !CanBlink()) yield break;
        _isBlinking = true;
        yield return DoubleBlinkRoutine();
        _isBlinking = false;
    }

    // -------------------- Helper ----------------------------

    bool HasValidSetup()
    {
        if (renderers == null || renderers.Length == 0) return false;
        if (blinkLeftIndex < 0 && blinkRightIndex < 0) return false;
        return true;
    }

    bool CanBlink()
    {
        return HasValidSetup();
    }

    float GetCurrentWeight(int index)
    {
        if (index < 0) return 0f;

        foreach (var r in renderers)
        {
            if (r != null)
                return r.GetBlendShapeWeight(index);
        }
        return 0f;
    }

    void ApplyWeights(float leftWeight, float rightWeight)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (blinkLeftIndex >= 0)
                r.SetBlendShapeWeight(blinkLeftIndex, leftWeight);

            if (blinkRightIndex >= 0)
                r.SetBlendShapeWeight(blinkRightIndex, rightWeight);
        }
    }
}
