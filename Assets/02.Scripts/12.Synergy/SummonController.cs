using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BagSurvivor.Monster;

public class SummonController : MonoBehaviour
{
    private SO_SummonData _data;
    private Transform     _player;
    private PlayerHealth  _playerHealth;
    private int           _attackPower;
    private LayerMask     _enemyLayer;

    private float _atkTimer;
    private float _uniqueSkillTimer;
    private float _lifeTimer;   // data.duration > 0 인 소환수의 누적 생존 시간

    // ── FollowAttack FSM ─────────────────────────────────────────
    private enum SummonState { Wander, Chase }
    private SummonState  _state = SummonState.Wander;
    private MonsterController _chaseTarget;

    // 배회 전용
    private Vector2 _wanderDest;
    private float   _wanderTimer;
    private const float WanderRadius    = 5f;   // 플레이어 주변 배회 반경
    private const float WanderInterval  = 2.5f; // 새 웨이포인트 갱신 간격
    private const float DetectRange     = 5f;   // 적 감지 거리 (멀리 쫓아가지 않도록 축소)
    private const float LeashRange      = 9f;   // 이 거리 이상 벗어나면 플레이어로 복귀

    // OrbitPlayer 전용 (페어리) — 콘셉트(슬라이드 6~8): 타원 궤도 + 요정별 교차 궤도
    private float _orbitAngle;
    private float _orbitTilt;                  // 요정별 타원 궤도면 회전각(교차 궤도용)
    private const float OrbitRadiusX = 3.0f;   // 타원 가로 반경
    private const float OrbitRadiusY = 1.4f;   // 타원 세로 반경

    // Bounce 전용
    private Vector2 _bounceVel;
    private const float BounceContactRadius = 0.6f;

    // GuardOffset 전용 — 플레이어 기준 고정 위치 유지(+공전). 공전 속도는 data.moveSpeed(초당 도).
    // 마왕 기어: moveSpeed>0 → 공전 / 대정령: moveSpeed=0 → 머리 위 고정
    private float _guardAngle;                    // 현재 공전 각도(도)
    private const float GuardRadius = 2.2f;       // 플레이어로부터의 거리

    // 본체 스프라이트/평상 애니메이션 — 광역 발동 시 발동 모션으로 잠시 교체(대정령)
    private SpriteRenderer _mainSr;
    private Coroutine      _idleAnimCo;

    // 성능: Camera.main은 매 프레임 FindObjectWithTag를 호출하므로 Init에서 캐싱
    private Camera _mainCam;
    // 메모리: Init에서 생성한 임시 Texture2D를 OnDestroy에서 명시적으로 해제
    private Texture2D _runtimeTex;

    // ─────────────────────────────────────────────────────────────

    public void Init(SO_SummonData data, Transform player, int attackPower, LayerMask enemyLayer,
                     int siblingIndex = 0, int siblingCount = 1)
    {
        _data         = data;
        _player       = player;
        _playerHealth = player.GetComponent<PlayerHealth>();
        _attackPower  = attackPower;
        _enemyLayer   = enemyLayer;

        _atkTimer         = 0f;
        _uniqueSkillTimer = 0f;
        _lifeTimer        = 0f;
        _mainCam          = Camera.main;

        // 여러 요정 소환 시 균등 배치: 0°, 120°, 240° 등
        if (siblingCount > 1)
            _orbitAngle = 360f / siblingCount * siblingIndex;
        // 요정별 타원 궤도면 회전각 — 서로 교차하는 궤도를 만든다 (2마리→0°,90° / 3마리→0°,60°,120°)
        _orbitTilt = siblingCount > 1 ? 180f / siblingCount * siblingIndex : 0f;

        if (data.modelPrefab != null)
        {
            var model = Instantiate(data.modelPrefab, transform);
            model.transform.localPosition = Vector3.zero;
        }
        else if (GetComponentInChildren<SpriteRenderer>() == null)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = data.sortingOrder; // 성역 등 바닥 효과는 낮은 값으로 깔림

            if (data.icon != null)
            {
                sr.sprite = data.icon;
                // 표시 크기: displayScale 지정 시 우선, 아니면 AI 타입별 기본값
                float scale = data.displayScale > 0f ? data.displayScale : data.aiType switch
                {
                    SummonAIType.Bounce      => 0.25f, // 핀볼: 작은 공
                    SummonAIType.OrbitPlayer => 0.40f, // 페어리: 플레이어 주변 회전
                    SummonAIType.GuardOffset => 0.1125f, // 마왕 기어 (0.45 → 4배 축소)
                    _                        => 0.60f, // 골렘·성역: 일반 크기
                };
                transform.localScale = Vector3.one * scale;

                _mainSr = sr; // 광역 발동 시 본체 스프라이트를 교체하기 위해 보관

                // 프레임이 2개 이상이면 애니메이션 코루틴 시작
                if (data.animFrames != null && data.animFrames.Length > 1)
                    _idleAnimCo = StartCoroutine(PlaySpriteAnim(sr, data.animFrames, data.animFps));
            }
            else
            {
                // 전용 이미지 없을 때 흰색 원 (임시 placeholder)
                _runtimeTex = new Texture2D(32, 32);
                for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 16f, dy = y - 16f;
                    _runtimeTex.SetPixel(x, y, Mathf.Sqrt(dx * dx + dy * dy) < 14f ? Color.white : Color.clear);
                }
                _runtimeTex.Apply();
                sr.sprite = Sprite.Create(_runtimeTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
                sr.color  = Color.white;
            }
        }

