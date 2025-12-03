using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiModeToggleButton : MonoBehaviour
{
    public UiModeSwitcher switcher;   
    public Button button;             
    public Image iconTarget;

    public Sprite spriteVrIcon;       // "VR로 전환" 아이콘
    public Sprite spritePcIcon;       // "PC로 전환" 아이콘

    void Awake()
    {
        if (!switcher) switcher = FindAnyObjectByType<UiModeSwitcher>(FindObjectsInactive.Include);
        if (!button) button = GetComponent<Button>();
        if (!iconTarget && button) iconTarget = button.image;
    }

    void OnEnable()
    {
        if (button) button.onClick.AddListener(OnClick);
        if (switcher) switcher.ModeChanged += OnModeChanged; // UiModeSwitcher가 이벤트 제공
        RefreshIcon();
    }

    void OnDisable()
    {
        if (button) button.onClick.RemoveListener(OnClick);
        if (switcher) switcher.ModeChanged -= OnModeChanged;
    }

    void OnClick()
    {
        if (!switcher) return;
        switcher.ToggleMode();   // 내부에서 ApplyMode 수행
        RefreshIcon();
    }
    void OnModeChanged(bool _) => RefreshIcon();

    void RefreshIcon()
    {
        if (!switcher || !iconTarget) return;
        bool isVr = switcher.IsVrMode;
        iconTarget.sprite = isVr ? spritePcIcon : spriteVrIcon;
    }
}
