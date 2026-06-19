// ============================================================
// SpawnFreezeGimmick.cs
// 스폰 직후 잠깐 정지했다가 다시 움직이는 기믹
//  - 분열 슬라임 등: 나오자마자 추적하지 않고 freezeTime 동안 멈춤(분열 연출) 후 추적 재개
//  - MonsterController에 컴포넌트로 부착 (컴포지션 패턴), 풀 재사용 대응
// ============================================================
using UnityEngine;
using System.Collections;

namespace BagSurvivor.Monster
{
    [RequireComponent(typeof(MonsterController))]
    public class SpawnFreezeGimmick : MonoBehaviour
    {
        [Tooltip("스폰 후 정지 시간(초)")]
        public float freezeTime = 0.5f;

        private MonsterController controller;

        private void Awake()
        {
            controller = GetComponent<MonsterController>();
        }

        private void OnEnable()
        {
            StartCoroutine(FreezeRoutine());
        }

        private IEnumerator FreezeRoutine()
        {
            // MonsterController.OnEnable(InitializeMonster가 isMovementPaused를 풂)의 실행 순서에
            // 상관없이 확실히 멈추도록, 한 프레임 뒤에 다시 한 번 정지시킨다.
            controller.PauseMovement();
            yield return null;
            if (controller == null || controller.IsDead) yield break;
            controller.PauseMovement();

            yield return new WaitForSeconds(freezeTime);

            if (controller != null && !controller.IsDead)
                controller.ResumeMovement();
        }
    }
}
