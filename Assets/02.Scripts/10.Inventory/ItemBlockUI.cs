using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 위에 올라간 아이템 블록 하나를 담당한다.
///
/// ▶ 마우스 추적 상태 (_isFollowingMouse):
///   - 좌클릭 → 셀 상태에 따라 배치 / 합성 / 스왑 처리
///   - 우클릭 → 취소(파괴)
///
/// ▶ 배치 완료 상태 (_isPlaced):
///   - IPointerDownHandler → 다시 집어서 재배치
///
/// ▶ 임시칸 상태 (_isInTempSlot):
///   - IPointerDownHandler → 임시칸에서 꺼내어 재배치
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ItemBlockUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 씬 전반에서 하나만 생성해 재사용
    private static Material _silhouetteMat;

    private static Material SilhouetteMat
    {
        get
        {
            if (_silhouetteMat != null) return _silhouetteMat;
            var shader = Shader.Find("Custom/UISilhouette");
            if (shader == null) return null;
            _silhouetteMat = new Material(shader) { name = "UISilhouette_Shared" };
            return _silhouetteMat;
        }
    }

    private ItemInstance    _instance;
    private InventoryGridUI _gridUI;
    private InventoryGrid   _grid;
    private int             _cellSize;

    public ItemInstance Instance => _instance;

    private RectTransform _rt;
    private bool _isFollowingMouse;
    private bool _isPlaced;
    private bool _isInTempSlot;
    private TempSlotUI _tempSlot;
    private bool _placementInputGuard;
    private int  _lastRingGradeBonus = -1;

    // 우클릭 취소 시 복원을 위한 원래 위치 정보
    private enum OriginType { None, Grid, TempSlot }
    private OriginType _originType    = OriginType.None;
    private Vector2Int _originCell;
    private TempSlotUI _originTempSlot;

    // 상점 구매 출처 (우클릭 취소 시 환불·슬롯 복원용)
    private ShopSlotUI _shopSlot;
    private int        _shopRefundCost;

    private readonly List<GameObject> _cellOutlines = new();

    // 희귀도별 테두리 색상 (무기용)
    private static readonly Color[] RarityColors =
    {
        new Color(1.00f, 1.00f, 1.00f),  // Common  → 흰색
        new Color(0.30f, 0.55f, 1.00f),  // Rare    → 파랑
        new Color(0.65f, 0.25f, 0.95f),  // Epic    → 보라
        new Color(1.00f, 0.80f, 0.10f),  // Legendary → 노랑
    };

    // 무기 외 아이템 배경색 (기존 방식 유지)
    private static readonly Color[] BgColors =
    {
        new Color(0.70f, 0.70f, 0.70f),  // Common
        new Color(0.30f, 0.55f, 1.00f),  // Rare
        new Color(0.65f, 0.25f, 0.95f),  // Epic
        new Color(1.00f, 0.80f, 0.10f),  // Legendary
    };

    // ─────────────────────────────────────────────────────────────

    public void Initialize(ItemInstance instance, InventoryGridUI gridUI,
                           InventoryGrid grid, int cellSize)
    {
        _instance = instance;
        _gridUI   = gridUI;
        _grid     = grid;
        _cellSize = cellSize;
        _rt       = GetComponent<RectTransform>();

        _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f);
        _rt.pivot     = new Vector2(0f, 1f);

        BuildVisuals();

        // 임시칸 RefreshPositions가 sizeDelta로 아이템 크기를 읽으므로 셀 범위로 설정
        var cells = InventoryGrid.GetCells(_instance.data);
        int maxRow = 0, maxCol = 0;
        foreach (var c in cells)
        {
            if (c.x > maxRow) maxRow = c.x;
            if (c.y > maxCol) maxCol = c.y;
        }
        _rt.sizeDelta = new Vector2((maxCol + 1) * _cellSize, (maxRow + 1) * _cellSize);

        _placementInputGuard = false;
        SetFollowing(true);
    }

    /// <summary>상점 구매 출처를 저장한다. BeginPlaceFromShop 직후 호출.</summary>
    public void SetShopSource(ShopSlotUI slot, int refundCost)
    {
        _shopSlot       = slot;
        _shopRefundCost = refundCost;
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        // 배치 상태에서 반지 인접 버프가 바뀌면 비주얼 갱신
        if (_isPlaced && _instance.RingGradeBonus != _lastRingGradeBonus)
        {
            _lastRingGradeBonus = _instance.RingGradeBonus;
            RefreshVisuals();
        }

        if (!_isFollowingMouse) return;

        var mouse    = Mouse.current;
        var mousePos = (Vector2)mouse.position.ReadValue();

        _rt.position = mousePos;

        var cell = _gridUI.ScreenToCell(mousePos);
        if (cell.HasValue)
            _gridUI.HighlightPlacement(_instance, cell.Value);
        else
            _gridUI.ClearHighlight();

        // 구매 직후 클릭과 구분하기 위한 1프레임 딜레이
        if (!_placementInputGuard) { _placementInputGuard = true; return; }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (cell.HasValue)
                HandlePlacement(cell.Value);
            else if (IsMouseOverSellSlot())
                SellAndDestroy();
            else if (IsMouseOverTempSlot())
            {
                var tempSlot    = _gridUI.TempSlot;
                var mergeTarget = tempSlot?.FindMergeTarget(_instance);
                if (mergeTarget != null)
                    TrySynthesizeInTempSlot(mergeTarget);
                else
                    SendToTempSlot();
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            SetFollowing(false);
            _gridUI.ClearHighlight();

            if (_originType == OriginType.Grid)
            {
                // 그리드 원위치 복원
                if (_grid.TryPlace(_instance, _originCell))
                    SnapToGrid(_originCell);
                else
                    SendToTempSlot(); // 복원 불가 시 임시칸으로
            }
            else if (_originType == OriginType.TempSlot && _originTempSlot != null)
            {
                // 임시칸 원위치 복원
                _originTempSlot.ReceiveBlock(this);
                _gridUI.OnItemSentToTempSlot(this);
            }
            else
            {
                // 상점에서 구매한 신규 아이템 → 취소: 골드 환불 + 슬롯 복원
                if (_shopRefundCost > 0)
                    GameManager.Instance?.AddGold(_shopRefundCost);
                _shopSlot?.RestoreFromSoldOut();
                _gridUI.OnPlacementCancelled(_instance);
                Destroy(gameObject);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 배치 분기

    private void HandlePlacement(Vector2Int origin)
    {
        // ── 인벤토리 블록: 잠긴 셀에 배치 → 활성화 후 소멸 ──
        if (_instance.data is SO_InventoryBlockData)
        {
            if (_grid.IsValidBlockExpansion(_instance, origin))
            {
                SetFollowing(false);
                _grid.ExpandWithBlock(_instance, origin);
                _gridUI.RefreshCellColors();
                _gridUI.OnPlacementCancelled(_instance);
                Destroy(gameObject);
            }
            return;
        }

        var overlaps = FindAllOverlappingInstances(origin);

        if (overlaps.Count == 0)
        {
            // 빈 칸 → 일반 배치
            if (_grid.TryPlace(_instance, origin))
                SnapToGrid(origin);
        }
        else if (overlaps.Count == 1
              && overlaps[0].data       == _instance.data
              && overlaps[0].gradeIndex == _instance.gradeIndex)
        {
            // 같은 종류 + 같은 등급 단독 → 합성
            TrySynthesize(overlaps[0]);
        }
        else
        {
            // 하나 또는 여러 다른 아이템 → 스왑
            TryMultiSwap(overlaps, origin);
        }
    }

    /// <summary>origin 기준으로 겹치는 모든 고유 ItemInstance 목록 반환</summary>
    private System.Collections.Generic.List<ItemInstance> FindAllOverlappingInstances(Vector2Int origin)
    {
        var result = new System.Collections.Generic.List<ItemInstance>();
        foreach (var local in InventoryGrid.GetCells(_instance.data))
        {
            var world = origin + local;
            var inst  = _grid.GetInstanceAt(world);
            if (inst != null && !result.Contains(inst))
                result.Add(inst);
        }
        return result;
    }

    // ─────────────────────────────────────────────────────────────
    // 합성

    private void TrySynthesize(ItemInstance target)
    {
        if (!target.TryUpgrade()) return; // 5등급이면 합성 불가

        SetFollowing(false);
        _gridUI.RefreshItemBlockVisual(target);
        _gridUI.OnPlacementCancelled(_instance);
        Destroy(gameObject);
    }

    private void TrySynthesizeInTempSlot(ItemBlockUI targetBlock)
    {
        if (!targetBlock.Instance.TryUpgrade()) return; // 최고 등급이면 합성 불가

        SetFollowing(false);
        _gridUI.OnPlacementCancelled(_instance);
        targetBlock.RefreshVisuals();
        Destroy(gameObject);
    }

    /// <summary>
    /// 임시칸 아이템 우클릭 처리: 합성 우선 → 그리드 자동 배치.
    /// 빈 공간도 없으면 임시칸에 그대로 유지.
    /// </summary>
    private void TrySmartPlaceFromTempSlot()
    {
        // 1순위: 그리드에서 합성 가능한 아이템 탐색
        if (_instance.HasGrades && _instance.gradeIndex < 4)
        {
            foreach (var existing in _grid.GetAllPlacedInstances())
            {
                if (existing.data != _instance.data || existing.gradeIndex != _instance.gradeIndex) continue;

                var savedTempSlot = _tempSlot;
                _isInTempSlot = false;
                _tempSlot     = null;
                savedTempSlot.OnItemPickedUp(this);
                existing.TryUpgrade();
                _gridUI.RefreshItemBlockVisual(existing);
                _gridUI.OnPlacementCancelled(_instance);
                Destroy(gameObject);
                return;
            }
        }

        // 2순위: 그리드 빈 공간에 자동 배치
        var origin = _gridUI.FindFirstValidPlacement(_instance);
        if (!origin.HasValue) return; // 공간 없음 → 임시칸 유지

        var slot = _tempSlot;
        _isInTempSlot = false;
        _tempSlot     = null;
        slot.OnItemPickedUp(this);
        if (_grid.TryPlace(_instance, origin.Value))
            SnapToGrid(origin.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // 스왑

    private void TryMultiSwap(System.Collections.Generic.List<ItemInstance> displaced, Vector2Int origin)
    {
        // 원위치 및 블록UI 저장
        var savedOrigins = new Vector2Int[displaced.Count];
        var savedBlocks  = new ItemBlockUI[displaced.Count];
        for (int i = 0; i < displaced.Count; i++)
        {
            _grid.TryGetOrigin(displaced[i], out savedOrigins[i]);
            savedBlocks[i] = _gridUI.FindItemBlock(displaced[i]);
        }

        // 전부 그리드에서 제거
        for (int i = 0; i < displaced.Count; i++)
        {
            _grid.Remove(displaced[i]);
            _gridUI.OnItemUnplaced(displaced[i]);
        }

        if (_grid.TryPlace(_instance, origin))
        {
            // 배치 성공: 첫 번째는 임시칸으로, 나머지는 마우스로
            var tempSlot  = _gridUI.TempSlot;
            bool tempUsed = false;
            for (int i = 0; i < displaced.Count; i++)
            {
                var blockUI = savedBlocks[i];
                if (blockUI == null) continue;
                if (!tempUsed && tempSlot != null
                    && !(blockUI.Instance.data is SO_InventoryBlockData))
                {
                    tempSlot.ReceiveBlock(blockUI);
                    tempUsed = true;
                }
                else
                {
                    blockUI.ResumeFollowing();
                }
            }
            SnapToGrid(origin);
        }
        else
        {
            // 배치 실패: 전부 원위치 복원
            for (int i = 0; i < displaced.Count; i++)
            {
                _grid.TryPlace(displaced[i], savedOrigins[i]);
                _gridUI.OnItemRestored(displaced[i], savedBlocks[i]);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 임시칸으로 보내기

    /// <summary>외부(상점 구매 등)에서 강제로 임시칸으로 보낼 때 호출</summary>
    public void ForceSendToTempSlot()
    {
        if (!_isFollowingMouse) return;
        SendToTempSlot();
    }

    private void SendToTempSlot()
    {
        if (_instance.data is SO_InventoryBlockData) return; // 임시칸 배치 불가

        var tempSlot = _gridUI.TempSlot;
        SetFollowing(false);
        if (tempSlot == null) return;
        tempSlot.ReceiveBlock(this);
        _gridUI.OnItemSentToTempSlot(this);
    }

    private bool IsMouseOverTempSlot()
    {
        var tempSlot = _gridUI.TempSlot;
        if (tempSlot == null) return false;

        var canvas = _gridUI.GetComponentInParent<Canvas>();
        var cam    = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;
        var rt = tempSlot.GetComponent<RectTransform>();
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Mouse.current.position.ReadValue(), cam);
    }

    private bool IsMouseOverSellSlot() =>
        SellSlotUI.Instance != null && SellSlotUI.Instance.IsMouseOver();

    private void SellAndDestroy()
    {
        SetFollowing(false);
        _gridUI.OnPlacementCancelled(_instance);
        SellSlotUI.Instance?.Sell(_instance);
        Destroy(gameObject);
    }

    /// <summary>스왑으로 밀려났을 때 마우스를 다시 따라다니게 한다</summary>
    public void ResumeFollowing()
    {
        _isPlaced     = false;
        _isInTempSlot = false;
        _tempSlot     = null;
        _placementInputGuard = true; // 이번 프레임 클릭 무시
        transform.SetParent(transform.root, false);
        transform.SetAsLastSibling();
        SetFollowing(true);
    }

    // ─────────────────────────────────────────────────────────────
    // 배치 완료 후 재집기 (배치 상태 + 임시칸 공통)

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isFollowingMouse) return;
        if (_gridUI.IsAnyFollowingMouse) return; // 다른 블록 드래그 중 → 클릭 무시

        // 우클릭: 임시칸 → 합성 우선, 빈 공간 자동 배치
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (_isInTempSlot) TrySmartPlaceFromTempSlot();
            return;
        }

        // T + 좌클릭: 즉시 판매
        if (eventData.button == PointerEventData.InputButton.Left
         && Keyboard.current != null && Keyboard.current.tKey.isPressed)
        {
            if (_isPlaced)
            {
                _grid.Remove(_instance);
                _gridUI.OnItemUnplaced(_instance);
            }
            else if (_isInTempSlot)
            {
                _tempSlot.OnItemPickedUp(this);
            }
            SellSlotUI.Instance?.Sell(_instance);
            _gridUI.OnPlacementCancelled(_instance);
            Destroy(gameObject);
            return;
        }

        if (_isPlaced)
        {
            _grid.TryGetOrigin(_instance, out _originCell);
            _originType     = OriginType.Grid;
            _originTempSlot = null;
            _grid.Remove(_instance);
            _gridUI.OnItemUnplaced(_instance);
            _isPlaced = false;
        }
        else if (_isInTempSlot)
        {
            _originType     = OriginType.TempSlot;
            _originTempSlot = _tempSlot;
            _originCell     = default;
            _tempSlot.OnItemPickedUp(this);
            _isInTempSlot = false;
            _tempSlot     = null;
        }
        else return;

        _placementInputGuard = false;
        transform.SetParent(transform.root, false);
        transform.SetAsLastSibling();
        _gridUI.ClearHighlight();
        SetFollowing(true);
    }

    // ─────────────────────────────────────────────────────────────
    // following 상태 변경 (GridUI 카운트 동기화)

    private void SetFollowing(bool value)
    {
        if (_isFollowingMouse == value) return;
        _isFollowingMouse = value;
        foreach (var outline in _cellOutlines)
            if (outline != null) outline.SetActive(value);
        if (value) _gridUI.OnBlockStartedFollowing(this);
        else       _gridUI.OnBlockStoppedFollowing(this);
    }

    // ─────────────────────────────────────────────────────────────
    // 임시칸 수신 콜백

    public void OnSentToTempSlot(TempSlotUI slot)
    {
        _isPlaced     = false;
        _isInTempSlot = true;
        _tempSlot     = slot;

        if (_instance != null && _instance.RingGradeBonus != 0)
        {
            _instance.RingGradeBonus = 0;
            _lastRingGradeBonus      = 0;
            RefreshVisuals();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 그리드에 스냅

    private void SnapToGrid(Vector2Int cell)
    {
        SetFollowing(false);
        _isPlaced = true;

        transform.SetParent(_gridUI.transform, false);
        _rt.anchoredPosition = _gridUI.CellToAnchoredPos(cell);

        _gridUI.OnPlacementSuccess(_instance, this);
    }

    /// <summary>
    /// 우클릭 스마트 구매용. Initialize 직후 호출 — 마우스 추적 없이 바로 그리드에 배치.
    /// </summary>
    public void SnapDirectly(Vector2Int cell) => SnapToGrid(cell);

    /// <summary>
    /// 자동 정렬 전용. 그리드/임시칸 상태 플래그를 모두 초기화한다.
    /// 이후 SnapDirectly 또는 TempSlot.ReceiveBlock 이 올바른 상태를 다시 설정한다.
    /// </summary>
    public void ResetForAutoSort()
    {
        _isPlaced     = false;
        _isInTempSlot = false;
        _tempSlot     = null;
    }

    private void OnDestroy()
    {
        // 예외적으로 파괴될 때 카운트 보정
        if (_isFollowingMouse && _gridUI != null)
            _gridUI.OnBlockStoppedFollowing(this);
    }

    // ─────────────────────────────────────────────────────────────
    // 비주얼

    public void RefreshVisuals()
    {
        foreach (Transform child in _rt)
            Destroy(child.gameObject);
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        _cellOutlines.Clear();

        var cells         = InventoryGrid.GetCells(_instance.data);
        bool isWeapon     = _instance.data is SO_WeaponData;
        bool hasSprite    = _instance.data.itemImage != null;
        bool useIconLayout = isWeapon || hasSprite;  // 무기 or 스프라이트가 있는 방어구

        // 희귀도 → 셀 보조선 색상
        int   rarityIdx  = Mathf.Clamp((int)_instance.data.rarity, 0, RarityColors.Length - 1);
        Color outlineCol = RarityColors[rarityIdx];
        outlineCol.a     = 0.6f;

        // 합성 등급 → 아이템 테두리 색상 (흰색 → 빨간색)
        int   effectiveGrade = Mathf.Clamp(_instance.gradeIndex + _instance.RingGradeBonus, 0, 4);
        Color borderCol      = _instance.HasGrades
            ? Color.Lerp(Color.white, Color.red, effectiveGrade / 4f)
            : Color.white;

        // ── 무기: 투명 배경 + 테두리 방식 ──
        if (useIconLayout)
        {
            int minRow = int.MaxValue, minCol = int.MaxValue;
            int maxRow = int.MinValue, maxCol = int.MinValue;
            foreach (var c in cells)
            {
                if (c.x < minRow) minRow = c.x; if (c.x > maxRow) maxRow = c.x;
                if (c.y < minCol) minCol = c.y; if (c.y > maxCol) maxCol = c.y;
            }

            float L = minCol * _cellSize, T = minRow * _cellSize;
            float R = (maxCol + 1) * _cellSize, B = (maxRow + 1) * _cellSize;
            float gap = _cellSize * 0.05f;   // 90% 스케일 — 각 방향 5% 여백
            L += gap; T += gap; R -= gap; B -= gap;
            float W = R - L, H = B - T;

            // 투명 히트박스 타일 (클릭 감지용) + 드래그 셀 외각선
            foreach (var cell in cells)
            {
                AddCellOutline(cell, outlineCol);
                var go = new GameObject("tile", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_rt, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(_cellSize - 2, _cellSize - 2);
                rt.anchoredPosition = new Vector2(cell.y * _cellSize + 1f, -cell.x * _cellSize - 1f);
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            }

            Vector2 center  = new Vector2((L + R) * 0.5f, -(T + B) * 0.5f);

            // 스프라이트 아이콘
            if (hasSprite)
            {
                // 8방향 오프셋 아웃라인 — 실루엣 외곽 투명 영역에 희귀도 색상 렌더
                const float outlineSize = 2f;
                Vector2[] dirs = {
                    new Vector2(-1,-1), new Vector2(0,-1), new Vector2(1,-1),
                    new Vector2(-1, 0),                    new Vector2(1, 0),
                    new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1),
                };
                var silMat = SilhouetteMat;
                foreach (var dir in dirs)
                {
                    var outGo = new GameObject("outline", typeof(RectTransform), typeof(Image));
                    outGo.transform.SetParent(_rt, false);
                    var outRt = outGo.GetComponent<RectTransform>();
                    outRt.anchorMin = outRt.anchorMax = new Vector2(0f, 1f);
                    outRt.pivot     = new Vector2(0.5f, 0.5f);
                    outRt.sizeDelta = new Vector2(W, H);
                    outRt.anchoredPosition = center + dir * outlineSize;
                    var outImg = outGo.GetComponent<Image>();
                    outImg.sprite         = _instance.data.itemImage;
                    outImg.preserveAspect = true;
                    outImg.color          = borderCol;
                    outImg.raycastTarget  = false;
                    if (silMat != null) outImg.material = silMat;
                }

                // 원본 스프라이트 (아웃라인 위에 렌더)
                var iconGo = new GameObject("icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(_rt, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
                iconRt.pivot     = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(W, H);
                iconRt.anchoredPosition = center;
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite         = _instance.data.itemImage;
                iconImg.preserveAspect = true;
                iconImg.color          = Color.white;
                iconImg.raycastTarget  = false;
            }
            else
            {
                // 스프라이트 없을 때 이름 레이블
                var labelGo = new GameObject("label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(_rt, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = labelRt.anchorMax = new Vector2(0f, 1f);
                labelRt.pivot     = new Vector2(0.5f, 0.5f);
                labelRt.sizeDelta = new Vector2(W, H);
                labelRt.anchoredPosition = center;
                var txt = labelGo.GetComponent<Text>();
                txt.font      = GetDefaultFont();
                txt.text      = _instance.data.itemName;
                txt.fontSize  = 9;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color     = Color.white;
                txt.raycastTarget = false;
            }
        }
        else
        {
            // ── 무기 외: 기존 색상 타일 방식 ──
            Color baseColor;
            if (_instance.data is SO_InventoryBlockData)
            {
                baseColor = new Color(0.25f, 0.80f, 0.35f);
            }
            else
            {
                float bright  = 1f + _instance.gradeIndex * 0.12f;
                baseColor = new Color(
                    Mathf.Clamp01(BgColors[rarityIdx].r * bright),
                    Mathf.Clamp01(BgColors[rarityIdx].g * bright),
                    Mathf.Clamp01(BgColors[rarityIdx].b * bright));
            }

            bool labelPlaced = false;
            foreach (var cell in cells)
            {
                AddCellOutline(cell, outlineCol);
                var go = new GameObject("tile", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_rt, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(_cellSize * 0.9f, _cellSize * 0.9f);
                rt.anchoredPosition = new Vector2(cell.y * _cellSize + _cellSize * 0.05f, -cell.x * _cellSize - _cellSize * 0.05f);
                go.GetComponent<Image>().color = baseColor;
                if (!labelPlaced)
                {
                    AddLabel(go, _instance.data.itemName);
                    labelPlaced = true;
                }
            }
        }

        // 등급 배지 (항상 최상단)
        if (_instance.HasGrades)
            AddGradeBadge(_rt.gameObject,
                          _instance.gradeIndex + _instance.RingGradeBonus + 1,
                          _instance.RingGradeBonus > 0,
                          RarityColors[rarityIdx]);
    }


    // 드래그 중 셀 형태를 나타내는 외각선 (4-strip 방식)
    private void AddCellOutline(Vector2Int cell, Color col)
    {
        const float thick = 1.5f;
        float s = _cellSize;

        var container = new GameObject("cell_outline");
        container.transform.SetParent(_rt, false);
        var cRt = container.AddComponent<RectTransform>();
        cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 1f);
        cRt.pivot     = new Vector2(0f, 1f);
        cRt.sizeDelta = new Vector2(s, s);
        cRt.anchoredPosition = new Vector2(cell.y * s, -cell.x * s);

        // 반투명 셀 배경 (아이템 뒤에서 블록 형태 강조)
        var bgGo = new GameObject("bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(container.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color         = new Color(0f, 0f, 0f, 0.3f);
        bgImg.raycastTarget = false;

        // 3px 외각선 / 내부 구분선 (4-strip)
        OutlineStrip(container, new Vector2(0,         0              ), new Vector2(s,     thick),          col);
        OutlineStrip(container, new Vector2(0,         -(s - thick)   ), new Vector2(s,     thick),          col);
        OutlineStrip(container, new Vector2(0,         -thick         ), new Vector2(thick, s - thick * 2f), col);
        OutlineStrip(container, new Vector2(s - thick, -thick         ), new Vector2(thick, s - thick * 2f), col);

        container.SetActive(_isFollowingMouse);
        _cellOutlines.Add(container);
    }

    private static void OutlineStrip(GameObject parent, Vector2 pos, Vector2 size, Color col)
    {
        var go = new GameObject("s", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color         = col;
        img.raycastTarget = false;
    }

    private static Font GetDefaultFont() =>
        Resources.Load<Font>("Fonts/Galmuri9")
        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

    private static void AddLabel(GameObject parent, string text)
    {
        var go = new GameObject("label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var txt = go.GetComponent<Text>();
        txt.font      = GetDefaultFont();
        txt.text      = text;
        txt.fontSize  = 9;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        txt.raycastTarget = false;
    }

    private static void AddGradeBadge(GameObject parent, int grade, bool isBuffed, Color rarityColor)
    {
        var go = new GameObject("grade", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(2f, -2f);
        rt.sizeDelta        = new Vector2(28f, 28f);

        var txt = go.GetComponent<Text>();
        txt.font      = GetDefaultFont();
        txt.text      = grade.ToString();
        txt.fontSize  = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.UpperLeft;
        // 반지 버프 적용 중이면 파란색, 아니면 희귀도 색상
        txt.color     = isBuffed ? new Color(0.35f, 0.75f, 1f) : rarityColor;
        txt.raycastTarget = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 아이템 정보 팝업

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스로 드래그 중이거나 데이터 없으면 표시 안 함
        if (_isFollowingMouse || _instance?.data == null) return;
        int effectiveGrade = Mathf.Clamp(
            _instance.gradeIndex + _instance.RingGradeBonus, 0, 4);
        ItemInfoPopup.Show(_instance.data, effectiveGrade, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemInfoPopup.Hide();
    }
}
