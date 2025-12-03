using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    [Header("Hero")]
    public Image heroImage;             // 화면 중앙의 큰 이미지
    public Sprite defaultHero;          // 기본 스프라이트

    [HideInInspector] public MainMenuHoverCard current;  // 현재 호버 카드

    [SerializeField]
    private List<MainMenuHoverCard> _cards = new();

    void Awake()
    {
        foreach (var c in _cards) c.Bind(this);
    }

    public void Select(MainMenuHoverCard card)
    {
        if (current == card) return;
        if (current) current.SetHover(false);  // 기존 카드 언호버
        current = card;
        if (heroImage)
            heroImage.sprite = card && card.heroSprite ? card.heroSprite : defaultHero;
        if (card) card.SetHover(true);
    }

    public void Unselect(MainMenuHoverCard card)
    {
        if (current != card) { card.SetHover(false); return; }
        current = null;
        if (heroImage) heroImage.sprite = defaultHero;
        card.SetHover(false);
    }
}
