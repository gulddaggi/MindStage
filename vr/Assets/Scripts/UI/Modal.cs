using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App.UI
{
    /// <summary>어디서나 간단히 호출하기 위한 정적 헬퍼.</summary>
    public static class Modal
    {
        static ModalManager Mgr => ModalManager.Instance;

        public static ModalView Show(
            string title,
            string message,
            string okText = "확인",
            string cancelText = null,
            Action onOk = null,
            Action onCancel = null)
        {
            if (Mgr == null)
            {
                Debug.LogWarning("[Modal] ModalManager.Instance is null.");
                return null;
            }

            return Mgr.Show(title, message, okText, cancelText, onOk, onCancel);
        }

        public static ModalView Alert(
            string message,
            string title = "알림",
            Action onOk = null)
        {
            return Show(title, message, "확인", null, onOk, null);
        }

        public static ModalView Confirm(
            string message,
            string title = "확인",
            Action onOk = null,
            Action onCancel = null)
        {
            return Show(title, message, "확인", "취소", onOk, onCancel);
        }
    }
}