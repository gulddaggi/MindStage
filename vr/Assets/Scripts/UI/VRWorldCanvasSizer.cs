using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VRWorldCanvasSizer : MonoBehaviour
{
    [Header("Reference (Desktop와 동일)")]
    public Vector2 referenceResolution = new(2880, 1800);
    [Range(0f, 1f)] public float match = 0.5f;

    [Header("월드에서 보이는 실제 크기")]
    public float widthMeters = 1.1f;   // UI 전체 너비(미터). 원하는 크기로 조절
    public float anchorDepth = 1.5f;   // 카메라 앞 거리(옵션, 필요 시 사용)

    RectTransform rt;
    CanvasScaler scaler;

    public float referenceWidth = 1920f;
    public float referenceDistance = 2.0f;
    public float targetDistance = 2.0f;

    Vector3 _baseScale;


    void Awake() { _baseScale = transform.localScale; }

    void OnEnable()
    {
        UiModeSwitcher.OnModeChanged += OnMode;
    }
    void OnDisable()
    {
        UiModeSwitcher.OnModeChanged -= OnMode;
    }

    void OnMode(bool vrOn)
    {
        if (vrOn) ApplyScale();
    }

    public void ApplyScale()
    {
        // 누적 방지: 항상 원래 스케일에서 시작
        transform.localScale = _baseScale * (targetDistance / referenceDistance);
    }

    void Apply()
    {
        if (!rt) rt = GetComponent<RectTransform>();
        if (!scaler) scaler = GetComponent<CanvasScaler>();

        // 1) 데스크톱과 동일한 방식으로 스케일 결정
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = match;

        // 2) 루트 캔버스 크기를 "레퍼런스 해상도"로 고정
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, referenceResolution.x);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, referenceResolution.y);

        // 3) 월드에서 보이는 실제 크기(미터)로 스케일 맞추기
        float unitsPerPixel = widthMeters / referenceResolution.x;   // 1픽셀이 몇 미터인지
        transform.localScale = Vector3.one * unitsPerPixel;

        // (옵션) UI를 카메라 앞 고정하고 싶다면
        // var cam = Camera.main;
        // if (cam)
        // {
        //     transform.position = cam.transform.position + cam.transform.forward * anchorDepth;
        //     transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        // }
    }
}
