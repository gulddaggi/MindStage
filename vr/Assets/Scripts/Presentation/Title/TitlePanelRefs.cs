using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TitlePanelRefs : MonoBehaviour
{
    [Header("Root Panels")]
    public GameObject panelTitle;      // 타이틀 패널 프리팹 인스턴스
    public GameObject panelLogin;      // 로그인 패널 프리팹 인스턴스
    public GameObject panelRegister;   // 회원가입 패널 프리팹 인스턴스

    [Header("Title Buttons")]
    public Button btnStart;       // 타이틀 → 로그인
    public Button btnQuit;             // 종료

    [Header("Login Panel View (패널 오브젝트에 붙어있음)")]
    public LoginPopupView loginView;   // 기존 LoginPopupView를 로그인 패널에 붙여서 사용

    [Header("Register Panel View")]
    public RegisterPanelView registerView; // 새로 추가(아래 코드 참고)

    [Header("UI Mode Toggle")]
    public Button btnToggleUi;         // PC↔VR 전환 버튼
    public Sprite spritePcIcon;        // 현재 VR일 때 보여줄 “PC 아이콘”
    public Sprite spriteVrIcon;        // 현재 PC일 때 보여줄 “VR 아이콘”
}
