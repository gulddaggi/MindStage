using UnityEngine;
namespace App.Services
{
    /// <summary>외부 API 없이 비프톤을 생성하는 로컬 TTS 대체 구현.</summary>

    public class DummyTtsProvider : ITtsProvider
    {
        public AudioClip Synthesize(string text)
        {
            // 0.5초 길이 비프(플레이스홀더). 실제 구현시 Resources에서 프리롤 벨소리/바운스음 로드 가능.
            int sr = 24000; float dur = 0.4f;
            var clip = AudioClip.Create("beep", (int)(sr * dur), 1, sr, false);
            float[] data = new float[clip.samples];
            float freq = 880f;
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sr) * (1f - (i / (float)data.Length));
            clip.SetData(data, 0);
            return clip;
        }
    }
}
