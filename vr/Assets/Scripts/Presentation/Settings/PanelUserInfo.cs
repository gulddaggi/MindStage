using App.Auth;
using App.Infra;
using App.Services;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Presentation.Settings
{
    public class PanelUserInfo : MonoBehaviour
    {
        public TMP_Text nameValueText;

        public TMP_InputField curPwInput;
        public Button btnPwCheck;
        public TMP_InputField newPwInput;

        public Button btnBack;
        public Button btnHome;
        public Button btnSavePw;

        public TMP_Text resultText;

        IUserService _user;
        IAuthService _auth;

        readonly Color _ok = new Color(0.20f, 0.90f, 0.40f);
        readonly Color _bad = new Color(0.95f, 0.20f, 0.20f);
        readonly Color _info = new Color(0.85f, 0.85f, 0.85f);

        Coroutine _fadeCo;

        void Awake()
        {
            _user = App.Infra.Services.Resolve<IUserService>();
            _auth = App.Infra.Services.Resolve<IAuthService>();

            btnPwCheck.onClick.AddListener(OnCheckCurrentPw);
            btnSavePw.onClick.AddListener(OnSaveNewPw);
            btnBack.onClick.AddListener(() =>
                GetComponentInParent<PanelRouter>(true).Show(SettingsPanel.Main));
            btnHome.onClick.AddListener(async () =>
                await SceneLoader.LoadSingleAsync(SceneIds.MainMenu));
        }

        async void OnEnable()
        {
            // 이름 채우기
            try
            {
                var me = await _user.GetMeAsync();   // UserDto 반환
                nameValueText.text = me.user.name ?? "(이름 없음)";
            }
            catch { nameValueText.text = "(오류)"; }
            curPwInput.text = "";
            newPwInput.text = "";
            ClearMsg();
        }

        void OnCheckCurrentPw()
        {
            var ok = PlayerPrefs.GetString("auth.pwHash", "") == Hash(curPwInput.text);
            var c = ok ? _ok : _bad;
            curPwInput.textComponent.color = c;

            // 확인 결과도 표시(짧게)
            ShowMsg(ok ? "현재 비밀번호 확인 완료" : "현재 비밀번호가 일치하지 않습니다.", c, 1.5f);

            if (ok) newPwInput.ActivateInputField();
        }

        async void OnSaveNewPw()
        {
            // 현재 비번 확인 먼저
            if (PlayerPrefs.GetString("auth.pwHash", "") != Hash(curPwInput.text))
            {
                ShowMsg("현재 비밀번호가 일치하지 않습니다.", _bad, 2.0f);
                return;
            }
            if (string.IsNullOrWhiteSpace(newPwInput.text))
            {
                ShowMsg("새 비밀번호를 입력하세요.", _bad, 2.0f);
                return;
            }

            btnSavePw.interactable = false;
            ShowMsg("비밀번호 변경 중…", _info, holdSeconds: 60f); // 진행 중 메시지(길게 유지)
            try
            {
                var (ok, msg) = await _auth.ChangePasswordAsync(curPwInput.text, newPwInput.text);
                Debug.Log($"[ChangePassword] {ok}, {msg}");

                if (ok)
                {
                    curPwInput.text = "";
                    newPwInput.text = "";
                    ShowMsg("비밀번호가 변경되었습니다.", _ok, 2.5f);
                }
                else
                {
                    ShowMsg(string.IsNullOrEmpty(msg) ? "비밀번호 변경 실패" : msg, _bad, 3.0f);
                }
            }
            finally { btnSavePw.interactable = true; }
        }

        void ShowMsg(string message, Color color, float holdSeconds = 2.0f)
        {
            if (!resultText)
            {
                Debug.Log(message);
                return;
            }
            if (_fadeCo != null) StopCoroutine(_fadeCo);

            var c = color; c.a = 1f;
            resultText.color = c;
            resultText.text = message;

            _fadeCo = StartCoroutine(CoFadeOut(holdSeconds));
        }

        IEnumerator CoFadeOut(float holdSeconds)
        {
            yield return new WaitForSeconds(holdSeconds);
            float t = 0f, dur = 0.35f;
            var baseColor = resultText.color;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / dur);
                resultText.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }
        }

        void ClearMsg()
        {
            if (!resultText) return;
            var c = resultText.color; c.a = 0f;
            resultText.color = c;
            resultText.text = "";
        }

        static string Hash(string s)
        {
            using var sha = SHA256.Create();
            var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
            var sb = new StringBuilder();
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
