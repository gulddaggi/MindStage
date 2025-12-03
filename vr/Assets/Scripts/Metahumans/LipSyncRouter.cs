using UnityEngine;

public class LipSyncRouter : MonoBehaviour
{
    [Header("Targets")]
    public LipSyncSpeaker speakerA;  // label == 0
    public LipSyncSpeaker speakerB;  // label == 1

    ///
    /// 외부에서 (clip, label) 들어오면 이 함수를 호출하기 
    /// label == 0 -> 남성 면접관, label == 1 -> 여성 면접관
    /// 
    public void PlayLabeledClip(AudioClip clip, int label)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayLabeledClip: clip이 null입니다.");
            return;
        }

        switch (label)
        {
            case 0:
                if (speakerA) speakerA.PlayClip(clip);
                else Debug.LogWarning("speakerA가 설정되지 않았습니다.");
                break;

            case 1:
                if (speakerB) speakerB.PlayClip(clip);
                else Debug.LogWarning("speakerB가 설정되지 않았습니다.");
                break;

            default:
                Debug.LogWarning($"알 수 없는 라벨 값: {label}");
                break;
        }
    }
}
