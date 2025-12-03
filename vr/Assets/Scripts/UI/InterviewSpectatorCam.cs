using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InterviewSpectatorCam : MonoBehaviour
{
    [Header("고정 관찰 시점(면접 공간을 내려다보는 위치)")]
    public Transform fixedAnchor;

    [Header("HMD 카메라 (XR Origin/Main Camera)")]
    public Camera xrMainCamera;

    [Header("초기 상태")]
    [Tooltip("시작할 때 고정 관찰 시점으로 둘지 여부")]
    public bool startInFixedMode = true;

    [Header("입력 설정")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("오른손 컨트롤러 A 버튼에 바인딩된 액션 (선택)")]
    public InputActionReference toggleAction;   // 예: RightHand A 버튼 액션
#endif

    [Tooltip("키보드 Tab으로도 토글할지 여부")]
    public bool enableKeyboardToggle = true;

    // true = 고정 카메라 모드, false = HMD 따라가기 모드
    bool _useFixed;

    void OnEnable()
    {
        _useFixed = startInFixedMode;

        if (_useFixed) SnapToAnchor();
        else SnapToHmd();

#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed += OnTogglePerformed;
            toggleAction.action.Enable();
        }
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnTogglePerformed;
        }
#endif
    }

    void Update()
    {
        // --- 키보드 Tab 토글 ---
        if (enableKeyboardToggle && Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMode();
        }

        // --- HMD 따라가기 모드일 때는 계속 추적 ---
        if (!_useFixed)
        {
            SnapToHmd();
        }
        // 고정 모드는 스냅 한 번이면 되므로 매 프레임 갱신할 필요 없음
    }

#if ENABLE_INPUT_SYSTEM
    void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            ToggleMode();
    }
#endif

    void ToggleMode()
    {
        _useFixed = !_useFixed;

        if (_useFixed)  // 고정 모드
            SnapToAnchor();
        else            // HMD 1인칭 모드
            SnapToHmd();
    }

    public void SnapToAnchor()
    {
        if (!fixedAnchor) return;
        transform.position = fixedAnchor.position;
        transform.rotation = fixedAnchor.rotation;
    }

    void SnapToHmd()
    {
        if (!xrMainCamera) return;
        transform.position = xrMainCamera.transform.position;
        transform.rotation = xrMainCamera.transform.rotation;
    }
}