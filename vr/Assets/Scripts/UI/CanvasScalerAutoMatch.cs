using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class CanvasScalerAutoMatch : MonoBehaviour
{
    [Header("Reference")]
    public Vector2 referenceResolution = new(2880, 1800);
    public float targetAspect = 16f / 10f;
    public bool smoothMatch = true;
    public Vector2 smoothAspectRange = new(4f / 3f, 21f / 9f);
    public float hysteresis = 0.03f;

    CanvasScaler _scaler;
    int _lastW, _lastH;
    float _lastMatch;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = referenceResolution;
        Recompute(true);
    }

    void OnEnable()
    {
        if (ResolutionManager.Instance != null)
            ResolutionManager.Instance.OnSettingsChanged += ForceRecompute;
    }
    void OnDisable()
    {
        if (ResolutionManager.Instance != null)
            ResolutionManager.Instance.OnSettingsChanged -= ForceRecompute;
    }

    public void ForceRecompute() => Recompute(true);

    void LateUpdate()
    {
        if (Screen.width != _lastW || Screen.height != _lastH)
            Recompute(false);
    }

    void Recompute(bool force)
    {
        _lastW = Screen.width; _lastH = Screen.height;
        if (_lastW <= 0 || _lastH <= 0) return;

        float cur = (float)_lastW / _lastH;
        float match;
        if (smoothMatch)
        {
            float t = Mathf.InverseLerp(smoothAspectRange.x, smoothAspectRange.y, cur);
            match = Mathf.Clamp01(t);
        }
        else
        {
            if (Mathf.Abs(cur - targetAspect) <= hysteresis) match = 0.5f;
            else match = (cur > targetAspect) ? 1f : 0f;
        }

        if (!force && Mathf.Approximately(_lastMatch, match)) return;
        _scaler.matchWidthOrHeight = match;
        _lastMatch = match;
    }
}
