using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App.Presentation.Settings
{
    public enum SettingsPanel { Main, UserInfo, AudioMic, Watch, Question }

    public class PanelRouter : MonoBehaviour
    {
        public GameObject panelSetting;     // 메인
        public GameObject panelUserInfo;    // 회원정보
        public GameObject panelAudioAndMic; // 오디오/마이크
        public GameObject panelWatch;       // 워치
        public GameObject panelQuestion;

        public SettingsPanel defaultPanel = SettingsPanel.Main;

        void OnEnable() => Show(defaultPanel);

        public void Show(SettingsPanel p)
        {
            if (panelSetting) panelSetting.SetActive(p == SettingsPanel.Main);
            if (panelUserInfo) panelUserInfo.SetActive(p == SettingsPanel.UserInfo);
            if (panelAudioAndMic) panelAudioAndMic.SetActive(p == SettingsPanel.AudioMic);
            if (panelWatch) panelWatch.SetActive(p == SettingsPanel.Watch);
            if (panelQuestion) panelQuestion.SetActive(p == SettingsPanel.Question);

        }
    }
}
