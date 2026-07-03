// ============================================================
// SynergyBeam.cs
// "발사가 아닌 지속 빔" 스킬용 런타임 컴포넌트 (과부하 레이저 등).
//  - 플레이어 위치에 고정되어 바라보는 방향(PlayerAttack.FacingDirection)을 실시간 추적한다.
//  - 빔 스프라이트를 사정거리만큼 늘려 캐릭터 전방으로 뻗는다.
//  - 일정 간격(틱)마다 빔 경로(박스 판정)에 닿은 적에게 지속 데미지를 준다.
//  - 시너지 해제(Refresh) 시 SynergyManager 가 이 오브젝트를 Destroy 한다.
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;

public class SynergyBeam : MonoBehaviour
{
    private Transform     _player;
    private PlayerAttack  _playerAttack;
    private float         _length;        // 빔 사정거리(유닛)
    private float         _width;         // 빔 두께(유닛)
    private int           _damage;        // 틱당 데미지
    private float         _tickInterval;  // 데미지 주기(초)

    private Transform      _spriteTf;     // 늘어나는 빔 스프라이트 자식
    private SpriteRenderer _sr;
    private float          _tickTimer;
    private Vector2        _dir = Vector2.right;

    // 적 콜라이더 검색 필터 — 트리거 포함, 레이어 무시(SynergyManager.GetEnemiesInRange 와 동일 정책)
    private static readonly ContactFilter2D _filter =
        new ContactFilter2D { useTriggers = true, useLayerMask = false, useDepth = false };
    private readonly List<Collider2D>           _cols = new();
    private readonly HashSet<MonsterController>  _hitThisTick = new();

    public void Init(Transform player, PlayerAttack atk,
                     float length, float width, int damage, float tickInterval,
                     Sprite[] frames, float fps, int sortingOrder)
    {
        _player       = player;
        _playerAttack = atk;
        _length       = length > 0f ? length : 10f;
        _width        = width  > 0f ? width  : 1f;
        _damage       = Mathf.Max(1, damage);
        _tickInterval = tickInterval > 0f ? tickInterval : 0.2f;

        // 빔 스프라이트 자식 — 루트는 플레이어 방향으로 회전, 자식은 전방으로 길이만큼 뻗는다.
        var child = new GameObject("BeamSprite");
        _spriteTf = child.transform;
        _spriteTf.SetParent(transform, false);
        _sr = child.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = sortingOrder;
        if (frames != null && frames.Length > 0)
        {
            _sr.sprite = frames[0];
            child.AddComponent<SpriteSheetAnimator>().Play(frames, fps, loop: true);
        }
    }

    private void LateUpdate()
    {
        if (_player == null) return;

        // 1) 플레이어 위치 고정 + 바라보는 방향 추적
        transform.position = _player.position;
        Vector2 dir = _playerAttack != null ? _playerAttack.FacingDirection : Vector2.right;
        if (dir.sqrMagnitude < 0.0001f) dir = _dir; // 정지 중이면 직전 방향 유지
        _dir = dir.normalized;
        float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);

        // 2) 스프라이트를 길이/두께에 맞춰 늘림 (현재 프레임 bounds 기준 재계산 → 애니 중에도 크기 일정)
        if (_sr != null && _sr.sprite != null)
        {
            Vector3 size = _sr.sprite.bounds.size; // 로컬 원본 크기
            float sx = size.x > 0.001f ? _length / size.x : _length;
            float sy = size.y > 0.001f ? _width  / size.y : _width;
            _spriteTf.localScale    = new Vector3(sx, sy, 1f);
            _spriteTf.localPosition = new Vector3(_length * 0.5f, 0f, 0f); // 플레이어 앞쪽으로 뻗음
        }

        // 3) 데미지 틱
        _tickTimer += Time.deltaTime;
        while (_tickTimer >= _tickInterval)
        {
            _tickTimer -= _tickInterval;
            ApplyBeamDamage();
        }
    }

    private void ApplyBeamDamage()
    {
        Vector2 center = (Vector2)_player.position + _dir * (_length * 0.5f);
        float   ang    = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;

        _cols.Clear();
        _hitThisTick.Clear();
        Physics2D.OverlapBox(center, new Vector2(_length, _width), ang, _filter, _cols);
        foreach (var c in _cols)
        {
            var mc = c.GetComponent<MonsterController>() ?? c.GetComponentInParent<MonsterController>();
            if (mc == null || mc.IsDead || !mc.gameObject.activeInHierarchy) continue;
            if (!_hitThisTick.Add(mc)) continue; // 한 틱에 같은 적 중복 타격 방지
            mc.TakeDamage(_damage, 0f, Vector2.zero);
        }
    }
}
