using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RegisterPanelView : MonoBehaviour
{
    [Header("Fields")]
    public TMP_InputField emailInput;
    public TMP_InputField pwInput;
    public TMP_InputField pwConfirmInput;
    public TMP_InputField nameInput;
    public TMP_Text errorText;

    [Header("Buttons")]
    public Button btnSubmit;     // 회원가입 실행
    public Button btnToLogin;    // 로그인 화면으로
    public Button btnReset;      // 폼 초기화
    public Button btnCheckDup;   // 중복 확인

    [Header("Colors")]
    public Color successColor = new Color(0.1f, 0.75f, 0.1f); // 초록
    Color _defaultErrorColor;

    void Awake()
    {
        if (errorText) _defaultErrorColor = Color.red; // 기본(빨강) 저장
    }

    public string Email => emailInput ? emailInput.text.Trim() : "";
    public string Password => pwInput ? pwInput.text : "";
    public string PasswordConfirm => pwConfirmInput ? pwConfirmInput.text : "";
    public string UserName => nameInput ? nameInput.text.Trim() : "";

    public void SetError(string msg)
    {
        if (!errorText) return;
        errorText.color = _defaultErrorColor;
        errorText.text = msg ?? "";
    }

    public void SetSuccessMessage(string msg)
    {
        if (!errorText) return;
        errorText.color = successColor;
        errorText.text = msg ?? "";
    }

    public void UseDefaultErrorColor()
    {
        if (errorText) errorText.color = _defaultErrorColor;
    }

    public void ResetFields()
    {
        if (emailInput) emailInput.text = "";
        if (pwInput) pwInput.text = "";
        if (pwConfirmInput) pwConfirmInput.text = "";
        if (nameInput) nameInput.text = "";
        SetError("");
    }

    public void FocusEmailNextFrame() => StartCoroutine(_Focus());
    System.Collections.IEnumerator _Focus()
    { yield return null; emailInput?.ActivateInputField(); }
}
