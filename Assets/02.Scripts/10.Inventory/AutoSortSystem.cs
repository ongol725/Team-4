using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// O 키로 인벤토리 아이템을 합성등급 → 희귀도 → 크기 기준으로 최적 재배치한다.
/// 함께 생성되는 버튼(AutoSortButton)은 단축키 도움말(Q와 동일)을 토글한다.
///
/// 사용법: 씬 내 아무 오브젝트(메인카메라 포함)에 부착.
///         InventoryGridUI 를 런타임에 자동 탐색한다.
/// </summary>
public class AutoSortSystem : MonoBehaviour
{
    [Header("단축키 정보 버튼 위치 (캔버스 좌하단 기준)")]
    [SerializeField] private Vector2 _buttonPos = new Vector2(478.18f, 279.35f);

    [Header("버튼 배경 스프라이트 (미지정 시 단색 배경 폴백)")]
    [SerializeField] private Sprite _buttonSprite;

    private InventoryGridUI _gridUI;
    private bool            _buttonCreated;

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        // 버튼은 InventoryGridUI 가 로드된 뒤 한 번만 생성
        if (!_buttonCreated) TryCreateButton();

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            RunAutoSort();
#else
        if (Input.GetKeyDown(KeyCode.O))
            RunAutoSort();
#endif
    }

    // ─────────────────────────────────────────────────────────────

    public void RunAutoSort()
    {
        if (_gridUI == null)
            _gridUI = FindAnyObjectByType<InventoryGridUI>();

        Debug.Log($"[AutoSort] gridUI={_gridUI}, following={_gridUI?.IsAnyFollowingMouse}");

        if (_gridUI == null) return;
        if (_gridUI.IsAnyFollowingMouse) return;

        _gridUI.AutoSortInventory();
    }

    // ─────────────────────────────────────────────────────────────
    // 버튼 생성

    private void TryCreateButton()
    {
        if (_gridUI == null)
            _gridUI = FindAnyObjectByType<InventoryGridUI>();
        if (_gridUI == null) return;

        var parentCanvas = _gridUI.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        CreateButton(parentCanvas.transform);
        _buttonCreated = true;
    }

    private void CreateButton(Transform canvasRoot)
    {
        Font font = Resources.Load<Font>("Fonts/Galmuri9")
                 ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── 버튼 배경 ──
        var btnGO = new GameObject("AutoSortButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasRoot, false);

        var rt           = btnGO.GetComponent<RectTransform>();
        rt.anchorMin     = new Vector2(0f, 0f);
        rt.anchorMax     = new Vector2(0f, 0f);
        rt.pivot         = new Vector2(0f, 0f);
        rt.sizeDelta     = new Vector2(120f, 34f);
        rt.anchoredPosition = _buttonPos;
        rt.localScale    = Vector3.one * 1.096982f;

        var bg = btnGO.GetComponent<Image>();
        if (_buttonSprite != null)
        {
            bg.sprite = _buttonSprite;
            bg.color  = Color.white; // 스프라이트 원본 색 그대로
        }
        else
        {
            bg.color = new Color(0.12f, 0.12f, 0.20f, 0.95f);

            var outline          = btnGO.AddComponent<Outline>();
            outline.effectColor  = new Color(0.40f, 0.45f, 0.70f, 0.90f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        // ── 텍스트 ──
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(btnGO.transform, false);

        var labelRt       = labelGO.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

        var txt             = labelGO.GetComponent<Text>();
        txt.font            = font;
        txt.text            = "단축키 정보 [Q]";
        txt.fontSize        = 14;
        txt.fontStyle       = FontStyle.Normal;
        txt.alignment       = TextAnchor.MiddleCenter;
        txt.color           = Color.white; // FFFFFF
        txt.raycastTarget   = false;

        // ── 버튼 이벤트 ──
        var btn             = btnGO.GetComponent<Button>();
        var colors          = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = colors;
        btn.onClick.AddListener(ShortcutHelpUI.Toggle);
    }
}
