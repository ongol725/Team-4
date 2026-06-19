using System.Collections;
using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// TestBattle 씬 전용 임시 몬스터 스포너.
/// 경과 시간에 따라 스폰 수와 한도가 최대 10배까지 증가한다 (3분에 도달).
/// </summary>
public class TestMonsterSpawner : MonoBehaviour
{
    [Header("스폰할 몬스터 프리팹")]
    [SerializeField] private GameObject[] _monsterPrefabs;

    [Header("스폰 설정 (기준값)")]
    [SerializeField] private float _startDelay      = 20f;  // 첫 스폰까지 대기 시간 (초)
    [SerializeField] private float _spawnInterval   = 3f;   // 기준 스폰 주기 (초)
    [SerializeField] private int   _baseMaxMonsters = 10;   // 초기 동시 최대 마리 수
    [SerializeField] private float _spawnRadius     = 8f;   // 플레이어로부터 스폰 반경
    [SerializeField] private float _minSpawnRadius  = 4f;   // 최소 스폰 거리

    [Header("난이도 스케일링")]
    [SerializeField] private float _scaleUpDuration = 180f; // 이 시간(초)에 최대 배율 도달 (3분)
    [SerializeField] private float _maxMultiplier   = 10f;  // 최대 배율

    private Transform _player;
    private int       _aliveCount;
    private float     _elapsed;      // startDelay 이후 경과 시간

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        StartCoroutine(SpawnLoop());
    }

    // 현재 배율: 0초=1배, scaleUpDuration(180초)=maxMultiplier(10배), 이후 유지
    private float CurrentMultiplier =>
        Mathf.Lerp(1f, _maxMultiplier, Mathf.Clamp01(_elapsed / _scaleUpDuration));

    // 현재 동시 최대 마리 수
    private int CurrentMaxMonsters =>
        Mathf.RoundToInt(_baseMaxMonsters * CurrentMultiplier);

    // 현재 스폰 간격: 배율이 오를수록 짧아짐 (최소 0.3초)
    private float CurrentInterval =>
        Mathf.Max(0.3f, _spawnInterval / CurrentMultiplier);

    // ─────────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(_startDelay);

        while (true)
        {
            _elapsed += CurrentInterval;

            if (_aliveCount < CurrentMaxMonsters)
                SpawnOne();

            yield return new WaitForSeconds(CurrentInterval);
        }
    }

    private void SpawnOne()
    {
        if (_monsterPrefabs == null || _monsterPrefabs.Length == 0) return;

        var prefab = _monsterPrefabs[Random.Range(0, _monsterPrefabs.Length)];
        var pos    = GetSpawnPosition();

        MonsterController mc = null;

        if (MonsterPool.Instance != null)
            mc = MonsterPool.Instance.Get(prefab, pos);
        else
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            mc = go.GetComponent<MonsterController>();
        }

        if (mc == null) return;

        _aliveCount++;
        mc.SetDeathCallback(OnMonsterDeath);
    }

    private void OnMonsterDeath(MonsterController mc)
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
    }

    private Vector3 GetSpawnPosition()
    {
        Vector2 center = _player != null ? (Vector2)_player.position : Vector2.zero;
        float   angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float   dist   = Random.Range(_minSpawnRadius, _spawnRadius);
        var     pos    = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
        return new Vector3(pos.x, pos.y, 0f);
    }
}
