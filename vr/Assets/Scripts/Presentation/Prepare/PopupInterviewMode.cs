using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace App.Presentation.Interview
{
    /// <summary>AI 면접 / 1분 자기소개 모드 선택 팝업.</summary>
    public class PopupInterviewMode : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text title;
        public TMP_Text txtSub;

        public Button btnQuestionSetMode;   // AI 면접 모드
        public Button btnSelfIntroMode;     // 1분 자기소개 모드
        public Button btnClose;             // 선택 사항

        public Action<InterviewMode> OnSelected;

        const string DESC_QUESTION = "질문 세트 기반으로 전체 면접을 진행합니다.";
        const string DESC_SELF = "1분 자기소개만 연습합니다.";

        void Awake()
        {
            //gameObject.SetActive(false);

            if (title)
                title.text = "모드 선택";

            // 기본 설명은 질문 세트 모드 기준으로
            if (txtSub)
                txtSub.text = DESC_QUESTION;

            if (btnQuestionSetMode)
            {
                btnQuestionSetMode.onClick.AddListener(
                    () => Select(InterviewMode.QuestionSet));
                AddHoverHandler(btnQuestionSetMode, DESC_QUESTION);
            }

            if (btnSelfIntroMode)
            {
                btnSelfIntroMode.onClick.AddListener(
                    () => Select(InterviewMode.SelfIntro1Min));
                AddHoverHandler(btnSelfIntroMode, DESC_SELF);
            }

            if (btnClose)
                btnClose.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void Open(InterviewMode defaultMode)
        {
            gameObject.SetActive(true);

            if (txtSub)
            {
                // 현재 선택 모드 기준 기본 설명 세팅
                txtSub.text = defaultMode == InterviewMode.SelfIntro1Min
                    ? DESC_SELF
                    : DESC_QUESTION;
            }
        }

        void Select(InterviewMode mode)
        {
            gameObject.SetActive(false);
            OnSelected?.Invoke(mode);
        }

        void AddHoverHandler(Button btn, string desc)
        {
            if (btn == null || txtSub == null) return;

            var trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = btn.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            entry.callback.AddListener(_ => txtSub.text = desc);
            trigger.triggers.Add(entry);
        }
    }
}
