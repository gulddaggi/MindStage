using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class UiModeSwitcher : MonoBehaviour
{
    [Header("Canvases")]
    public Canvas desktopCanvas;                 // Screen Space - Overlay
    public Canvas vrCanvas;                      // World Space (EventCamera=XR Main Camera)

    [Header("EventSystem Modules")]
    public InputSystemUIInputModule desktopUI;   // 데스크톱용
    public XRUIInputModule xrUI;                 // VR용

    [Header("Shared UI (권장)")]
    [Tooltip("PC/VR이 공유하는 동일한 패널 인스턴스(예: TitlePanel 인스턴스 루트)")]
    public RectTransform sharedPageRoot;         // 하나의 프리팹 인스턴스

    [Tooltip("데스크톱 Canvas 아래에 둘 빈 부모(예: CanvasDesktop/Content)")]
    public RectTransform desktopContainer;           // 재부모 대상(PC)

    [Tooltip("VR Canvas 아래에 둘 빈 부모(예: CanvasVR/Content)")]
    public RectTransform vrContainer;                // 재부모 대상(VR)

    [Tooltip("VR 전환 시 카메라 앞에 한 번 배치")]
    public bool placeSharedRootInFrontOnVR = true;
    public float vrPlaceDistance = 70f;
    public bool vrYawOnlyBillboard = true;

    [Header("Panels")]
    public GameObject desktopPanelRoot;          // CanvasDesktop/TitlePanel
    public GameObject vrPanelRoot;               // CanvasVR/TitlePanel

    [Header("Cameras & XR Objects")]
    public GameObject xrOriginRoot;              // XR Origin (XR Rig)
    public Camera xrMainCamera;                  // XR Origin/Main Camera (HMD)
    public Camera desktopCamera;                 // 씬에 있는 일반 Camera

    [Header("Audio Listeners")]
    public AudioListener xrAudioListener;
    public AudioListener desktopAudioListener;

    [Header("Also Toggle")]
    [Tooltip("VR 모드에서만 활성화할 오브젝트들 (XR Interaction Manager, InputActionManagerObj, XR Device Simulator 등)")]
    public List<GameObject> enableWhenVR = new();
    [Tooltip("데스크톱 모드에서만 활성화할 오브젝트들 (특별히 없으면 비워두세요)")]
    public List<GameObject> enableWhenDesktop = new();

    [Header("Applicant Tracking Target")]
    [Tooltip("사용자를 나타내는 Applicant01 오브젝트의 Transform")]
    public Transform applicantRoot;

    [Header("Options")]
    public bool autoPollXR = true;               // XR 실행상태 감시(에디터/런타임 자동 전환)
    public float pollInterval = 0.5f;            // XR 상태 폴링 주기

    [Header("Presence-based Auto Switch")]
    public bool autoSwitchOnPresence = true;         // HMD 착용/해제도 전환 트리거로 사용
    public bool requireHmdWornForVR = true;          // VR 모드 유지에 '착용'을 요구
    public float wearEnterDelay = 0.4f;              // 착용 감지 연속 시간(초) 후 VR 전환
    public float wearExitDelay = 0.8f;               // 해제 감지 연속 시간(초) 후 PC 전환

    bool _curVrOn;                                   // 현재 모드 캐시
    float _wearT, _unwearT;                          // 디바운스 타이머

    [Header("Runtime Lock")]
    public bool desktopLock; // 인스펙터에서 상태 확인용

    [Header("Spectator Options")]
    public bool useDesktopCameraAsSpectatorInVR = true;

    enum ForceMode { None, Desktop, VR }
    ForceMode _force = ForceMode.None;
    bool _lastXR;

    public bool IsVrMode => _curVrOn;
    public event Action<bool> ModeChanged;

    public static event System.Action<bool> OnModeChanged;

    struct OverlaySnap { public Vector3 anchoredPos3D; public Quaternion localRot; public Vector3 localScale; }
    struct VRSnap { public Vector3 worldPos; public Quaternion worldRot; public Vector3 localScale; }

    readonly Dictionary<RectTransform, OverlaySnap> _overlaySnaps = new();
    readonly Dictionary<RectTransform, VRSnap> _vrSnaps = new();

    void SaveOverlayIfNeeded(RectTransform rt)
    {
        if (!rt || _overlaySnaps.ContainsKey(rt)) return;
        _overlaySnaps[rt] = new OverlaySnap
        {
            anchoredPos3D = rt.anchoredPosition3D,
            localRot = rt.localRotation,
            localScale = rt.localScale
        };
    }
    void RestoreOverlaySnap(RectTransform rt)
    {
        if (!rt) return;
        if (_overlaySnaps.TryGetValue(rt, out var s))
        {
            rt.localScale = s.localScale;
            rt.localRotation = s.localRot;
            rt.anchoredPosition3D = s.anchoredPos3D;
            var lp = rt.localPosition; lp.z = 0f; rt.localPosition = lp; // 안전 z=0
        }
        else
        {
            // 스냅샷이 아직 없다면 기존 0 초기화로 대응
            ResetRectForOverlay(rt);
        }
    }
    void SaveVRSnapIfNeeded(RectTransform rt)
    {
        if (!rt || _vrSnaps.ContainsKey(rt)) return;
        _vrSnaps[rt] = new VRSnap
        {
            worldPos = rt.position,
            worldRot = rt.rotation,
            localScale = rt.localScale
        };
    }
    void RestoreVRSnap(RectTransform rt)
    {
        if (!rt) return;
        if (_vrSnaps.TryGetValue(rt, out var s))
        {
            rt.position = s.worldPos;
            rt.rotation = s.worldRot;
            rt.localScale = s.localScale;
        }
    }

    void Start()
    {
        if (!xrAudioListener && xrMainCamera)
            xrAudioListener = xrMainCamera.GetComponent<AudioListener>();

        if (!desktopAudioListener && desktopCamera)
            desktopAudioListener = desktopCamera.GetComponent<AudioListener>();

        _curVrOn = IsXRRunning();                // 초기값 기억
        ApplyMode(_curVrOn);
        if (autoPollXR) StartCoroutine(PollXR());
    }

    IEnumerator PollXR()
    {
        var wait = new WaitForSeconds(pollInterval);
        while (true)
        {
            if (desktopLock)
            {
                if (_curVrOn) ApplyMode(false); // 잠금 유지 중엔 항상 PC
                yield return wait;
                continue;
            }

            bool xrRunning = IsXRRunning();
            bool worn = IsHmdWorn(out bool presenceSupported);
            bool wornOk = !autoSwitchOnPresence || !requireHmdWornForVR || (presenceSupported ? worn : true);
            bool wantVR = xrRunning && wornOk;

            if (_force == ForceMode.None) // 수동 강제 모드가 아닐 때만
            {
                if (wantVR != _curVrOn)
                {
                    if (wantVR) { _wearT += pollInterval; _unwearT = 0f; if (_wearT >= wearEnterDelay) { _curVrOn = true; ApplyMode(true); } }
                    else { _unwearT += pollInterval; _wearT = 0f; if (_unwearT >= wearExitDelay) { _curVrOn = false; ApplyMode(false); } }
                }
                else { _wearT = _unwearT = 0f; } // 상태 유지 → 타이머 리셋
            }
            yield return wait;
        }
    }


    void Update()
    {
        if (desktopLock) return; // 잠금 시 키 무시

        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame) SwitchToDesktop();
            if (Keyboard.current.f2Key.wasPressedThisFrame) SwitchToVR();
            if (Keyboard.current.f3Key.wasPressedThisFrame) ForceAuto();
        }
    }

    public void ForceAuto()
    {
        _force = ForceMode.None;
        var vr = IsXRRunning();
        ApplyMode(vr);
    }

    bool IsXRRunning()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetInstances(displays);
        foreach (var d in displays) if (d.running) return true;
        return false;
    }

    /// <summary> vrOn == true → VR 모드, false → 데스크톱 모드 </summary>
    void ApplyMode(bool vrOn)
    {
        bool prev = _curVrOn;          // ▼ 이벤트 발행을 위해 이전 상태 저장
        _curVrOn = vrOn;               // ▼ 내부 상태 항상 동기화

        // 1) 카메라/리그
        if (xrOriginRoot) xrOriginRoot.SetActive(vrOn);
        if (xrMainCamera) xrMainCamera.gameObject.SetActive(vrOn);
        if (desktopCamera)
        {
            // PC 모드에서는 항상 켜고,
            // VR 모드에서는 옵션에 따라 '관전자 카메라'로 유지
            bool enableDesktopCam = !vrOn || useDesktopCameraAsSpectatorInVR;
            desktopCamera.gameObject.SetActive(enableDesktopCam);
        }

        if (xrAudioListener)
            xrAudioListener.enabled = vrOn;          // VR 모드에서만 XR 리스너 ON

        if (desktopAudioListener)
            desktopAudioListener.enabled = !vrOn;    // PC 전용 모드에서만 PC 리스너 ON

        UpdateApplicantParent(vrOn);

        bool usedShared = false;
        if (sharedPageRoot && desktopContainer && vrContainer)
        {
            usedShared = true;
            if (vrOn)
            {
                // VR로 이동
                Reparent(sharedPageRoot, vrContainer, false);

                // 최초 VR 진입 시 원하는 초기 배치를 만든 다음 그걸 스냅샷으로 저장
                if (!_vrSnaps.ContainsKey(sharedPageRoot))
                {
                    if (placeSharedRootInFrontOnVR) PlaceInFrontOfCameraOnce(sharedPageRoot);
                    SaveVRSnapIfNeeded(sharedPageRoot);
                }
                // 이후 전환 때마다 저장된 초기 VR 위치/회전으로 복귀
                //RestoreVRSnap(sharedPageRoot);
                MoveSharedRoot(vrContainer);


            }
            else
            {
                // PC로 이동
                Reparent(sharedPageRoot, desktopContainer, false);

                // 최초 PC(Overlay) 스냅샷 저장
                SaveOverlayIfNeeded(sharedPageRoot);
                // 이후 전환 때마다 초기 Overlay 위치/회전으로 복귀
                RestoreOverlaySnap(sharedPageRoot);
            }
        }

        // 2) 캔버스
        if (desktopCanvas) desktopCanvas.gameObject.SetActive(!vrOn);
        if (vrCanvas)
        {
            vrCanvas.gameObject.SetActive(vrOn);
            if (vrOn && xrMainCamera) vrCanvas.worldCamera = xrMainCamera;
        }

        // 3) EventSystem 모듈
        if (desktopUI) desktopUI.enabled = !vrOn;
        if (xrUI) xrUI.enabled = vrOn;

        // 4) 공유 프리팹 이동


        // 5) 분리 패널일 때
        if (!usedShared)
        {
            if (desktopPanelRoot) desktopPanelRoot.SetActive(!vrOn);
            if (vrPanelRoot) vrPanelRoot.SetActive(vrOn);

            if (vrOn && vrPanelRoot)
            {
                var rt = vrPanelRoot.transform as RectTransform;
                if (!_vrSnaps.ContainsKey(rt))
                {
                    if (placeSharedRootInFrontOnVR) PlaceInFrontOfCameraOnce(rt);
                    SaveVRSnapIfNeeded(rt);
                }
                //RestoreVRSnap(rt);
                MoveSharedRoot(vrContainer);
            }

            if (!vrOn && desktopPanelRoot)
            {
                var rt = desktopPanelRoot.transform as RectTransform;
                SaveOverlayIfNeeded(rt);
                RestoreOverlaySnap(rt);
            }
        }

        // 6) 기타 오브젝트 토글
        foreach (var go in enableWhenVR) if (go) go.SetActive(vrOn);
        foreach (var go in enableWhenDesktop) if (go) go.SetActive(!vrOn);

        if (vrOn && vrCanvas) EnsureRaycasterOnChildren(vrCanvas.transform);

        Canvas.ForceUpdateCanvases();

        // ▼ 상태 변경 시 구독자에게 통지
        if (prev != vrOn) ModeChanged?.Invoke(vrOn);

        OnModeChanged?.Invoke(vrOn);
    }

    void Reparent(RectTransform child, Transform newParent, bool worldPositionStays)
    {
        if (!child || !newParent) return;
        var prevScale = child.localScale;           // worldPositionStays=false일 때 스케일 보존
        child.SetParent(newParent, worldPositionStays);
        if (!worldPositionStays) child.localScale = prevScale;
        child.SetAsLastSibling();
        child.gameObject.SetActive(true);
    }

    void PlaceInFrontOfCameraOnce(RectTransform target)
    {
        if (!xrMainCamera || !target) return;
        var fwd = xrMainCamera.transform.forward;
        if (vrYawOnlyBillboard) { fwd.y = 0f; fwd.Normalize(); }

        var pos = xrMainCamera.transform.position + fwd * vrPlaceDistance;
        target.position = pos;

        Vector3 lookDir = target.position - xrMainCamera.transform.position;
        if (vrYawOnlyBillboard) lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 1e-4f)
            target.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

        var lockr = target.GetComponentInParent<WorldLock>();
        if (lockr) lockr.ArmNow();
    }

    void EnsureRaycasterOnChildren(Transform root)
    {
        if (!root) return;
        foreach (var c in root.GetComponentsInChildren<Canvas>(true))
            if (!c.TryGetComponent<TrackedDeviceGraphicRaycaster>(out _))
                c.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
    }

    // XRDisplaySubsystem 경로 전부 제거하고, HMD의 userPresence만 사용
    bool IsHmdWorn(out bool presenceSupported)
    {
        presenceSupported = false;

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid) return false; // 디바이스 자체가 없으면 미착용 취급

        // 표준 착용 센서(근접센서): 지원되면 가장 신뢰
        if (head.TryGetFeatureValue(XRCommonUsages.userPresence, out bool present))
        {
            presenceSupported = true;
            return present;
        }

        // 여기까지 왔다는 건 런타임이 userPresence를 보고하지 않는 경우.
        // 이때 false를 돌려버리면 ‘항상 PC로 꺼짐’이 되어 버려서, 자동 전환은 비활성처럼 동작시키기 위해 true 반환.
        // (위의 wornOk에서 presenceSupported ? worn : true 로 처리됨)
        return true;
    }

    
    public void SwitchToDesktop()
    {
        _force = ForceMode.Desktop;     // 자동 전환에 덮어씌움
        _curVrOn = false;
        ApplyMode(false);                

        MoveSharedRoot(desktopContainer);
    }
    

    public void SwitchToVR()
    {
        _force = ForceMode.VR;
        _curVrOn = true;
        ApplyMode(true);

        MoveSharedRoot(vrContainer);
    }

    public void ToggleMode()
    {
        if (desktopLock) return; // 잠금 시 토글 무시
        if (_curVrOn) SwitchToDesktop();
        else SwitchToVR();
    }

    void ResetRectForOverlay(RectTransform rt)
    {
        if (!rt) return;

        // 위치/회전/스케일 초기화 (Overlay 기준)
        rt.anchoredPosition3D = Vector3.zero;     // x,y,z = 0 (z도 0으로)
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        // 안전 차원에서 localPosition.z도 0으로
        var lp = rt.localPosition; lp.z = 0f; rt.localPosition = lp;
    }

    public void LockDesktop()
    {
        desktopLock = true;
        _force = ForceMode.Desktop;
        ApplyMode(false);         // 즉시 PC로
    }

    public void UnlockDesktopToAuto()
    {
        desktopLock = false;
        _force = ForceMode.None;  // 자동 전환 복귀
        ApplyMode(IsXRRunning());
    }

    static void ResetRect(RectTransform rt, bool stretch = true)
    {
        // 앵커/오프셋 초기화
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // 포즈 초기화 (Z 누적 방지 핵심)
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = Vector2.zero;
        rt.anchoredPosition3D = Vector3.zero; // ← Z=0 보장
        rt.localPosition = Vector3.zero;      // 안전빵
        rt.SetAsLastSibling();
    }

    void MoveSharedRoot(RectTransform newParent)
    {
        if (!sharedPageRoot || !newParent) return;

        sharedPageRoot.SetParent(newParent, worldPositionStays: false);
        ResetRect(sharedPageRoot, stretch: true);
    }

    Transform ResolveApplicant()
    {
        if (applicantRoot != null) return applicantRoot;

        // 인스펙터에서 안 넣어줬다면 이름으로 한 번 찾아본다.
        var obj = GameObject.Find("Applicant01");
        if (obj != null) applicantRoot = obj.transform;
        return applicantRoot;
    }

    void UpdateApplicantParent(bool vrOn)
    {
        var app = ResolveApplicant();
        if (app == null) return;

        Transform targetParent = null;

        if (vrOn)
        {
            if (xrMainCamera != null)
                targetParent = xrMainCamera.transform;
        }
        else
        {
            if (desktopCamera != null)
                targetParent = desktopCamera.transform;
        }

        if (targetParent == null) return;

        // 부모를 현재 모드 카메라로 옮기고, 카메라 기준 (0,0,0)에 위치시킨다.
        app.SetParent(targetParent, worldPositionStays: false);
        app.localPosition = Vector3.zero;
        app.localRotation = Quaternion.identity;
        // 스케일은 기존 값 유지
    }
}
