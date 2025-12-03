using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class MainMenuHoverCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Refs")]
    public Image fillImage;                 // ← type=Filled 로 설정 (Horizontal 또는 Radial)
    public TMP_Text label;
    public TMP_Text arrow;
    public Sprite heroSprite;               // ← 이 카드에 대응하는 중앙 이미지

    [Header("Colors & Anim")]
    public Color textNormal = new(1, 1, 1, 1); // 기본 텍스트 색
    public Color textHover = Color.black;  // 호버 시 텍스트 색
    public float fillDuration = 0.22f;      // 채우기 시간(초)

    MainMenuView _view;
    Button _btn;
    Coroutine _running;

    public void Bind(MainMenuView view)
    {
        _view = view;
        _btn = GetComponent<Button>();
        // 초기 상태 정리
        if (fillImage) fillImage.fillAmount = 0f;
        if (label) label.color = textNormal;
        if (arrow) arrow.color = textNormal;
    }

    void OnEnable()
    {
        if (!_view) _view = GetComponentInParent<MainMenuView>(true);
        if (!_btn) _btn = GetComponent<Button>();
        if (_view && !_view.current) // 초기엔 모두 비활성 룩
        {
            if (fillImage) fillImage.fillAmount = 0f;
            if (label) label.color = textNormal;
            if (arrow) arrow.color = textNormal;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_view) _view.Select(this);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        // XR/마우스 이동에 따라 Exit이 연속으로 올 수 있음: 현재 선택 카드가 내가 아니면 바로 언호버
        if (_view && _view.current != this) SetHover(false);
    }

    public void OnSelect(BaseEventData eventData) => OnPointerEnter(null);
    public void OnDeselect(BaseEventData eventData) => OnPointerExit(null);

    public void SetHover(bool on)
    {
        if (label) label.color = on ? textHover : textNormal;
        if (arrow) arrow.color = on ? textHover : textNormal;
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(AnimateFill(on ? 1f : 0f));
    }

    IEnumerator AnimateFill(float target)
    {
        if (!fillImage) yield break;
        float start = fillImage.fillAmount;
        float t = 0f;
        while (t < fillDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fillDuration);
            fillImage.fillAmount = Mathf.Lerp(start, target, k);
            yield return null;
        }
        fillImage.fillAmount = target;
    }
}
