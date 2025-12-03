using App.Core;
using App.Infra;                 
using App.Presentation.Interview; 
using App.Presentation.Prepare;
using App.Services;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ServiceHub = App.Infra.Services;


namespace App.Presentation.Settings
{
    // Summary: 설정 화면 컨트롤러 – 마이크 선택/레벨미터, 워치 링크, 시스템 기본 스피커 테스트·마스터 볼륨 저장·복원.

    public class SettingController : MonoBehaviour
    {
        [Header("Mic")]
        public MicRecorder mic;                // Audio 오브젝트의 MicRecorder
        public TMP_Dropdown micDropdown;
        public Image rmsBar;                   // fillAmount 0..1
        public TMP_Text sampleRateText;

        [Header("Watch Link Box")]
        public TMP_Text linkBadgeText;
        public TMP_Text linkDetailText;
        public Button btnLink;
        public Button btnUnlink;
        public TMP_Text idText;               // 설치 고유번호
        public TMP_Text serialText;           // 시리얼 번호

        [Header("Audio Output (System Default)")]
        public TMP_Text speakerDeviceText;    // "시스템 기본 스피커" 안내용
        public Slider volumeSlider;           // 0~1
        public TMP_Text volumeValueText;      // "80%" 등
        public Button btnTestTone;            // 테스트 재생 버튼
        public AudioSource audioOut;          // 테스트톤 재생용(2D, mute=false)

        [Header("Nav")]
        public Button btnSave;
        public Button btnBack;

        IWearLinkService _wear;
        bool _updating;
        List<string> _micNames = new();

        private App.Infra.LocalSettings _settings;

        static class PrefKeys
        {
            public const string MicDevice = "pref.mic.device";
            public const string MasterVolume = "pref.audio.masterVolume";
        }

        void Awake()
        {
            EnsureSeparateAudioSources();
        }

        void EnsureSeparateAudioSources()
        {
            // MicRecorder의 내부 오디오소스와 같은지 방어
            if (audioOut == null)
            {
                var go = new GameObject("SpeakerOut");
                go.transform.SetParent(transform, false);
                audioOut = go.AddComponent<AudioSource>();
                audioOut.playOnAwake = false;
                audioOut.loop = false;
                audioOut.spatialBlend = 0f;
                audioOut.priority = 128;
            }
        }

        AudioClip MakeTestTone(float hz, float durSec)
        {
            int sr = AudioSettings.outputSampleRate;
            int count = Mathf.CeilToInt(sr * durSec);
            var clip = AudioClip.Create("TestTone", count, 1, sr, false);
            float step = (Mathf.PI * 2f * hz) / sr;
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Sin(step * i) * 0.5f;
            clip.SetData(data, 0);
            return clip;
        }

        async void Start()
        {
            _wear = ServiceHub.Resolve<IWearLinkService>();

            // 로컬 설정 불러오기
            _settings = App.Infra.LocalSettingsStore.Load();

            InitMicUI();
            mic.RestartMetering();

            InitSpeakerUI();
            InitVolumeFromPrefs();

            await RefreshWatchBoxUI();

            // 워치 버튼
            btnLink.onClick.AddListener(OpenLinkPopup);
            btnUnlink.onClick.AddListener(async () =>
            {
                await _wear.UnlinkAsync();
                SetIdSerial("", "");
                await RefreshWatchBoxUI();
            });

            btnTestTone.onClick.AddListener(() =>
            {
                // toneClip은 미리 만들었거나, 아래처럼 즉석 생성 가능
                var tone = MakeTestTone(440f, 0.3f); // 440Hz, 0.3초
                audioOut.PlayOneShot(tone, volumeSlider.value);
            });

            // 볼륨 슬라이더
            volumeSlider.onValueChanged.AddListener(SetVolumeAndSave);

            btnSave.onClick.AddListener(SaveAll);
            btnBack.onClick.AddListener(async () => {
                await SceneLoader.LoadSingleAsync(SceneIds.MainMenu);
            });

            _updating = true;
        }

        void Update()
        {
            if (mic != null)
            {
                // 시각적으로 보기 좋게 약간의 게인/스무딩
                float target = Mathf.Clamp01(mic.LevelRms * 1.8f);
                if (rmsBar) rmsBar.fillAmount = target;
            }
        }

        void InitMicUI()
        {
            // 드롭다운 채우기
            micDropdown.ClearOptions();
            var opts = new System.Collections.Generic.List<string>(Microphone.devices);
            if (opts.Count == 0) opts.Add("(장치 없음)");
            micDropdown.AddOptions(opts);

            // 현재 선택
            var idx = Mathf.Max(0, opts.IndexOf(mic.selectedDevice));
            micDropdown.value = idx;
            micDropdown.onValueChanged.AddListener(i => {
                var dev = opts[Mathf.Clamp(i, 0, opts.Count - 1)];
                if (dev != "(장치 없음)")
                {
                    mic.selectedDevice = dev;
                    mic.RestartMetering();
                }
            });

            sampleRateText.text = $"샘플레이트: {mic.sampleRate} Hz";
        }

