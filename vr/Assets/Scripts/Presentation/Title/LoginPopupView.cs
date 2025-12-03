using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPopupView : MonoBehaviour
{
    [Header("Fields")]
    public TMP_InputField idInput;
    public TMP_InputField pwInput;
    public TMP_Text errorText;

    [Header("Buttons")]
    public Button btnLogin;
    public Button btnRegister;
    public Button btnCancel;

    void Update()
    {
        // 입력 필드가 활성화(포커스)된 상태에서만 처리
        if (idInput.isFocused || pwInput.isFocused)
        {
            HandleTabKey();
        }

        HandleEnterKey();
    }

    private void HandleTabKey()
    {
        // Tab 키가 눌렸을 때
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // Shift 키와 함께 눌렸으면: 역방향 (PW -> ID)
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (pwInput.isFocused && idInput != null)
                {
                    // Shift+Tab: 비밀번호 -> ID
                    idInput.ActivateInputField();
                    idInput.Select();
                }
            }
            // Tab 키만 눌렸으면: 정방향 (ID -> PW)
            else
            {
                if (idInput.isFocused && pwInput != null)
                {
                    // Tab: ID -> 비밀번호
                    pwInput.ActivateInputField();
                    pwInput.Select();
                }
            }
        }
    }

    private void HandleEnterKey()
    {
        // Enter 키 또는 Keypad Enter 키가 눌렸을 때
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (btnLogin.interactable)
            {
                // 로그인 버튼이 활성화되어 있다면 버튼 클릭 이벤트 호출
                btnLogin.onClick.Invoke();

                // 처리가 끝난 후 입력 포커스 해제
                idInput.DeactivateInputField();
                pwInput.DeactivateInputField();
            }
        }
    }

    // 처음 열릴 때 ID 입력란 포커스
    public void FocusIdNextFrame() => StartCoroutine(_Focus());
    IEnumerator _Focus() { yield return null; idInput?.ActivateInputField(); }

    public string Id => idInput ? idInput.text.Trim() : "";
    public string Pw => pwInput ? pwInput.text : "";
    public void SetError(string msg) { if (errorText) errorText.text = msg ?? ""; }
}
