using UnityEngine;

public class IdleBlendshapeController : MonoBehaviour
{
    [Header("Dependencies")]
    public LipSyncSpeaker speaker;             // 이 캐릭터의 LipSyncSpeaker
    public SkinnedMeshRenderer faceRenderer;   // 블렌드쉐입이 있는 헤드 메쉬

    [Header("Target Blendshape (a)")]
    public int blendShapeIndex = 0;      // a 블렌드쉐입 index
    public float idleWeight = 80f;       // idle일 때 유지할 값
    public float talkingWeight = 0f;     // talking일 때 유지할 값 (보통 0)
    public float lerpSpeed = 10f;        // 값 전환 속도 (클수록 더 빨리 바뀜)

    void Reset()
    {
        // 같은 오브젝트에 붙어있으면 자동으로 찾아주기
        if (!speaker) speaker = GetComponent<LipSyncSpeaker>();
        if (!faceRenderer) faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    void Update()
    {
        if (faceRenderer == null) return;
        if (blendShapeIndex < 0 ||
            blendShapeIndex >= faceRenderer.sharedMesh.blendShapeCount) return;

        // 현재 상태에 따라 목표 weight 결정
        float target =
            (speaker != null && speaker.IsTalking)
            ? talkingWeight   // talking 중
            : idleWeight;     // idle 중

        float current = faceRenderer.GetBlendShapeWeight(blendShapeIndex);
        float newWeight = Mathf.Lerp(current, target, Time.deltaTime * lerpSpeed);

        faceRenderer.SetBlendShapeWeight(blendShapeIndex, newWeight);
    }
}
