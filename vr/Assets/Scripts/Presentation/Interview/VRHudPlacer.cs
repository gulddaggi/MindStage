using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App.Presentation.Interview
{
    /// <summary>
    /// XR 카메라 앞 일정 거리로 부드럽게 따라오는 HUD 배치기.
    /// 장애물이 있으면 살짝 당겨 배치. Yaw만 빌보드(멀미 최소화).
    /// </summary>
    public class VRHudPlacer : MonoBehaviour
    {
        public Camera xrCamera;                 // XR Origin/Main Camera
        [Header("Distance/Height")]
        public float targetDistance = 1.8f;     // 1.2~2.0m 권장
        public float heightOffset = -0.05f;     // 눈높이 대비 소폭 아래

        [Header("Follow Tuning")]
        public float yawDeadZone = 8f;          // 수평 회전 데드존(도)
        public float moveLerp = 6f;             // 위치 보간 속도
        public float rotLerp = 8f;              // 회전 보간 속도
        public bool yawOnly = true;             // 수평만 빌보드

        [Header("Obstruction")]
        public LayerMask obstructionMask;       // 벽/가구 레이어
        public float castRadius = 0.18f;        // UI 반경
        public float wallMargin = 0.06f;        // 벽과 UI 간격

        Vector3 _lastFacing; // 수평 바라보는 방향 캐시

        void OnEnable()
        {
            if (!xrCamera) xrCamera = Camera.main;
            Snap(true);
        }

        void LateUpdate()
        {
            if (!xrCamera) return;
            Follow();
        }

        void Follow()
        {
            var cam = xrCamera.transform;

            // 1) 바라보는 수평 방향
            var fwd = cam.forward;
            if (yawOnly) { fwd.y = 0f; if (fwd.sqrMagnitude < 1e-6f) fwd = cam.forward; fwd.Normalize(); }

            // 2) 데드존: 일정 각도 이상 돌아섰을 때만 갱신
            if (_lastFacing == Vector3.zero) _lastFacing = fwd;
            float yawDelta = Vector3.SignedAngle(_lastFacing, fwd, Vector3.up);
            if (Mathf.Abs(yawDelta) > yawDeadZone) _lastFacing = fwd;

            // 3) 목표 위치 계산 + 장애물 보정
            Vector3 desired = cam.position + _lastFacing * targetDistance;
            desired.y = cam.position.y + heightOffset;

            if (Physics.SphereCast(cam.position, castRadius, _lastFacing, out var hit, targetDistance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                desired = hit.point - _lastFacing * (castRadius + wallMargin);
                desired.y = cam.position.y + heightOffset;
            }

            // 4) 이동/회전 보간
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-moveLerp * Time.deltaTime));

            Vector3 look = transform.position - cam.position;
            if (yawOnly) look.y = 0f;
            if (look.sqrMagnitude > 1e-6f)
            {
                var rot = Quaternion.LookRotation(look.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-rotLerp * Time.deltaTime));
            }
        }

        public void Snap(bool immediate = false)
        {
            var cam = xrCamera ? xrCamera.transform : null;
            if (!cam) return;
            _lastFacing = cam.forward; if (yawOnly) { _lastFacing.y = 0f; _lastFacing.Normalize(); }
            if (immediate) { moveLerp = rotLerp = 1000f; LateUpdate(); moveLerp = 6f; rotLerp = 8f; }
        }
    }
}