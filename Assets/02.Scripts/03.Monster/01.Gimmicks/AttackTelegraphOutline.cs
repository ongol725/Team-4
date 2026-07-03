// ============================================================
// AttackTelegraphOutline.cs
// 몬스터 공격 준비(텔레그래프) 동안 스프라이트에 빨간 외곽 테두리를 표시.
//  - 원본 스프라이트를 상/하/좌/우로 살짝 오프셋한 4개 복제(빨강)를 본체 뒤에 렌더 → 외곽선 효과.
//  - Show(true)로 켜고, 매 프레임 본체의 현재 스프라이트/플립을 따라감(애니메이션 대응).
//  - 원거리 몬스터(RangedAttackGimmick)가 발사 직전 잠깐 켠다.
// ============================================================
using UnityEngine;

namespace BagSurvivor.Monster
{
    public class AttackTelegraphOutline : MonoBehaviour
    {
        [Tooltip("외곽선 색")]
        public Color outlineColor = new Color(1f, 0.15f, 0.15f, 0.9f);

        [Tooltip("외곽선 두께(월드 유닛). 스프라이트 크기에 맞춰 조정")]
        public float thickness = 0.08f;

        private SpriteRenderer _owner;
        private SpriteRenderer[] _outlines; // 4방향
        private static readonly Vector2[] Dirs =
        {
            new Vector2( 1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        };

        private void Awake()
        {
            _owner = GetComponentInChildren<SpriteRenderer>();
            BuildOutlines();
            SetActiveOutline(false);
        }

        private void BuildOutlines()
        {
            if (_owner == null) return;
            _outlines = new SpriteRenderer[Dirs.Length];
            for (int i = 0; i < Dirs.Length; i++)
            {
                var go = new GameObject("Outline_" + i);
                go.transform.SetParent(_owner.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = outlineColor;
                sr.sortingLayerID = _owner.sortingLayerID;
                sr.sortingOrder = _owner.sortingOrder - 1; // 본체 뒤
                _outlines[i] = sr;
            }
        }

        /// <summary>텔레그래프 외곽선 표시/숨김.</summary>
        public void Show(bool on)
        {
            SetActiveOutline(on);
            if (on) Sync();
        }

        private void SetActiveOutline(bool on)
        {
            if (_outlines == null) return;
            foreach (var o in _outlines) if (o != null) o.gameObject.SetActive(on);
        }

        private void LateUpdate()
        {
            if (_outlines == null || _outlines.Length == 0 || !_outlines[0].gameObject.activeSelf) return;
            Sync();
        }

        // 본체의 현재 스프라이트/플립을 4방향 복제에 반영(애니메이션 프레임 추적)
        private void Sync()
        {
            if (_owner == null) return;
            for (int i = 0; i < _outlines.Length; i++)
            {
                var sr = _outlines[i];
                if (sr == null) continue;
                sr.sprite = _owner.sprite;
                sr.flipX = _owner.flipX;
                sr.flipY = _owner.flipY;
                sr.color = outlineColor;
                sr.transform.localPosition = (Vector3)(Dirs[i] * thickness);
                sr.transform.localScale = Vector3.one;
            }
        }
    }
}
