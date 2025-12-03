using App.Core;
using App.Infra;
using App.Presentation.Interview;
using App.Services;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ServiceHub = App.Infra.Services;

namespace App.Presentation.Prepare
{
    public class InterviewPrepareController : MonoBehaviour
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

        public TMP_Text idText;      
        public TMP_Text serialText;

        [Header("Nav")]
        public Button btnNext;                 // 질문 세트 선택으로 이동
        public Button btnBack;

        IWearLinkService _wear;
        bool _updating;

        async void Start()
        {
            _wear = ServiceHub.Resolve<IWearLinkService>();

            InitMicUI();
            mic.StartMetering();
            await RefreshWatchBoxUI();

            btnLink.onClick.AddListener(OpenLinkPopup);
            btnUnlink.onClick.AddListener(async () => {
                await _wear.UnlinkAsync();
                SetIdSerial("", "");
                await RefreshWatchBoxUI();
            });

            btnNext.onClick.AddListener(async () => {
                await SceneLoader.LoadSingleAsync(SceneIds.QuestionSetSelect);
            });

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
            if (idText is TMP_Text t1) t1.text = installationId ?? "";
            if (serialText is TMP_Text t2) t2.text = serial ?? "";
        }

        void OpenLinkPopup()
        {
            // 프리팹을 띄우거나, 이미 씬에 비활성으로 둔 팝업을 활성화
            var popup = FindObjectOfType<WatchLinkPopupController>(includeInactive: true);
            popup.gameObject.SetActive(true);

            popup.Open(async (serial, instId) => {
                SetIdSerial(instId, serial);

                // 백엔드 더미/실서버 등록
                /*await _wear.RegisterAsync(new WearLinkRegisterRequest
                {
                    serial = serial,
                    installationId = instId
                });
                */
                await RefreshWatchBoxUI();
            });
        }

        void OnDestroy()
        {
            mic?.StopMetering();
        }
    }
}
