using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace App.Presentation.Interview
{
    /// <summary>마이크로부터 PCM을 녹음하고 RMS 레벨·WAV 바이트를 제공.</summary>

    [RequireComponent(typeof(AudioSource))]
    public class MicRecorder : MonoBehaviour
    {
        [Range(8000, 48000)] public int sampleRate = 48000;
        public string selectedDevice;
        public bool IsRecording { get; private set; }
        public float LevelRms { get; private set; }

        AudioSource _src;
        AudioClip _clip;
        const int MAX_SECONDS = 180;
        float[] _readBuf = new float[1024];

        const int WindowSize = 1024;
        float[] _win = new float[WindowSize];

        // 마지막 녹음 길이(초) – 참고용
        public float lastRecordDurationSec { get; private set; }

        AudioClip _meterClip;
        Coroutine _meterCo;

        string _meterDevice;

        void Awake() => EnsureSource();

        void EnsureSource()
        {
            if (_src != null) return;
            _src = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.mute = true;
            _src.spatialBlend = 0f;
            _src.ignoreListenerPause = true;
        }

        public void StartRecord()
        {
            EnsureSource();

            // (1) 혹시 남아있을 수 있는 미터링/이전 세션 정리
            if (_clip != null && Microphone.IsRecording(selectedDevice))
            {
                try { Microphone.End(selectedDevice); } catch { }
                _src.Stop();
                _clip = null;
            }

            // (2) 장치 비어있으면 기본 장치 사용
            if (string.IsNullOrEmpty(selectedDevice) && Microphone.devices.Length > 0)
                selectedDevice = Microphone.devices[0];

            // (3) 녹음 시작
            _clip = Microphone.Start(selectedDevice, false, MAX_SECONDS, sampleRate);

            // (4) 녹음 상태 ON 
            IsRecording = true;
        }

        public AudioClip StopRecord(float tailSeconds = 3f)
        {
            if (!IsRecording) return null;

            int micPos = Microphone.GetPosition(selectedDevice);
            if (micPos < 0) micPos = 0;

            Microphone.End(selectedDevice);

            var src = _clip;
            _clip = null;
            if (src == null) return null;

            int freq = src.frequency;
            int channels = src.channels;
            int tail = Mathf.RoundToInt(Mathf.Clamp(tailSeconds, 0f, 5f) * freq);
            int desiredSamples = Mathf.Min(src.samples, micPos + tail);
            if (desiredSamples <= 0) desiredSamples = Mathf.Min(src.samples, freq / 10);

            var outClip = AudioClip.Create("mic_trimmed", desiredSamples, channels, freq, false);
            var data = new float[desiredSamples * channels];
            src.GetData(data, 0);
            outClip.SetData(data, 0);

            lastRecordDurationSec = desiredSamples / (float)freq;

            // 상태 OFF
            IsRecording = false;

            return outClip;
        }


        void Update()
        {
            if (!IsRecording || _clip == null) { LevelRms = 0f; return; }

            // 현재 마이크 위치 기준으로 윈도우 크기만큼 직접 읽어서 RMS 계산
            int pos = Microphone.GetPosition(selectedDevice);
            if (pos < WindowSize) { LevelRms = 0f; return; }

            int start = pos - WindowSize;               // 루프 중이라면 음수 가능 → 보정
            if (start < 0) start += _clip.samples;

            _clip.GetData(_win, start);

            double sum = 0;
            for (int i = 0; i < WindowSize; i++)
            {
                float s = _win[i];
                sum += s * s;
            }
            LevelRms = Mathf.Sqrt((float)(sum / WindowSize));  // 0~1 정도
        }

        // WAV 인메모리 인코딩
        public byte[] ToWavBytes(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            // 16bit PCM
            var pcm = new byte[samples.Length * 2];
            int p = 0; foreach (var f in samples) { short s = (short)Mathf.Clamp(f * 32767f, -32768, 32767); pcm[p++] = (byte)(s & 0xff); pcm[p++] = (byte)((s >> 8) & 0xff); }

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            int byteRate = clip.frequency * clip.channels * 2;
            int subchunk2 = pcm.Length;

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + subchunk2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16); bw.Write((short)1); // PCM
            bw.Write((short)clip.channels);
            bw.Write(clip.frequency);
            bw.Write(byteRate);
            bw.Write((short)(clip.channels * 2));
            bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2);
            bw.Write(pcm, 0, pcm.Length);
            return ms.ToArray();
        }

        /// <summary>저장 없이 레벨만 측정(미리듣기/미터용)</summary>
        public void StartMetering()
        {
            EnsureSource();

            StopMetering();

            if (string.IsNullOrEmpty(selectedDevice))
            {
                if (Microphone.devices.Length == 0) { IsRecording = false; return; }
                selectedDevice = Microphone.devices[0];
            }

            // 길이 1초 루프 클립
            _clip = Microphone.Start(selectedDevice, true, 1, sampleRate);
            _src.clip = _clip;             // 이제 NRE 안 남
            _src.Play();

            StartCoroutine(WaitForStart());
        }

        IEnumerator WaitForStart()
        {
            var dev = selectedDevice;            // 시작 당시 디바이스 고정
            while (Microphone.GetPosition(dev) <= 0) yield return null;
            IsRecording = true;                  // 이제 Update()가 RMS를 계산함
        }

        public void StopMetering()
        {
            if (_clip != null)
            {
                _src.Stop();
                Microphone.End(selectedDevice);
                _clip = null;
            }
            IsRecording = false;
            LevelRms = 0f;
        }


        IEnumerator MeterLoop()
        {
            // 짧은 버퍼로 RMS 계산
            const int N = 1024;
            var buf = new float[N];

            while (_meterClip != null && !string.IsNullOrEmpty(_meterDevice) && Microphone.IsRecording(_meterDevice))
            {
                int pos = Microphone.GetPosition(_meterDevice);
                if (pos >= N)           // 안전한 구간만 읽기
                {
                    int start = pos - N;
                    if (start < 0) start = 0;
                    _meterClip.GetData(buf, start);

                    float sum = 0f;
                    for (int i = 0; i < N; i++) { float s = buf[i]; sum += s * s; }
                    LevelRms = Mathf.Sqrt(sum / N);   // 0..~ 값
                }

                yield return new WaitForSeconds(0.05f);
            }
        }

        public void RestartMetering()
        {
            StopMetering();
            StartMetering();
        }

        private void OnDestroy()
        {
            StopMetering();
        }
    }
}
