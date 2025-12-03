using UnityEngine;

/// <summary>
/// 머리 + 눈알을 특정 타겟을 향해 돌려주는 스크립트.
/// 애니메이션이 만든 포즈를 기준으로, LateUpdate에서 "추가 회전"만 얹는다.
/// </summary>
public class HeadEyeLookAt : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Bones")]
    public Transform headBone;
    public Transform leftEyeBone;
    public Transform rightEyeBone;

    [Header("Weights & Limits")]
    [Range(0f, 1f)]
    public float headWeight = 0.5f;

    [Range(0f, 1f)]
    public float eyeWeight = 1.0f;

    [Tooltip("머리가 현재 애니메이션 포즈에서 최대 몇 도까지 타겟 쪽으로 돌 수 있는지")]
    public float maxHeadAngle = 50f;

    [Tooltip("눈알이 현재 포즈에서 최대 몇 도까지 돌 수 있는지")]
    public float maxEyeAngle = 70f;

    [Tooltip("회전 보간 속도")]
    public float rotationLerpSpeed = 8f;

    void LateUpdate()
    {
        if (target == null) return;

        if (headBone != null && headWeight > 0f)
        {
            ApplyLookAtDelta(
                headBone,
                target.position,
                headWeight,
                maxHeadAngle
            );
        }

        if (leftEyeBone != null && eyeWeight > 0f)
        {
            ApplyLookAtDelta(
                leftEyeBone,
                target.position,
                eyeWeight,
                maxEyeAngle
            );
        }

        if (rightEyeBone != null && eyeWeight > 0f)
        {
            ApplyLookAtDelta(
                rightEyeBone,
                target.position,
                eyeWeight,
                maxEyeAngle
            );
        }
    }

    /// <summary>
    /// bone의 "현재 애니메이션 포즈"를 기준으로,
    /// 타겟 방향으로 추가 회전을 덧입힌다.
    /// </summary>
    void ApplyLookAtDelta(Transform bone, Vector3 targetWorldPos, float weight, float maxAngle)
    {
        if (bone == null) return;
        Transform parent = bone.parent;
        if (parent == null) return; // 루트면 스킵

        // 1) 현재 애니메이션이 만들어 둔 "기준 정면" (부모 로컬 기준)
        Vector3 currentForwardLocal =
            parent.InverseTransformDirection(bone.forward).normalized;

        // 2) 타겟 방향 (부모 로컬 기준)
        Vector3 toTargetWorld = (targetWorldPos - bone.position);
        if (toTargetWorld.sqrMagnitude < 0.0001f) return;

        Vector3 toTargetLocal =
            parent.InverseTransformDirection(toTargetWorld.normalized);

        // 3) 각도 제한
        float angle = Vector3.Angle(currentForwardLocal, toTargetLocal);
        if (angle < 0.001f) return;

        float clampFactor = 1f;
        if (angle > maxAngle)
        {
            clampFactor = maxAngle / angle;
        }

        Vector3 clampedDir =
            Vector3.Slerp(currentForwardLocal, toTargetLocal, clampFactor);

        // 4) 현재 forward → 타겟(제한된) 방향으로 회전하는 델타
        Quaternion deltaRot =
            Quaternion.FromToRotation(currentForwardLocal, clampedDir);

        // 5) 델타를 현재 로컬 회전에 곱해서 "원하는 회전" 생성
        Quaternion desiredLocalRot = deltaRot * bone.localRotation;

        // 6) 부드럽게 보간 + weight 반영
        bone.localRotation = Quaternion.Slerp(
            bone.localRotation,
            desiredLocalRot,
            Time.deltaTime * rotationLerpSpeed * weight
        );
    }
}
