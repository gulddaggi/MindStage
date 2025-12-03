using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace App.Presentation.Interview
{
    public class PanelModalError : MonoBehaviour
    {
        [Header("UI")]
        public GameObject backdrop;    // 반투명 배경(전체 클릭 방지)
        public TMP_Text titleTxt;
        public TMP_Text bodyTxt;
        public Button btnPrimary;      // 면접 준비로
        public TMP_Text btnPrimaryLabel;
        public Button btnSecondary;    // (선택) 다시 시도
        public TMP_Text btnSecondaryLabel;

        Action _onPrimary, _onSecondary;

        void Awake()
        {
            //Hide(); // 시작 시 비활성
        }

        public void Open(string title, string body,
                         Action onPrimary,
                         string primaryText = "면접 준비로",
                         Action onSecondary = null,
                         string secondaryText = "다시 시도",
                         Transform preferParent = null)
        {
            titleTxt.text = title ?? "다운로드 실패";
            bodyTxt.text = body ?? "질문 오디오를 불러오지 못했습니다.";

            _onPrimary = onPrimary;
            _onSecondary = onSecondary;

            btnPrimary.onClick.RemoveAllListeners();
            btnPrimary.onClick.AddListener(() => { _onPrimary?.Invoke(); Hide(); });

            if (btnPrimaryLabel) btnPrimaryLabel.text = primaryText;

            bool useSecondary = (onSecondary != null);
            if (btnSecondary) btnSecondary.gameObject.SetActive(useSecondary);
            if (useSecondary)
            {
                if (btnSecondaryLabel) btnSecondaryLabel.text = secondaryText;
                btnSecondary.onClick.RemoveAllListeners();
                btnSecondary.onClick.AddListener(() => { _onSecondary?.Invoke(); Hide(); });
            }

            gameObject.SetActive(true);
            if (backdrop) backdrop.SetActive(true);
        }

        public void Hide()
        {
            if (backdrop) backdrop.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