        // 초기 배회 목적지 설정
        PickNewWanderDest();

        // Bounce 초기 방향 설정
        if (data.aiType == SummonAIType.Bounce)
            _bounceVel = Random.insideUnitCircle.normalized * data.moveSpeed;

        // GuardOffset(마왕 기어): 플레이어 주변을 균등 각도로 시작해 공전 (위쪽 12시부터)
        if (data.aiType == SummonAIType.GuardOffset)
        {
            _guardAngle = (siblingCount > 0 ? 360f / siblingCount * siblingIndex : 0f) + 90f;
            float rad = _guardAngle * Mathf.Deg2Rad;
            transform.position = (Vector2)player.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * GuardRadius;
        }
    }

    private void OnDestroy()
    {
        // Init에서 생성한 임시 Texture2D 명시적 해제 (메모리 누수 방지)
        if (_runtimeTex != null) Destroy(_runtimeTex);
    }

    private void Update()
    {
        if (_data == null || _player == null) return;

        // 지속시간 소환수: data.duration 경과 시 소멸 (성역 등). -1 이하면 영구 유지.
        if (_data.duration > 0f)
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _data.duration) { Destroy(gameObject); return; }
        }

        _atkTimer         += Time.deltaTime;
        _uniqueSkillTimer += Time.deltaTime;

        switch (_data.aiType)
        {
            case SummonAIType.FollowAttack: UpdateFollowAttack(); break;
            case SummonAIType.OrbitPlayer:  UpdateOrbitPlayer();  break;
            case SummonAIType.Stationary:   UpdateStationary();   break;
            case SummonAIType.Bounce:       UpdateBounce();       break;
            case SummonAIType.GuardOffset:  UpdateGuardOffset();  break;
        }

        if (_data.uniqueSkill != null && _uniqueSkillTimer >= _data.uniqueSkillCooldown)
        {
            _uniqueSkillTimer = 0f;
            StartCoroutine(ExecuteSkill(_data.uniqueSkill));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // FollowAttack FSM

    private void UpdateFollowAttack()
    {
        // 플레이어와 너무 멀면 즉시 복귀
        float distToPlayer = Vector2.Distance(transform.position, _player.position);
        if (distToPlayer > LeashRange)
        {
            _state = SummonState.Wander;
            PickNewWanderDest();
            MoveToward(_player.position);
            return;
        }

        switch (_state)
        {
            case SummonState.Wander:
                UpdateWander();
                break;
            case SummonState.Chase:
                UpdateChase();
                break;
        }
    }

    private void UpdateWander()
    {
        // 감지 범위 내 적 확인 → Chase 전환
        var detected = FindNearest(DetectRange);
        if (detected != null)
        {
            _chaseTarget = detected;
            _state = SummonState.Chase;
            return;
        }

        // 웨이포인트 타이머
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector2.Distance(transform.position, _wanderDest) < 0.4f)
            PickNewWanderDest();

        MoveToward(_wanderDest);
    }

    private void UpdateChase()
    {
        // 타겟 무효화 확인 — IsDead 외에 풀 반환(비활성화)도 처리
        if (_chaseTarget == null || _chaseTarget.IsDead || !_chaseTarget.gameObject.activeInHierarchy)
        {
            _chaseTarget = null;
            _state = SummonState.Wander;
            PickNewWanderDest();
            return;
        }

        // 감지 범위 이탈 시 배회 복귀
        float distToTarget = Vector2.Distance(transform.position, _chaseTarget.transform.position);
        if (distToTarget > DetectRange * 1.5f)
        {
            _chaseTarget = null;
            _state = SummonState.Wander;
            PickNewWanderDest();
            return;
        }

        // 공격 사거리 밖이면 접근
        if (distToTarget > _data.atkRange)
            MoveToward(_chaseTarget.transform.position);

        // 공격 (사거리 안에 들어왔을 때만 — 쫓아가는 도중 원거리 타격 방지)
        if (distToTarget <= _data.atkRange && _atkTimer >= _data.atkCooldown)
        {
            _atkTimer = 0f;
            Vector2 kb = ((Vector2)_chaseTarget.transform.position - (Vector2)transform.position).normalized;
            _chaseTarget.TakeDamage(_attackPower, 1f, kb);
            SpawnAttackEffect(_chaseTarget.transform.position);
        }
    }

    /// <summary>공격 명중 위치에 공격 이펙트(정령 골렘 spirit_attack 등)를 재생한다.</summary>
    private void SpawnAttackEffect(Vector3 pos)
    {
        var frames = _data.attackEffectFrames;
        if (frames == null || frames.Length == 0) return;

        var go = new GameObject("SummonAtkFx");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = SynergyLayers.Effect; // 공격 이펙트는 캐릭터 위
        sr.sprite = frames[0];
        if (frames[0] != null)
        {
            float target    = _data.attackEffectScale > 0f ? _data.attackEffectScale : 0.5f;
            float maxExtent  = Mathf.Max(frames[0].bounds.extents.x, frames[0].bounds.extents.y);
            go.transform.localScale = maxExtent > 0.001f ? Vector3.one * (target / maxExtent) : Vector3.one * target;
        }

        go.AddComponent<SpriteSheetAnimator>().Play(frames, _data.attackEffectFps, loop: false);
        float life = frames.Length / Mathf.Max(1f, _data.attackEffectFps) + 0.1f;
        Destroy(go, life);
    }

    private void PickNewWanderDest()
    {
        if (_player == null) return;
        _wanderTimer = WanderInterval;
        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(WanderRadius * 0.3f, WanderRadius);
        _wanderDest = (Vector2)_player.position + offset;
    }

    // ─────────────────────────────────────────────────────────────
    // OrbitPlayer

    private void UpdateOrbitPlayer()
    {
        _orbitAngle += _data.moveSpeed * Time.deltaTime;
        float rad = _orbitAngle * Mathf.Deg2Rad;

        // 타원 로컬 좌표(가로>세로) → 요정별 궤도면 회전 적용 → 플레이어 기준 배치
        Vector2 local = new Vector2(Mathf.Cos(rad) * OrbitRadiusX, Mathf.Sin(rad) * OrbitRadiusY);
        float t  = _orbitTilt * Mathf.Deg2Rad;
        float ct = Mathf.Cos(t), st = Mathf.Sin(t);
        Vector2 rot = new Vector2(local.x * ct - local.y * st, local.x * st + local.y * ct);
        transform.position = (Vector2)_player.position + rot;

        if (_atkTimer >= _data.atkCooldown)
        {
            var target = FindNearest(_data.atkRange);
            if (target != null)
            {
                _atkTimer = 0f;
                Vector2 kb = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                target.TakeDamage(_attackPower, 1f, kb);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Stationary

    private void UpdateStationary()
    {
        if (_atkTimer < _data.atkCooldown) return;
        _atkTimer = 0f;

        // 성기사단 성역: 플레이어 체력 회복 (HealArmorHpPct)
        if (_data.fixedEffect == FixedEffectType.HealArmorHpPct && _playerHealth != null)
        {
            int armorHp  = GameManager.Instance?.CurrentLoadout?.TotalArmorHp ?? 0;
            int healAmt  = Mathf.Max(1, Mathf.RoundToInt(armorHp * _data.fixedEffectValue / 100f));
            _playerHealth.Heal(healAmt);
        }

        // 광역 공격: 성역(atkRange) 안의 모든 적을 동시에 타격 (단일 대상이 아님)
        foreach (var mc in GetEnemiesInRange(_data.atkRange))
        {
            Vector2 kb = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
            mc.TakeDamage(_attackPower, 1f, kb);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Bounce (핀볼)

    private void UpdateBounce()
    {
        // 이동
        transform.position += (Vector3)(_bounceVel * Time.deltaTime);

        // 카메라 경계에서 반사 (Camera.main 대신 캐싱된 _mainCam 사용)
        var cam = _mainCam != null ? _mainCam : (_mainCam = Camera.main);
        if (cam != null)
        {
            float halfH  = cam.orthographicSize;
            float halfW  = halfH * cam.aspect;
            var   camPos = cam.transform.position;
            var   pos    = (Vector2)transform.position;

            if (pos.x < camPos.x - halfW || pos.x > camPos.x + halfW)
            {
                _bounceVel.x = -_bounceVel.x;
                transform.position = new Vector3(
                    Mathf.Clamp(pos.x, camPos.x - halfW, camPos.x + halfW),
                    pos.y, 0f);
            }
            if (pos.y < camPos.y - halfH || pos.y > camPos.y + halfH)
            {
                _bounceVel.y = -_bounceVel.y;
                transform.position = new Vector3(
                    transform.position.x,
                    Mathf.Clamp(pos.y, camPos.y - halfH, camPos.y + halfH), 0f);
            }
        }

        // 플레이어 리셀 (너무 멀어지면 복귀)
        if (Vector2.Distance(transform.position, _player.position) > LeashRange * 3f)
        {
            transform.position = _player.position;
            _bounceVel = Random.insideUnitCircle.normalized * _data.moveSpeed;
        }

        // 접촉 피해
        if (_atkTimer >= _data.atkCooldown)
        {
            var enemies = GetEnemiesInRange(BounceContactRadius);
            if (enemies.Count > 0)
            {
                _atkTimer = 0f;
                foreach (var mc in enemies)
                {
                    Vector2 kb = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
                    mc.TakeDamage(_attackPower, 2f, kb);
                }
                // 적 충돌 시에도 방향 반사 (첫 번째 적 기준)
                Vector2 toEnemy = ((Vector2)enemies[0].transform.position - (Vector2)transform.position).normalized;
                _bounceVel = Vector2.Reflect(_bounceVel, -toEnemy).normalized * _data.moveSpeed;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // GuardOffset (마왕 기어) — 플레이어 기준 고정 위치를 부드럽게 따라가며 원거리 공격

    private void UpdateGuardOffset()
    {
        // moveSpeed(초당 도)만큼 공전 — 0이면 시작 각도에 고정(대정령 머리 위). 이동하는 플레이어도 추종.
        _guardAngle += _data.moveSpeed * Time.deltaTime;
        float rad = _guardAngle * Mathf.Deg2Rad;
        transform.position = (Vector2)_player.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * GuardRadius;

        if (_atkTimer >= _data.atkCooldown)
        {
            var enemy = FindNearest(_data.atkRange);
            if (enemy != null)
            {
                _atkTimer = 0f;
                Vector2 kb = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
                enemy.TakeDamage(_attackPower, 1f, kb);
                SpawnAttackEffect(enemy.transform.position);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 유니크 스킬

    private IEnumerator ExecuteSkill(SO_SkillData skill)
    {
        int damage  = Mathf.RoundToInt(_attackPower * skill.dmgMultiplier);
        var enemies = GetEnemiesInRange(skill.rangeRadius);

        switch (skill.targetType)
        {
            case SkillTargetType.RandomEnemy:
                if (enemies.Count > 0)
                    ApplySkillHit(skill, enemies[Random.Range(0, enemies.Count)], damage);
                break;
            case SkillTargetType.AreaCenter:
            case SkillTargetType.Self:
                // 대정령 광역기: 화면 내 모든 적에게 대량 피해 (노드 이펙트 없음)
                foreach (var mc in enemies) ApplySkillHit(skill, mc, damage);
                break;
            case SkillTargetType.Forward:
                var nearest = FindNearest(skill.rangeRadius);
                if (nearest != null) ApplySkillHit(skill, nearest, damage);
                break;
        }

        if (skill.vfxPrefab != null)
        {
            var vfx = Instantiate(skill.vfxPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, skill.duration > 0f ? skill.duration : 2f);
        }
        else if (_mainSr != null && skill.animFrames != null && skill.animFrames.Length > 0)
        {
            // 광역 발동: 본체 스프라이트를 발동 모션으로 잠시 "대체"(같은 위치·크기·레이어, 평상 애니메이션은 잠시 사라짐)
            StartCoroutine(PlayCastMotion(skill.animFrames, skill.animFps));
        }

        yield return null;
    }

    /// <summary>평상 애니메이션을 멈추고 발동 모션을 1회 재생한 뒤 평상 애니메이션을 복구한다(대정령 광역).</summary>
    private IEnumerator PlayCastMotion(Sprite[] castFrames, float fps)
    {
        if (_mainSr == null || castFrames == null || castFrames.Length == 0) yield break;

        if (_idleAnimCo != null) StopCoroutine(_idleAnimCo); // 평상 애니메이션 정지(잠시 사라짐 = 대체)

        // 발동 모션 스프라이트의 원본 크기가 평상(대정령)과 달라도 같은 표시 크기가 되도록 스케일 보정
        Vector3 baseScale = transform.localScale;
        Sprite  idleRef   = (_data.animFrames != null && _data.animFrames.Length > 0) ? _data.animFrames[0] : _data.icon;
        float   idleDisplayed = idleRef != null
            ? Mathf.Max(idleRef.bounds.extents.x, idleRef.bounds.extents.y) * baseScale.x : 0f;
        float   castExtent    = Mathf.Max(castFrames[0].bounds.extents.x, castFrames[0].bounds.extents.y);
        if (castExtent > 0.001f && idleDisplayed > 0.0001f)
            transform.localScale = Vector3.one * (idleDisplayed / castExtent);

        var wait = new WaitForSeconds(1f / Mathf.Max(1f, fps));
        for (int i = 0; i < castFrames.Length; i++)
        {
            if (_mainSr == null) { transform.localScale = baseScale; yield break; }
            _mainSr.sprite = castFrames[i];
            yield return wait;
        }

        transform.localScale = baseScale; // 평상 크기 복구

        // 평상 애니메이션 복구
        if (_mainSr != null && _data.animFrames != null && _data.animFrames.Length > 1)
            _idleAnimCo = StartCoroutine(PlaySpriteAnim(_mainSr, _data.animFrames, _data.animFps));
        else if (_mainSr != null && _data.animFrames != null && _data.animFrames.Length == 1)
            _mainSr.sprite = _data.animFrames[0];
        else if (_mainSr != null && _data.icon != null)
            _mainSr.sprite = _data.icon;
    }

    private void ApplySkillHit(SO_SkillData skill, MonsterController mc, int damage)
    {
        Vector2 kb = ((Vector2)mc.transform.position - (Vector2)transform.position).normalized;
        float kbForce = skill.fixedEffect == FixedEffectType.Knockback ? skill.fixedEffectValue : 0f;
        mc.TakeDamage(damage, kbForce, kb);
    }

    // ─────────────────────────────────────────────────────────────
    // 스프라이트 애니메이션

    private IEnumerator PlaySpriteAnim(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        float interval = 1f / Mathf.Max(fps, 1f);
        var wait = new WaitForSeconds(interval);
        int idx = 0;
        while (true)
        {
            if (sr == null) yield break;
            sr.sprite = frames[idx];
            idx = (idx + 1) % frames.Length;
            yield return wait;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 유틸

    private void MoveToward(Vector2 dest)
    {
        float dist = Vector2.Distance(transform.position, dest);
        if (dist < 0.3f) return;
        transform.position = Vector2.MoveTowards(
            transform.position, dest, _data.moveSpeed * Time.deltaTime);
    }

    private MonsterController FindNearest(float range)
    {
        MonsterController best = null;
        float minDist = float.MaxValue;
        foreach (var mc in GetEnemiesInRange(range))
        {
            float d = Vector2.Distance(transform.position, mc.transform.position);
            if (d < minDist) { minDist = d; best = mc; }
        }
        return best;
    }

    private List<MonsterController> GetEnemiesInRange(float range)
    {
        var result = new List<MonsterController>();

        // 몬스터 콜라이더가 IsTrigger=true 이므로 ContactFilter2D.useTriggers 필수
        var filter = new ContactFilter2D();
        filter.useTriggers = true;
        if (_enemyLayer != 0) filter.SetLayerMask(_enemyLayer);
        else                  filter = ContactFilter2D.noFilter;

        var cols = new List<Collider2D>();
        Physics2D.OverlapCircle((Vector2)transform.position, range, filter, cols);

        foreach (var h in cols)
        {
            var mc = h.GetComponent<MonsterController>()
                  ?? h.GetComponentInParent<MonsterController>();
            if (mc != null && !mc.IsDead && mc.gameObject.activeInHierarchy) result.Add(mc);
        }
        return result;
    }
}
