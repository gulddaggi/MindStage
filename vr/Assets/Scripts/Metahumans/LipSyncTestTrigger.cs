using UnityEngine;

public class LipSyncTestTrigger : MonoBehaviour
{
    public LipSyncRouter router;   // 씬의 LipSyncRouter 할당
    public AudioClip testClip;     // 인스펙터에서 넣을 테스트용 클립
    [Range(0, 1)] public int testLabel = 0;  // 0 또는 1

    [Header("Play Once When Checked In Play Mode")]
    public bool trigger = false;   // 플레이 도중 체크하면 실행

    void Update()
    {
        if (!Application.isPlaying) return;

        if (trigger)
        {
            trigger = false; // 한 번만 실행되도록 바로 꺼줌

            if (router != null && testClip != null)
            {
                router.PlayLabeledClip(testClip, testLabel);
            }
            else
            {
                Debug.LogWarning("router 또는 testClip이 비어 있습니다.");
            }
        }
    }
}
