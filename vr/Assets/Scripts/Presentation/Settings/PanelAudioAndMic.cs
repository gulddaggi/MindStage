using App.Infra;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Settings
{
    public class PanelAudioAndMic : MonoBehaviour
    {
        [Header("Mic")]
        public App.Presentation.Interview.MicRecorder mic;
        public TMP_Dropdown micDropdown;
        public Image rmsBar;
        public TMP_Text sampleRateText;

        [Header("Speaker")]
        public TMP_Text speakerDeviceText;
        public Slider volumeSlider;
        public TMP_Text volumeValueText;
        public Button btnTestTone;
        public AudioSource audioOut;

        [Header("Nav")]
        public Button btnBack;
        public Button btnHome;
        public Button btnSave;

        List<string> _micNames = new();

        public Action onClosed;

        bool _micUiInitialized;
        Coroutine _startMeterCo;

        void Awake()
        {
            if (!audioOut)
            {
                var go = new GameObject("SpeakerOut");
                go.transform.SetParent(transform, false);
                audioOut = go.AddComponent<AudioSource>();
                audioOut.playOnAwake = false;
                audioOut.loop = false;
                audioOut.spatialBlend = 0f;
            }

            btnBack.onClick.AddListener(() =>
                GetComponentInParent<PanelRouter>(true).Show(SettingsPanel.Main));
            btnHome?.onClick.AddListener(async () =>
                await SceneLoader.LoadSingleAsync(SceneIds.MainMenu));
            btnSave.onClick.AddListener(SaveAll);

            btnTestTone.onClick.AddListener(PlayTestTone);
            volumeSlider.onValueChanged.AddListener(SetVolumeAndSave);
        }

        void OnEnable()
        {
            // 1) Mic 드롭다운 초기화는 생명주기 전체에서 한 번만
            if (!_micUiInitialized)
            {
                InitMicUI();
                _micUiInitialized = true;
            }

            // 2) 이미 미터링 중이면 재시작하지 않음
            if (mic && !mic.IsRecording && _startMeterCo == null)
            {
                // 한 프레임 양보해서 UI 먼저 뜨게 한 뒤 마이크 시작
                _startMeterCo = StartCoroutine(CoStartMeteringLazy());
            }

            // 3) 스피커/볼륨 UI 동기화
            if (speakerDeviceText) speakerDeviceText.text = GetSystemDefaultSpeakerLabel();

            float v = PlayerPrefs.GetFloat("pref.audio.masterVolume", 0.8f);
            v = Mathf.Clamp01(v);
            volumeSlider.SetValueWithoutNotify(v);
            ApplyVolume(v);
        }

        IEnumerator CoStartMeteringLazy()
        {
            // UI 그려지는 프레임 양보
            yield return null;

            if (mic && !mic.IsRecording)
            {
                mic.StartMetering();  // RestartMetering 대신 StartMetering
            }
            _startMeterCo = null;
        }

        void OnDisable()
        {
            if (_startMeterCo != null)
            {
                StopCoroutine(_startMeterCo);
                _startMeterCo = null;
            }

            // MicRecorder는 OnDestroy에서 정리하므로 여기서 StopMetering 안 함
            onClosed?.Invoke();
        }

        void Update()
        {
            if (mic && rmsBar)
            {
                float target = Mathf.Clamp01(mic.LevelRms * 1.8f);
                rmsBar.fillAmount = target;
            }
        }

        void InitMicUI()
        {
            micDropdown.onValueChanged.RemoveAllListeners();

            micDropdown.ClearOptions();
            _micNames.Clear();
            _micNames.AddRange(Microphone.devices);
            if (_micNames.Count == 0) _micNames.Add("(장치 없음)");
            micDropdown.AddOptions(_micNames);

            if (mic == null) return;

            // 저장된 장치 복원
            var saved = PlayerPrefs.GetString("pref.mic.device", "");
            if (!string.IsNullOrEmpty(saved) && _micNames.Contains(saved))
            {
                mic.selectedDevice = saved;
            }
            else if (string.IsNullOrEmpty(mic.selectedDevice) && _micNames.Count > 0)
            {
                if (_micNames[0] != "(장치 없음)")
                    mic.selectedDevice = _micNames[0];
            }

            var idx = Mathf.Max(0, _micNames.IndexOf(mic.selectedDevice));
            micDropdown.SetValueWithoutNotify(idx);

            micDropdown.onValueChanged.AddListener(OnMicDropdownChanged);

            if (sampleRateText) sampleRateText.text = $"샘플레이트: {mic.sampleRate} Hz";
        }

        void OnMicDropdownChanged(int i)
        {
            if (mic == null || _micNames.Count == 0) return;

            var dev = _micNames[Mathf.Clamp(i, 0, _micNames.Count - 1)];
            if (dev == "(장치 없음)") return;

            // 같은 장치로 다시 선택하면 아무것도 안 함
            if (mic.selectedDevice == dev) return;

            mic.selectedDevice = dev;

            // 장치가 실제로 변경됐을 때만 미터링 재시작
            if (mic.IsRecording)
                mic.RestartMetering();

            PlayerPrefs.SetString("pref.mic.device", dev);
            // 실제 디스크 저장은 SaveAll()에서 한 번만
        }

        void PlayTestTone()
        {
            if (!audioOut) return;
            int sr = AudioSettings.outputSampleRate;
            int len = sr; float freq = 440f;
            var data = new float[len];
            for (int i = 0; i < len; i++) data[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sr);
            var clip = AudioClip.Create("TestTone_A4_1s", len, 1, sr, false);
            clip.SetData(data, 0);
            audioOut.PlayOneShot(clip);
        }

        string GetSystemDefaultSpeakerLabel()
        {
#if UNITY_STANDALONE_WIN
            return "시스템 기본 스피커 (Windows 사운드 설정)";
#elif UNITY_STANDALONE_OSX
            return "시스템 기본 스피커 (macOS 사운드 설정)";
#else
            return "시스템 기본 스피커";
#endif
        }

        void SetVolumeAndSave(float v)
        {
            v = Mathf.Clamp01(v);
            ApplyVolume(v);
            PlayerPrefs.SetFloat("pref.audio.masterVolume", v);
            //PlayerPrefs.Save();
        }

        void ApplyVolume(float v)
        {
            AudioListener.volume = Mathf.Clamp01(v);
            if (volumeValueText) volumeValueText.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
            if (audioOut) audioOut.volume = 1f; // 톤은 1.0 고정, 전역 볼륨으로 제어
        }

        void SaveAll()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            Debug.Log("[Audio/Mic] Saved.");
#endif
        }

    }
}
