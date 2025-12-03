using App.Infra;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>타이틀 화면 제어. 로그인 팝업 표시/검증 후 메인으로 전환(로그인 필수 게이트).</summary>

public class TitleController : MonoBehaviour
{
    [SerializeField] private TitlePanelRefs view;
    [SerializeField] private UiModeSwitcher switcher;

    private bool _bound;

    enum Panel { Title, Login, Register }

    void Awake()
    {
        if (!view)
            view = FindAnyObjectByType<TitlePanelRefs>(FindObjectsInactive.Include);
        if (!switcher)
            switcher = FindAnyObjectByType<UiModeSwitcher>(FindObjectsInactive.Include);

        if (!view) Debug.LogError("[Title] TitlePanelRefs가 없습니다.");
    }

    void OnEnable() => Bind();
    void OnDisable() => Unbind();

    void Bind()
    {
        if (_bound || !view) return;

        // 타이틀 버튼
        if (view.btnStart) view.btnStart.onClick.AddListener(() => Show(Panel.Login));
        if (view.btnQuit) view.btnQuit.onClick.AddListener(
            () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        ); 

        // 로그인 패널
        if (view.loginView)
        {
            view.loginView.SetError("");
            if (view.loginView.btnLogin) view.loginView.btnLogin.onClick.AddListener(() => _ = DoLoginAsync());
            if (view.loginView.btnRegister) view.loginView.btnRegister.onClick.AddListener(() => Show(Panel.Register)); // “회원가입”으로 이동
            if (view.loginView.btnCancel) view.loginView.btnCancel.onClick.AddListener(() => Show(Panel.Title));     // ← 타이틀로
        }

        // 회원가입 패널
        if (view.registerView)
        {
            view.registerView.SetError("");
            if (view.registerView.btnSubmit) view.registerView.btnSubmit.onClick.AddListener(() => _ = DoRegisterAsync());
            if (view.registerView.btnToLogin) view.registerView.btnToLogin.onClick.AddListener(() => Show(Panel.Login));
            if (view.registerView.btnReset) view.registerView.btnReset.onClick.AddListener(() => view.registerView.ResetFields());
            if (view.registerView.btnCheckDup) view.registerView.btnCheckDup.onClick.AddListener(() => _ = DoCheckEmailAsync());
        }

        // 초기 패널은 타이틀
        Show(Panel.Title);

        _bound = true;
    }

    void Unbind()
    {
        if (!_bound || !view) return;

        if (view.btnStart) view.btnStart.onClick.RemoveAllListeners();
        if (view.btnQuit) view.btnQuit.onClick.RemoveAllListeners();

        if (view.loginView)
        {
            if (view.loginView.btnLogin) view.loginView.btnLogin.onClick.RemoveAllListeners();
            if (view.loginView.btnRegister) view.loginView.btnRegister.onClick.RemoveAllListeners();
            if (view.loginView.btnCancel) view.loginView.btnCancel.onClick.RemoveAllListeners();
        }

        if (view.registerView)
        {
            if (view.registerView.btnSubmit) view.registerView.btnSubmit.onClick.RemoveAllListeners();
            if (view.registerView.btnToLogin) view.registerView.btnToLogin.onClick.RemoveAllListeners();
            if (view.registerView.btnReset) view.registerView.btnReset.onClick.RemoveAllListeners();
        }

        _bound = false;
    }

    // 패널 전환
    void Show(Panel p)
    {
        if (!view) return;

        if (view.panelTitle) view.panelTitle.SetActive(p == Panel.Title);
        if (view.panelLogin) view.panelLogin.SetActive(p == Panel.Login);
        if (view.panelRegister) view.panelRegister.SetActive(p == Panel.Register);

        // ── 잠금 정책: 로그인/회원가입 동안은 PC 고정 ──
        if (switcher)
        {
            bool needLock = (p == Panel.Login) || (p == Panel.Register);
            if (needLock) switcher.LockDesktop();
            else switcher.UnlockDesktopToAuto();
        }

        // 포커스/메시지 초기화
        if (p == Panel.Login && view.loginView)
        {
            view.loginView.SetError("");
            view.loginView.FocusIdNextFrame();
        }
        else if (p == Panel.Register && view.registerView)
        {
            view.registerView.SetError("");
            view.registerView.UseDefaultErrorColor();
            view.registerView.FocusEmailNextFrame();
        }
    }

