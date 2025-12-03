using App.Core;
using App.Infra;
using App.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Settings
{
    public class PanelWatch : MonoBehaviour
    {
        public TMP_InputField uuidInput;
        public TMP_InputField modelInput;
        public TMP_Text statusText;          // “등록이 완료되었습니다 / 등록이 필요합니다”
        public Button btnConnect;            // 연결/해제 토글 버튼
        public Button btnBack;
        public Button btnHome;

        IWearLinkService _wear;

        public Action onClosed;

        // 버튼 색/텍스트 기본값 기억
        Image _btnImg;
        TMP_Text _btnLabel;
        Color _btnColorDefault = Color.white;
        Color _labelColorDefault = Color.white;
        readonly Color _danger = new Color(0.95f, 0.2f, 0.2f);

        void Awake()
        {
            _wear = App.Infra.Services.Resolve<IWearLinkService>();

            _btnImg = btnConnect.GetComponent<Image>();
            _btnLabel = btnConnect.GetComponentInChildren<TMP_Text>(true);
            if (_btnImg) _btnColorDefault = _btnImg.color;
            if (_btnLabel) _labelColorDefault = _btnLabel.color;

            btnBack.onClick.AddListener(() =>
                GetComponentInParent<PanelRouter>(true).Show(SettingsPanel.Main));
            if (btnHome)
            {
                btnHome.onClick.AddListener(async () =>
                    await SceneLoader.LoadSingleAsync(SceneIds.MainMenu));
            }

        }

        async void OnEnable() => await RefreshUI();

        void OnDisable()
        {
            onClosed?.Invoke();   // 추가
        }

        async System.Threading.Tasks.Task RefreshUI()
        {
            var st = await _wear.GetStatusAsync();
            bool linked = st.state == WearLinkState.Linked;

            uuidInput.interactable = !linked;
            modelInput.interactable = !linked;

            if (linked)
            {
                uuidInput.text = st.uuid ?? uuidInput.text;
                modelInput.text = st.modelName ?? modelInput.text;
                SetStatus(true, "등록이 완료되었습니다.");
                uuidInput.text = "";
                SetButtonUnlinkMode();   // 해제 모드
            }
            else
            {
                uuidInput.text = "";
                modelInput.text = "";
                SetStatus(false, "등록이 필요합니다.");
                SetButtonLinkMode();     // 연결 모드
            }
        }

        // ---- 버튼 모드 전환 ----
        void SetButtonLinkMode()
        {
            btnConnect.onClick.RemoveAllListeners();
            btnConnect.onClick.AddListener(OnConnect);
            btnConnect.interactable = true;
            if (_btnImg) _btnImg.color = _btnColorDefault;
            if (_btnLabel) { _btnLabel.text = "연결"; _btnLabel.color = _labelColorDefault; }
        }

        void SetButtonUnlinkMode()
        {
            btnConnect.onClick.RemoveAllListeners();
            btnConnect.onClick.AddListener(OnUnlink);
            btnConnect.interactable = true;
            if (_btnImg) _btnImg.color = _danger;
            if (_btnLabel) { _btnLabel.text = "해제"; _btnLabel.color = _danger; }
        }

        // ---- 액션 ----
        async void OnConnect()
        {
            var uuid = uuidInput.text?.Trim();
            var model = modelInput.text?.Trim();
            if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(model)) return;

            btnConnect.interactable = false;
            try
            {
                await _wear.RegisterAsync(new WearLinkRegisterRequest { uuid = uuid, modelName = model });
            }
            finally
            {
                btnConnect.interactable = true;
                await RefreshUI();
            }
        }

        async void OnUnlink()
        {
            btnConnect.interactable = false;
            try
            {
                await _wear.UnlinkAsync();
            }
            finally
            {
                btnConnect.interactable = true;
                await RefreshUI();   // 기본(연결 모드)로 복귀
            }
        }

        void SetStatus(bool ok, string msg)
        {
            statusText.text = msg;
            statusText.color = ok ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.95f, 0.2f, 0.2f);
        }
    }
}