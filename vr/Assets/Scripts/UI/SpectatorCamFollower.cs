using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpectatorCamFollower : MonoBehaviour
{
    public Camera xrMainCamera;
    public bool lockRoll = true;
    public float followLerp = 1f; // 0~1 사이, 부드럽게 따라가고 싶으면 값 조절

    void LateUpdate()
    {
        if (xrMainCamera == null) return;

        // 위치 따라가기
        transform.position = Vector3.Lerp(
            transform.position,
            xrMainCamera.transform.position,
            followLerp * Time.deltaTime
        );

        // 회전 따라가기 (롤 제거 옵션)
        var euler = xrMainCamera.transform.rotation.eulerAngles;

        if (lockRoll)
            euler.z = 0f; // 모니터에서는 항상 수평으로

        transform.rotation = Quaternion.Euler(euler);
    }
}
