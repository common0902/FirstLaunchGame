using System.Collections.Generic;
using UnityEngine;

namespace ElementMage
{
    /// <summary>
    /// 범위 안의 적 검색. 씬에서 2D 콜라이더를 가진 것은 적뿐이므로 필터 없이 조회한다.
    /// 결과 리스트는 호출자가 재사용하는 것을 넘겨 GC 할당을 피한다.
    /// </summary>
    public static class EnemyQuery
    {
        static readonly List<Collider2D> hits = new List<Collider2D>(128);
        static ContactFilter2D filter = ContactFilter2D.noFilter;

        public static int Circle(Vector2 center, float radius, List<Enemy> results)
        {
            results.Clear();
            Physics2D.OverlapCircle(center, radius, filter, hits);
            return Collect(results);
        }

        public static int Box(Vector2 center, Vector2 size, float angleDeg, List<Enemy> results)
        {
            results.Clear();
            Physics2D.OverlapBox(center, size, angleDeg, filter, hits);
            return Collect(results);
        }

        /// <summary>원 안의 적 수만 센다</summary>
        public static int CountCircle(Vector2 center, float radius)
        {
            return Physics2D.OverlapCircle(center, radius, filter, hits);
        }

        static int Collect(List<Enemy> results)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                Enemy e = Enemy.FromCollider(hits[i]);
                if (e != null && !e.IsDead) results.Add(e);
            }
            return results.Count;
        }
    }

    /// <summary>스킬이 대상을 고를 때 쓰는 함수 모음</summary>
    public static class Targeting
    {
        static readonly List<Enemy> candidates = new List<Enemy>(128);

        public static Enemy Nearest(Vector2 from, float range)
        {
            Enemy best = null;
            float bestSqr = range * range;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                float d = (list[i].Position - from).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = list[i]; }
            }
            return best;
        }

        public static Enemy HighestHp(Vector2 from, float range)
        {
            Enemy best = null;
            float bestHp = -1f;
            float sqr = range * range;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if ((e.Position - from).sqrMagnitude > sqr) continue;
                if (e.Hp > bestHp) { bestHp = e.Hp; best = e; }
            }
            return best;
        }

        public static Enemy RandomInRange(Vector2 from, float range)
        {
            Enemy pick = null;
            int seen = 0;
            float sqr = range * range;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].Position - from).sqrMagnitude > sqr) continue;
                seen++;
                if (Random.Range(0, seen) == 0) pick = list[i];
            }
            return pick;
        }

        public static bool AnyInRange(Vector2 from, float range)
        {
            float sqr = range * range;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
                if ((list[i].Position - from).sqrMagnitude <= sqr) return true;
            return false;
        }

        /// <summary>
        /// 범위 안에서 적이 가장 몰린 지점을 찾는다. avoid에 있는 지점과 minSeparation보다 가까운 곳은 제외한다.
        /// 모든 적을 검사하지 않고 일부만 표본으로 뽑아 비용을 줄인다.
        /// </summary>
        public static bool DensePoint(Vector2 from, float range, float clusterRadius, out Vector2 point,
            List<Vector2> avoid = null, float minSeparation = 0f)
        {
            point = from;
            candidates.Clear();
            float sqr = range * range;
            List<Enemy> list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
                if ((list[i].Position - from).sqrMagnitude <= sqr) candidates.Add(list[i]);
            if (candidates.Count == 0) return false;

            const int Samples = 12;
            int bestCount = -1;
            bool found = false;
            float sepSqr = minSeparation * minSeparation;
            int tries = Mathf.Min(Samples, candidates.Count);
            for (int s = 0; s < tries; s++)
            {
                Vector2 p = candidates[Random.Range(0, candidates.Count)].Position;
                bool blocked = false;
                if (avoid != null)
                {
                    for (int a = 0; a < avoid.Count; a++)
                        if ((avoid[a] - p).sqrMagnitude < sepSqr) { blocked = true; break; }
                }
                if (blocked) continue;
                int count = EnemyQuery.CountCircle(p, clusterRadius);
                if (count > bestCount) { bestCount = count; point = p; found = true; }
            }
            return found;
        }
    }
}
