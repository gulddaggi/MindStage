using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRCanvasPlacer : MonoBehaviour
{
    public enum FacingMode
    {
        FullHead,        // roll/pitch/yaw 그대로 추종
        YawOnly,         // yaw만 추종(수평 유지)
        YawPitchClamped, // yaw + 제한된 pitch
        Static           // 전환 시 한 번만 배치하고 이후엔 고정
    }

    [Header("Refs")]
    public Camera xrMainCamera;

    [Header("Placement")]
    public float distance = 1.7f;
    public float yOffset = 0f;
    [Tooltip("연속 추종 모드에서 위치/회전 보간 속도")]
    public float followLerp = 12f;

    [Header("Facing")]
    public FacingMode facing = FacingMode.YawOnly;
    [Range(0f, 90f)] public float pitchClampDeg = 20f;

    Canvas _canvas;
    Transform _t;

    void Awake()
    {
        _t = transform;
        _canvas = GetComponent<Canvas>();
    }

    void OnEnable() { UiModeSwitcher.OnModeChanged += HandleMode; }
    void OnDisable() { UiModeSwitcher.OnModeChanged -= HandleMode; }

    void HandleMode(bool vrOn)
    {
        if (vrOn) PlaceNow(true); // 전환 시점에 한 번 앞에 위치
    }

    void LateUpdate()
    {
        if (!xrMainCamera) return;
        if (facing == FacingMode.Static) return; // ★ 더 이상 따라가지 않음

        var cam = xrMainCamera.transform;

        // --- 위치 ---
        var posTarget = cam.position + cam.forward * distance + Vector3.up * yOffset;
        _t.position = Vector3.Lerp(_t.position, posTarget, Time.deltaTime * followLerp);

        // --- 회전 ---
        var rotTarget = ComputeRotation(cam, facing);
        _t.rotation = Quaternion.Slerp(_t.rotation, rotTarget, Time.deltaTime * followLerp);
    }

    public void PlaceNow(bool snap = false)
    {
        var cam = GetCam();
        if (!cam) return;

        var ct = cam.transform;

        _t.position = ct.position + ct.forward * distance + Vector3.up * yOffset;

        // Static 모드에선 첫 배치 때만 yaw 기준으로 바라보게 세팅
        var modeForPlacement = (facing == FacingMode.Static) ? FacingMode.YawOnly : facing;
        _t.rotation = ComputeRotation(ct, modeForPlacement);

        if (snap && facing != FacingMode.Static)
        {
            // 연속 추종 모드 첫 프레임에 보간 없이 정확히 맞추고 시작
            _t.position = ct.position + ct.forward * distance + Vector3.up * yOffset;
        }
    }

    Quaternion ComputeRotation(Transform cam, FacingMode mode)
    {
        switch (mode)
        {
            case FacingMode.FullHead:
                return Quaternion.LookRotation(cam.forward, cam.up);

            case FacingMode.YawPitchClamped:
                var yawFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                if (yawFwd.sqrMagnitude < 1e-6f) yawFwd = cam.forward;
                var right = Vector3.Cross(Vector3.up, yawFwd).normalized;
                float pitch = Vector3.SignedAngle(yawFwd, cam.forward, right);
                pitch = Mathf.Clamp(pitch, -pitchClampDeg, pitchClampDeg);
                var pitchedFwd = Quaternion.AngleAxis(pitch, right) * yawFwd;
                return Quaternion.LookRotation(pitchedFwd, Vector3.up);

            default: // YawOnly 및 Static의 기본 회전
                var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 1e-6f) fwd = cam.forward;
                return Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    Camera GetCam()
    {
        if (_canvas && _canvas.worldCamera) return _canvas.worldCamera;
        var um = FindObjectOfType<UiModeSwitcher>();
        if (um && um.xrMainCamera) return um.xrMainCamera;
        return Camera.main;
    }
}
