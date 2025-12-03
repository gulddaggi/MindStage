namespace App.Services
{
    /// <summary>질문 텍스트를 오디오로 변환하는 TTS 제공자 인터페이스(더미/실구현 공용).</summary>

    public interface ITtsProvider
    {
        // 실제에선 text→오디오 생성. 지금은 더미: 준비된 AudioClip 또는 비프음 반환
        UnityEngine.AudioClip Synthesize(string text);
    }
}
