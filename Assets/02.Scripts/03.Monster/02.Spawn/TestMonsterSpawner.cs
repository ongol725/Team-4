using System.Collections;
using UnityEngine;
using BagSurvivor.Monster;

/// <summary>
/// TestBattle 씬 전용 임시 몬스터 스포너.
/// 플레이어 주변에 일정 간격으로 몬스터를 스폰한다.
/// MonsterPool이 있으면 풀링, 없으면 Instantiate 사용.
/// </summary>
public class TestMonsterSpawner : MonoBehaviour
{
    [Header("스폰할 몬스터 프리팹")]
    [SerializeField] private GameObject[] _monsterPrefabs;

    [Header("스폰 설정")]
    [SerializeField] private float _startDelay     = 20f;  // 첫 스폰까지 대기 시간 (초)
    [SerializeField] private float _spawnInterval  = 3f;   // 스폰 주기 (초)
    [SerializeField] private int   _maxMonsters    = 10;   // 동시 최대 마리 수
    [SerializeField] private float _spawnRadius    = 8f;   // 플레이어로부터 스폰 반경
    [SerializeField] private float _minSpawnRadius = 4f;   // 최소 스폰 거리

    private Transform _player;
    private int       _aliveCount;

    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(_startDelay);
        var wait = new WaitForSeconds(_spawnInterval);
        while (true)
        {
            if (_aliveCount < _maxMonsters)
                SpawnOne();
            yield return wait;
        }
    }

    private void SpawnOne()
    {
        if (_monsterPrefabs == null || _monsterPrefabs.Length == 0) return;

        var prefab = _monsterPrefabs[Random.Range(0, _monsterPrefabs.Length)];
        var pos    = GetSpawnPosition();

        MonsterController mc = null;

        if (MonsterPool.Instance != null)
        {
            mc = MonsterPool.Instance.Get(prefab, pos);
        }
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

        for (int i = 0; i < 20; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist  = Random.Range(_minSpawnRadius, _spawnRadius);
            var   pos   = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            return new Vector3(pos.x, pos.y, 0f);
        }
        return new Vector3(center.x + _spawnRadius, center.y, 0f);
    }
}
