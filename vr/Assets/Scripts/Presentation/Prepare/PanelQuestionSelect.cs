using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Interview
{
    /// <summary>예/아니오/뒤로 로 구성된 꼬리질문 사용 여부 선택 팝업.</summary>
    public class PanelQuestionSelect : MonoBehaviour
    {
        public Button YesBtn;
        public Button NoBtn;
        public Button BackBtn;

        Action<bool> _onSelected;

        void Awake()
        {
            if (YesBtn) YesBtn.onClick.AddListener(() => Close(true));
            if (NoBtn) NoBtn.onClick.AddListener(() => Close(false));
            if (BackBtn) BackBtn.onClick.AddListener(() => Close(null));
        }

        public void Open(Action<bool> onSelected)
        {
            _onSelected = onSelected;
            gameObject.SetActive(true);
        }

        void Close(bool? value)
        {
            gameObject.SetActive(false);
            if (value.HasValue) _onSelected?.Invoke(value.Value);
        }
    }
}