    // 인증
    async Task DoLoginAsync()
    {
        if (view?.loginView == null) return;

        var id = view.loginView.Id;
        var pw = view.loginView.Pw;

        var res = await Services.Auth.LoginAsync(id, pw);
        if (!res.ok) { view.loginView.SetError(res.message); return; }

        await SceneLoader.LoadSingleAsync(SceneIds.MainMenu);
    }

    async Task DoRegisterAsync()
    {
        if (view?.registerView == null) return;

        string email = view.registerView.Email;
        string pw = view.registerView.Password;
        string pw2 = view.registerView.PasswordConfirm;
        string name = view.registerView.UserName;

        // ── 클라이언트 검증 (OpenAPI 기준) ──
        if (string.IsNullOrWhiteSpace(email))
        { view.registerView.SetError("이메일을 입력하세요."); return; }

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 10 || !Regex.IsMatch(name, "^[가-힣a-zA-Z]+$"))
        { view.registerView.SetError("이름은 2~10자 한글/영문만 가능합니다."); return; }

        if (pw != pw2)
        { view.registerView.SetError("비밀번호 확인이 일치하지 않습니다."); return; }

        // 최소 강도(서버 정규식 요약): 8~20자, 대/소문자/숫자/특수문자 포함
        if (pw.Length < 8 || pw.Length > 20 ||
            !Regex.IsMatch(pw, "[a-z]") ||
            !Regex.IsMatch(pw, "[A-Z]") ||
            !Regex.IsMatch(pw, "\\d") ||
            !Regex.IsMatch(pw, "[@$!%*?&]"))
        {
            view.registerView.SetError("비밀번호는 8~20자, 대/소문자/숫자/특수문자를 모두 포함해야 합니다.");
            return;
        }

        // ── 호출 ──
        var res = await Services.Auth.RegisterAsync(email, pw, name);
        if (!res.ok)
        {
            // 409 등 서버 메시지 그대로 노출
            view.registerView.SetError(res.message);
            return;
        }

        // 가입 성공 → 로그인 화면으로 이동 및 이메일 채워두기
        Show(Panel.Login);
        if (view.loginView)
        {
            view.loginView.SetError("");
            view.loginView.idInput.text = email;
            view.loginView.FocusIdNextFrame();
        }
    }

    // =============== UI 모드 전환 버튼/아이콘 ===============
    void OnClickToggleUi()
    {
        if (!switcher)
            switcher = FindAnyObjectByType<UiModeSwitcher>(FindObjectsInactive.Include);

        if (!switcher) { Debug.LogWarning("[Title] UiModeSwitcher not found"); return; }

        switcher.ToggleMode();   // 내부에서 ApplyMode + 상태 고정
        UpdateToggleUiIcon();
    }

    void OnUiModeChanged(bool _) => UpdateToggleUiIcon();

    void UpdateToggleUiIcon()
    {
        if (!view || !view.btnToggleUi) return;
        var icon = view.btnToggleUi.image;
        if (!icon) return;

        bool isVr = switcher && switcher.IsVrMode;
        // 현재 모드가 VR이면 “PC로 전환” 아이콘을 보여주고, 반대면 “VR로 전환”
        icon.sprite = isVr ? view.spritePcIcon : view.spriteVrIcon;
    }

    async Task DoCheckEmailAsync()
    {
        if (view?.registerView == null) return;
        var v = view.registerView;

        string email = v.Email;
        if (string.IsNullOrWhiteSpace(email))
        { v.SetError("이메일을 입력하세요."); return; }

        // 간단 형식 검증
        // (정규식은 필요시 더 강화)
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        { v.SetError("올바른 이메일 형식이 아닙니다."); return; }

        // 중복 호출 방지: 버튼 잠시 비활성
        var btn = v.btnCheckDup;
        if (btn) btn.interactable = false;

        var (ok, available, message) = await Services.Auth.CheckEmailAvailableAsync(email);

        if (ok)
        {
            if (available) v.SetSuccessMessage("사용 가능한 이메일입니다.");
            else v.SetError("이미 사용 중인 이메일입니다.");
        }
        else
        {
            v.SetError(string.IsNullOrWhiteSpace(message) ? "확인 중 오류가 발생했습니다." : message);
        }

        if (btn) btn.interactable = true;
    }
}
