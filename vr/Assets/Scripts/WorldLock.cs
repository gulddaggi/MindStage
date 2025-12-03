using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldLock : MonoBehaviour
{
    [Header("배치 후 잠금")]
    public bool lockPosition = true;
    public bool lockRotation = true;
    public bool lockScale = true;

    Vector3 _p, _s;
    Quaternion _r;
    bool _armed;

    public void ArmNow()  // 배치 끝난 시점에 호출
    {
        _p = transform.position;
        _r = transform.rotation;
        _s = transform.localScale;
        _armed = true;
    }

    void LateUpdate()
    {
        if (!_armed) return;
        if (lockPosition) transform.position = _p;
        if (lockRotation) transform.rotation = _r;
        if (lockScale) transform.localScale = _s;
    }
}
