using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionManager : MonoBehaviour
{
    public static ResolutionManager Instance { get; private set; }
    public event System.Action OnSettingsChanged;

    [Header("Defaults")]
    public Vector2Int referenceResolution = new(2880, 1800);
    public float targetAspect = 2880f / 1800f;

    [Header("UI (선택 연결)")]
    public Dropdown resolutionDropdown;          // Unity UI Dropdown (TMP면 코드 조금 바꿔도 됨)
    public Dropdown modeDropdown;                // 0:Windowed, 1:Borderless, 2:Exclusive
    public Toggle fullscreenToggle;              // 토글로 전체화면 on/off 쓰고 싶으면 연결

    // 내부
    Resolution[] _resList;
    int _selIndex;

    const string KEY_W = "res_w";
    const string KEY_H = "res_h";
    const string KEY_MODE = "res_mode"; // 0=W,1=B,2=E

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BuildResList();
        BuildUI();
        ApplyCurrent(save: false);
    }

    void BuildResList()
    {
        _resList = Screen.resolutions
            .GroupBy(r => (r.width, r.height))
            .Select(g => g.First())
            .OrderByDescending(r => r.width * r.height)
            .ToArray();

        if (_resList.Length == 0) _resList = new[] { Screen.currentResolution };

        var curW = Screen.width; var curH = Screen.height;
        _selIndex = System.Array.FindIndex(_resList, r => r.width == curW && r.height == curH);
        if (_selIndex < 0) _selIndex = 0;
    }

    void BuildUI()
    {
        if (resolutionDropdown)
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(_resList.Select(r => $"{r.width} x {r.height}").ToList());
            resolutionDropdown.value = _selIndex;
            resolutionDropdown.onValueChanged.AddListener(i => { _selIndex = i; ApplyCurrent(); });
        }

        if (modeDropdown)
        {
            modeDropdown.ClearOptions();
            modeDropdown.AddOptions(new System.Collections.Generic.List<string> { "Windowed", "Borderless", "Exclusive" });
            modeDropdown.onValueChanged.AddListener(_ => ApplyCurrent());
        }

        if (fullscreenToggle)
            fullscreenToggle.onValueChanged.AddListener(_ => ApplyCurrent());
    }

    public void ApplyCurrent(bool save = true)
    {
        var r = _resList[Mathf.Clamp(_selIndex, 0, _resList.Length - 1)];

        int modeIdx = modeDropdown ? modeDropdown.value : 1;
        bool fs = fullscreenToggle ? fullscreenToggle.isOn : Screen.fullScreen;
        var mode = FullScreenMode.FullScreenWindow; // Borderless 기본
        switch (modeIdx)
        {
            case 0: mode = FullScreenMode.Windowed; fs = false; break;
            case 1: mode = FullScreenMode.FullScreenWindow; fs = true; break;
            case 2: mode = FullScreenMode.ExclusiveFullScreen; fs = true; break;
        }

        Screen.SetResolution(r.width, r.height, mode, r.refreshRateRatio);
        Screen.fullScreen = fs;

        // 모든 CanvasScalerAutoMatch에게 알림
        OnSettingsChanged?.Invoke();
    }
}