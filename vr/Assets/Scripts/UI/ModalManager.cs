using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App.UI
{
    public enum ModalUIMode
    {
        Desktop,
        VR
    }

    /// <summary>DontDestroyOnLoad로 유지되는 전역 모달 매니저.</summary>
    public class ModalManager : MonoBehaviour
    {
        public static ModalManager Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] GameObject desktopModalPrefab;
        [SerializeField] GameObject vrModalPrefab;

        [Header("Roots")]
        [Tooltip("데스크톱 모달을 붙일 Canvas나 Transform (없으면 이 오브젝트 하위에 생성)")]
        [SerializeField] Transform desktopRoot;

        [Tooltip("VR 모달을 배치할 기준 Transform (대개 HMD 카메라)")]
        [SerializeField] Transform vrRoot;

        [Tooltip("VR 모달을 HMD 앞쪽으로 얼마나 떨어뜨릴지(m)")]
        [SerializeField] float vrDistance = 1.5f;

        ModalUIMode _mode = ModalUIMode.Desktop;
        public ModalUIMode CurrentMode => _mode;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>현재 UI 모드(Desktop/VR)를 설정. UiModeSwitcher나 씬 컨트롤러에서 호출.</summary>
        public void SetMode(ModalUIMode mode)
        {
            _mode = mode;
        }

        /// <summary>모달을 생성하고 내용을 설정.</summary>
        public ModalView Show(
            string title,
            string message,
            string okText = "확인",
            string cancelText = null,
            Action onOk = null,
            Action onCancel = null)
        {
            GameObject prefab = null;
            Transform parent = null;

            if (_mode == ModalUIMode.VR && vrModalPrefab != null)
            {
                prefab = vrModalPrefab;
                parent = vrRoot != null ? vrRoot : transform;
            }
            else
            {
                prefab = desktopModalPrefab;
                parent = desktopRoot != null ? desktopRoot : transform;
            }

            if (!prefab)
            {
                Debug.LogWarning($"[ModalManager] Prefab not assigned for mode {_mode}");
                return null;
            }

            var go = Instantiate(prefab, parent);

            // VR인 경우 HMD 앞에 위치/회전 정렬
            if (_mode == ModalUIMode.VR && vrRoot != null)
            {
                var t = go.transform;
                t.position = vrRoot.position + vrRoot.forward * vrDistance;
                t.rotation = Quaternion.LookRotation(vrRoot.forward, Vector3.up);
            }

            var view = go.GetComponent<ModalView>();
            if (!view)
            {
                Debug.LogWarning("[ModalManager] ModalView component missing on prefab.");
                return null;
            }

            view.Setup(title, message, okText, cancelText, onOk, onCancel);
            return view;
        }
    }
}