        #region Mic
        void InitMicUIFromPrefs()
        {
            micDropdown.ClearOptions();
            _micNames.Clear();
            _micNames.AddRange(Microphone.devices);
            if (_micNames.Count == 0) _micNames.Add("(장치 없음)");
            micDropdown.AddOptions(_micNames);

            // 저장된 장치 복원
            var saved = PlayerPrefs.GetString(PrefKeys.MicDevice, "");
            if (!string.IsNullOrEmpty(saved) && _micNames.Contains(saved))
                mic.selectedDevice = saved;

            // 드롭다운 초기값 동기화
            var idx = Mathf.Max(0, _micNames.IndexOf(mic.selectedDevice));
            micDropdown.value = idx;

            // 변경 처리 + 저장
            micDropdown.onValueChanged.AddListener(i =>
            {
                var dev = _micNames[Mathf.Clamp(i, 0, _micNames.Count - 1)];
                if (dev != "(장치 없음)")
                {
                    mic.selectedDevice = dev;
                    mic.RestartMetering();
                    PlayerPrefs.SetString(PrefKeys.MicDevice, dev);
                    PlayerPrefs.Save();
                }
            });

            sampleRateText.text = $"샘플레이트: {mic.sampleRate} Hz";
        }
        #endregion

        #region Speaker (System Default + Test + Volume)
        void InitSpeakerUI()
        {
            // Unity는 출력 장치 열거/선택 불가 → 안내 텍스트만 표기
            if (speakerDeviceText) speakerDeviceText.text = GetSystemDefaultSpeakerLabel();

            if (btnTestTone) btnTestTone.onClick.AddListener(PlayTestTone);

            // AudioSource 기본값 권장:
            // - Spatial Blend = 0 (2D)
            // - Mute = false
            // - Play On Awake = false
            // - Output Audio Mixer 연결 시에도 마스터는 AudioListener.volume로 관리
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

        void InitVolumeFromPrefs()
        {
            float v = PlayerPrefs.GetFloat(PrefKeys.MasterVolume, 0.8f);
            v = Mathf.Clamp01(v);
            volumeSlider.SetValueWithoutNotify(v);
            ApplyVolume(v);
        }

        void SetVolumeAndSave(float v)
        {
            v = Mathf.Clamp01(v);
            ApplyVolume(v);
            PlayerPrefs.SetFloat(PrefKeys.MasterVolume, v);
            PlayerPrefs.Save();
        }

        void ApplyVolume(float v)
        {
            AudioListener.volume = v;                // 전역 마스터
            if (volumeValueText) volumeValueText.text = $"{Mathf.RoundToInt(v * 100f)}%";
            if (audioOut) audioOut.volume = 1f;
        }

        void PlayTestTone()
        {
            if (audioOut == null) return;

            int sr = AudioSettings.outputSampleRate;
            int len = sr;                      // 1초
            float freq = 440f;                 // A4
            var data = new float[len];
            for (int i = 0; i < len; i++)
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * i / sr);

            var clip = AudioClip.Create("TestTone_A4_1s", len, 1, sr, false);
            clip.SetData(data, 0);
            audioOut.PlayOneShot(clip);
        }
        #endregion

        #region Watch
        async Task RefreshWatchBoxUI()
        {
            var st = await _wear.GetStatusAsync();

            SetIdSerial("", "");

            switch (st.state)
            {
                case WearLinkState.Disconnected:
                    linkBadgeText.text = "미연결";
                    linkDetailText.text = "워치가 연결되어 있지 않습니다.";
                    btnLink.gameObject.SetActive(true);
                    btnUnlink.gameObject.SetActive(false);
                    break;

                case WearLinkState.Pending:
                    linkBadgeText.text = "연결 대기";
                    linkDetailText.text = $"코드 대기중 (TTL {st.ttlSeconds}s)";
                    btnLink.gameObject.SetActive(false);
                    btnUnlink.gameObject.SetActive(true);
                    break;

                case WearLinkState.Linked:
                    linkBadgeText.text = "연결됨";
                    linkDetailText.text = "연결된 워치 정보를 확인하세요.";
                    //SetIdSerial(st.installationId, st.serial);
                    btnLink.gameObject.SetActive(false);
                    btnUnlink.gameObject.SetActive(true);
                    break;
            }
        }

        void SetIdSerial(string installationId, string serial)
        {
            if (idText) idText.text = installationId ?? "";
            if (serialText) serialText.text = serial ?? "";
        }

        void OpenLinkPopup()
        {
            // 씬에 비활성으로 둔 프리팹을 찾아 활성화 (면접 준비와 동일)
            var popup = FindObjectOfType<WatchLinkPopupController>(includeInactive: true);
            popup.gameObject.SetActive(true);

            popup.Open(async (serial, instId) =>
            {
                SetIdSerial(instId, serial);

                /*await _wear.RegisterAsync(new WearLinkRegisterRequest
                {
                    serial = serial,
                    installationId = instId
                });
                */
                await RefreshWatchBoxUI();
            });
        }
        #endregion

        void OnDestroy()
        {
            mic?.StopMetering();
        }

        void SaveAll()
        {
            if (_settings == null) _settings = new App.Infra.LocalSettings();

            // Mic
            _settings.micDevice = mic ? mic.selectedDevice : _settings.micDevice;
            _settings.micSampleRate = mic ? mic.sampleRate : _settings.micSampleRate;

            // Watch (현재 화면 표시값을 그대로 저장)
            _settings.watchInstallationId = idText ? idText.text : _settings.watchInstallationId;
            _settings.watchSerial = serialText ? serialText.text : _settings.watchSerial;

            // Speaker
            _settings.speakerVolume = AudioListener.volume;

            App.Infra.LocalSettingsStore.Save(_settings);

#if UNITY_EDITOR
            Debug.Log("[Settings] Saved.");
#endif
        }

    }
}