using App.Auth;
using App.Infra;
using App.Services;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Settings
{
    public class PanelSettingMenu : MonoBehaviour
    {
        public Button btnUserInfo;
        public Button btnAudioMic;
        public Button btnWatch;
        public Button btnBackToMainMenu;

        PanelRouter _router;

        void Awake()
        {
            _router = GetComponentInParent<PanelRouter>(true);
            btnUserInfo.onClick.AddListener(() => _router.Show(SettingsPanel.UserInfo));
            btnAudioMic.onClick.AddListener(() => _router.Show(SettingsPanel.AudioMic));
            btnWatch.onClick.AddListener(() => _router.Show(SettingsPanel.Watch));
            btnBackToMainMenu.onClick.AddListener(async () =>
            {
                await SceneLoader.LoadSingleAsync(SceneIds.MainMenu);
            });
        }
    }
}
