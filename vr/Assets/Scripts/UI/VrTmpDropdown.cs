using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class VrTmpDropdown : TMP_Dropdown
{
    [Header("VR용 드롭다운 정렬 설정")]
    [SerializeField] private bool dropdownOverrideSorting = false;
    [SerializeField] private int dropdownSortingOrder = 0;

    // 드롭다운 리스트 캔버스 세팅
    protected override GameObject CreateDropdownList(GameObject template)
    {
        var go = base.CreateDropdownList(template);

        var canvas = go.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = dropdownOverrideSorting;
            canvas.sortingOrder = dropdownSortingOrder;
        }

        return go;
    }

    // Blocker 생성 (밖 클릭 시 닫힘)
    protected override GameObject CreateBlocker(Canvas rootCanvas)
    {
        var blocker = base.CreateBlocker(rootCanvas);

        var canvas = blocker.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            // Blocker는 리스트 바로 아래에 위치 (루트 캔버스보다 위)
            canvas.sortingOrder = 0;
        }

        // XR용 Raycaster만 추가
        if (blocker.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
        {
            blocker.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        return blocker;
    }

    // 드롭다운 버튼 다시 클릭 시 토글 (열려 있으면 닫기)
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!IsActive() || !IsInteractable())
            return;

        if (IsExpanded)
        {
            // 이미 열려 있으면 닫기
            Hide();
        }
        else
        {
            // 닫혀 있으면 열기
            Show();
        }
    }
}
