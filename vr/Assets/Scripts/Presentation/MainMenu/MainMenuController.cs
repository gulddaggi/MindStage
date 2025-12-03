using App.Infra;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


/// <summary>MainMenu: 카드 버튼으로 각 화면으로 이동하는 허브.</summary>

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button btnInterview;
    public Button btnResumes;
    public Button btnReports;
    public Button btnSettings;
    public Button btnQuit;
    public Button btnLogout;

    bool _navigating;

    bool _probed;

    private void Awake()
    {
        btnResumes.onClick.AddListener(OnClickResumes);
        btnInterview.onClick.AddListener(OnClickInterview);
        btnSettings.onClick.AddListener(OnClickSettings);
        btnReports.onClick.AddListener(OnClickReports);
        //btnQuit.onClick.AddListener(OnClickQuit);
        btnLogout.onClick.AddListener(OnClickLogout);
    }

    // void Start() => _ = ProbeSelfOnce();

    private async void OnClickResumes()
    {
        if (_navigating) return;
        _navigating = true;

        var btn = btnResumes;
        if (btn) btn.interactable = false;

        bool navigated = false;
        try
        {
            await App.Infra.SceneLoader.LoadSingleAsync(SceneIds.ResumeList);
            navigated = true; // 성공적으로 다른 씬으로 이동
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            // 씬 이동에 실패했거나(=navigated==false) 이 오브젝트/버튼이 아직 살아 있을 때만 되돌림
            if (!navigated && this && btn) btn.interactable = true;
            _navigating = false;
        }
    }

    private async void OnClickInterview()
    {
        if (_navigating) return;
        _navigating = true;

        var btn = btnInterview;
        if (btn) btn.interactable = false;

        bool navigated = false;
        try
        {
            await App.Infra.SceneLoader.LoadSingleAsync(SceneIds.InterviewPrepare);
            navigated = true; // 성공적으로 다른 씬으로 이동
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            // 씬 이동에 실패했거나(=navigated==false) 이 오브젝트/버튼이 아직 살아 있을 때만 되돌림
            if (!navigated && this && btn) btn.interactable = true;
            _navigating = false;
        }
    }

    private async void OnClickSettings()
    {
        if (_navigating) return;
        _navigating = true;

        var btn = btnSettings;
        if (btn) btn.interactable = false;

        bool navigated = false;
        try
        {
            await App.Infra.SceneLoader.LoadSingleAsync(SceneIds.Settings);
            navigated = true; // 성공적으로 다른 씬으로 이동
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            // 씬 이동에 실패했거나(=navigated==false) 이 오브젝트/버튼이 아직 살아 있을 때만 되돌림
            if (!navigated && this && btn) btn.interactable = true;
            _navigating = false;
        }
    }

    private async void OnClickReports()
    {
        if (_navigating) return;
        _navigating = true;

        var btn = btnReports;
        if (btn) btn.interactable = false;

        bool navigated = false;
        try
        {
            await App.Infra.SceneLoader.LoadSingleAsync(SceneIds.ResultsList);
            navigated = true; // 성공적으로 다른 씬으로 이동
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            // 씬 이동에 실패했거나(=navigated==false) 이 오브젝트/버튼이 아직 살아 있을 때만 되돌림
            if (!navigated && this && btn) btn.interactable = true;
            _navigating = false;
        }
    }

    void OnClickQuit()
    {
        // 저장 등 필요한 정리 없으면 바로 종료
        PlayerPrefs.Save();         // 선택
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private async void OnClickLogout()
    {
        if (_navigating) return;
        _navigating = true;

        var btn = btnLogout;
        if (btn) btn.interactable = false;

        bool navigated = false;
        try
        {
            var (ok, msg) = await App.Infra.Services.Auth.LogoutAsync();
            if (!ok) Debug.LogWarning($"Logout result: {msg}");

            await App.Infra.SceneLoader.LoadSingleAsync(App.Infra.SceneIds.Title);
            navigated = true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            if (!navigated && this && btn) btn.interactable = true;
            _navigating = false;
        }
    }

    async Task ProbeSelfOnce()
    {
        if (_probed) return;
        _probed = true;

        var svc = App.Infra.Services.User;
        if (svc == null) { Debug.LogWarning("[MainMenu] IUserService not registered"); return; }

        var (ok, me, msg) = await svc.GetMeAsync();
        if (ok)
            Debug.Log($"[ME] id={me.userId}, name={me.name}, email={me.email}, role={me.role}");
        else
            Debug.LogWarning($"[ME] 실패: {msg}");
    }
}
