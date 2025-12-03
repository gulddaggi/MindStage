using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Prepare
{
    public class WatchLinkPopupController : MonoBehaviour
    {
        public TMP_InputField serialInput;
        public TMP_InputField installationIdInput;
        public Button btnConnect;
        public Button btnCancel;

        Action<string, string> _onConnect;

        void Awake()
        {
            btnCancel.onClick.AddListener(() => gameObject.SetActive(false));
            btnConnect.onClick.AddListener(() => {
                var serial = serialInput.text?.Trim();
                var inst = installationIdInput.text?.Trim();
                _onConnect?.Invoke(serial, inst);
                gameObject.SetActive(false);
            });
        }

        public void Open(Action<string, string> onConnect)
        {
            _onConnect = onConnect;
            serialInput.text = "";
            installationIdInput.text = "";
        }
    }
}
