using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(OVRLipSyncContext))]
public class LipSyncSpeaker : MonoBehaviour
{
    [Header("References")]
    public AudioSource audioSource;              // 비워두면 자동으로 GetComponent
    public OVRLipSyncContext lipSyncContext;     // 비워두면 자동으로 GetComponent
    public Animator animator;                    // 입/몸 애니메이션 담당

    [Header("Animator State Names")]
    public string idleStateName = "Idle";        // Idle 스테이트 이름
    public string talkingStateName = "Talking";  // 말할 때 스테이트 이름
    public float crossFadeTime = 0.2f;           // 애니메이션 전환 시간

    public bool IsTalking { get; private set; }

    private Coroutine _playingRoutine;

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!lipSyncContext) lipSyncContext = GetComponent<OVRLipSyncContext>();

        // 씬 시작 시 자동 재생 방지
        if (audioSource)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    /// 
    /// 이 스피커가 특정 오디오 클립을 재생하고,
    /// 애니메이션을 Talking → 끝나면 Idle로 돌려줌.
    /// 
    public void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            Debug.LogWarning($"{name}: Clip 또는 AudioSource가 없습니다.");
            return;
        }

        // 이전에 재생 중이던 것/코루틴 정리
        if (_playingRoutine != null)
        {
            StopCoroutine(_playingRoutine);
        }
        audioSource.Stop();

        IsTalking = true;

        // 새 클립 할당 후 재생
        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();

        // Animator가 있으면 말하기 상태로 자연스럽게 전환
        if (animator && !string.IsNullOrEmpty(talkingStateName))
        {
            animator.CrossFade(talkingStateName, crossFadeTime);
        }

        // 오디오가 끝나면 Idle로 돌아가는 코루틴 시작
        _playingRoutine = StartCoroutine(WaitAndReturnToIdle());
    }

    private IEnumerator WaitAndReturnToIdle()
    {
        // 1) 오디오가 재생되는 동안 대기
        while (audioSource != null && audioSource.isPlaying)
        {
            yield return null;
        }

        IsTalking = false;

        // 2) 오디오가 끝나는 즉시 Idle 상태로 강제 전환
        if (animator && !string.IsNullOrEmpty(idleStateName))
        {
            animator.CrossFade(idleStateName, 0.05f, 0);
        }

        _playingRoutine = null;
    }
}
