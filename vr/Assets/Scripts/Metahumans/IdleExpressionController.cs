using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleExpressionController : MonoBehaviour
{
    [System.Serializable]
    public class IdleBlendshape
    {
        public string name;      // 설명용
        public int index;        // SkinnedMeshRenderer의 BlendShape 인덱스
        public float maxWeight = 30f;
    }

    public LipSyncSpeaker speaker;               // 말하는지 여부 체크
    public SkinnedMeshRenderer faceRenderer;     // 헤드 메쉬
    public List<IdleBlendshape> idleShapes = new List<IdleBlendshape>();

    [Header("Timing")]
    public float minInterval = 2f;   // 다음 표정까지 최소 대기
    public float maxInterval = 5f;   // 최대 대기
    public float blendInTime = 0.15f;
    public float holdTime = 0.25f;
    public float blendOutTime = 0.15f;

    void Start()
    {
        if (faceRenderer != null && idleShapes.Count > 0)
            StartCoroutine(LoopIdleExpressions());
    }

    IEnumerator LoopIdleExpressions()
    {
        while (true)
        {
            // 랜덤 대기
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            // 말하는 중이거나, 세팅이 부족하면 스킵
            if (speaker != null && speaker.IsTalking)
                continue;
            if (faceRenderer == null || idleShapes.Count == 0)
                continue;

            // 랜덤 블렌드쉐입 선택
            var shape = idleShapes[Random.Range(0, idleShapes.Count)];
            yield return StartCoroutine(PlayShape(shape));
        }
    }

    IEnumerator PlayShape(IdleBlendshape shape)
    {
        int idx = shape.index;
        float t;

        // 1) blend in
        t = 0f;
        while (t < blendInTime)
        {
            if (speaker != null && speaker.IsTalking) break; // 말하면 즉시 중단
            t += Time.deltaTime;
            float w = Mathf.Lerp(0f, shape.maxWeight, t / blendInTime);
            faceRenderer.SetBlendShapeWeight(idx, w);
            yield return null;
        }

        // 2) hold
        t = 0f;
        while (t < holdTime)
        {
            if (speaker != null && speaker.IsTalking) break;
            t += Time.deltaTime;
            yield return null;
        }

        // 3) blend out
        t = 0f;
        float startW = faceRenderer.GetBlendShapeWeight(idx);
        while (t < blendOutTime)
        {
            t += Time.deltaTime;
            float w = Mathf.Lerp(startW, 0f, t / blendOutTime);
            faceRenderer.SetBlendShapeWeight(idx, w);
            yield return null;
        }

        faceRenderer.SetBlendShapeWeight(idx, 0f);
    }

    // 밖에서 특정 이름만 강제 재생하고 싶을 때(시선 b 볼 때 등)
    public void PlayShapeOnceByName(string name)
    {
        var shape = idleShapes.Find(s => s.name == name);
        if (shape != null && faceRenderer != null)
            StartCoroutine(PlayShape(shape));
    }
}
