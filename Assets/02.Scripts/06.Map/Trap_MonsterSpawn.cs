using UnityEngine;

public class Trap_MonsterSpawn : MonoBehaviour, ITrapEffect
{
    public GameObject monsterPrefab;
    public int spawnCount = 5;
    
    [Tooltip("벽 밖으로 몬스터가 소환되지 않도록 소환 반경을 좁게 설정합니다.")]
    public float spawnRadius = 0.5f;

    public float ExecuteTrap(GameObject player)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 randomPos = (Vector2)transform.position + Random.insideUnitCircle * spawnRadius;
            if (monsterPrefab != null)
            {
                Instantiate(monsterPrefab, randomPos, Quaternion.identity);
            }
        }
        
        // 몬스터 소환은 순식간(즉발)에 끝나버리므로 대기할 필요가 없음. 0초 반환!
        return 0f; 
    }
}