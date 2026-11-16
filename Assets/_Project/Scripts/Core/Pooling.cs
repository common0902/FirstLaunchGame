using System;
using UnityEngine;
using UnityEngine.Pool;

namespace ElementMage
{
    /// <summary>
    /// 풀링되는 오브젝트의 기반 클래스. Pool&lt;T&gt;.Get()으로 꺼내고 Release()로 반납한다.
    /// </summary>
    public abstract class Pooled : MonoBehaviour
    {
        internal Action<Pooled> returnToPool;
        bool released;

        internal void MarkTaken() => released = false;
        internal void InvokeCreated() => OnCreated();

        public void Release()
        {
            if (released) return;
            released = true;
            OnReleased();
            returnToPool?.Invoke(this);
        }

        /// <summary>풀에서 처음 생성될 때 한 번 호출 (컴포넌트 구성용)</summary>
        protected virtual void OnCreated() { }
        protected virtual void OnReleased() { }
    }

    /// <summary>
    /// 타입별 오브젝트 풀. 오브젝트는 코드로 구성되므로 프리팹 없이 동작한다.
    /// 씬이 다시 로드되면 루트가 파괴되므로 자동으로 풀을 새로 만든다.
    /// </summary>
    public static class Pool<T> where T : Pooled
    {
        static ObjectPool<T> pool;
        static Transform root;

        public static T Get()
        {
            if (pool == null || root == null) Build();
            T item = pool.Get();
            item.MarkTaken();
            return item;
        }

        static void Build()
        {
            root = new GameObject("Pool_" + typeof(T).Name).transform;
            pool = new ObjectPool<T>(
                Create,
                item => item.gameObject.SetActive(true),
                item => item.gameObject.SetActive(false),
                item => { if (item != null) UnityEngine.Object.Destroy(item.gameObject); },
                false, 16, 2000);
        }

        static T Create()
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(root, false);
            T item = go.AddComponent<T>();
            ObjectPool<T> owner = pool;
            item.returnToPool = p => { if (p != null && owner == pool) owner.Release((T)p); };
            item.InvokeCreated();
            return item;
        }
    }
}
