using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.UI
{
    /// <summary>제목/본문/OK/Cancel 버튼을 가진 기본 모달 뷰.</summary>
    public class ModalView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] Button okButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text okLabel;
        [SerializeField] TMP_Text cancelLabel;

        Action _onOk;
        Action _onCancel;

        /// <summary>모달 내용을 설정하고 버튼 콜백을 연결.</summary>
        public void Setup(
            string title,
            string message,
            string okText,
            string cancelText,
            Action onOk,
            Action onCancel)
        {
            if (titleText) titleText.text = title ?? string.Empty;
            if (bodyText) bodyText.text = message ?? string.Empty;

            _onOk = onOk;
            _onCancel = onCancel;

            // OK 버튼
            if (okButton)
            {
                okButton.gameObject.SetActive(true);
                okButton.onClick.RemoveAllListeners();
                okButton.onClick.AddListener(OnOkClicked);

                if (okLabel)
                    okLabel.text = string.IsNullOrEmpty(okText) ? "확인" : okText;
            }

            // Cancel 버튼
            if (cancelButton)
            {
                bool hasCancel = !string.IsNullOrEmpty(cancelText) || onCancel != null;
                cancelButton.gameObject.SetActive(hasCancel);

                cancelButton.onClick.RemoveAllListeners();
                if (hasCancel)
                    cancelButton.onClick.AddListener(OnCancelClicked);

                if (cancelLabel && hasCancel)
                    cancelLabel.text = string.IsNullOrEmpty(cancelText) ? "취소" : cancelText;
            }
        }

        void OnOkClicked()
        {
            _onOk?.Invoke();
            Close();
        }

        void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Close();
        }

        public void Close()
        {
            Destroy(gameObject);
        }
    }
}
