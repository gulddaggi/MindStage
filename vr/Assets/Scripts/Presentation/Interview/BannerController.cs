using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>상단 배너의 메시지 표기/토글을 관리하는 단순 UI 컨트롤러.</summary>

public class BannerController : MonoBehaviour
{
    public TMP_Text text;
    public void Show(string msg) { text.text = msg; gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }
}
