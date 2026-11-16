using UnityEngine;

namespace ElementMage
{
    /// <summary>피해 숫자 표시. 동시에 뜨는 개수에 상한을 둬서 모바일 부담을 줄인다.</summary>
    public class DamagePopup : Pooled
    {
        static int activeCount;

        TextMesh text;
        Transform tr;
        float time;
        Color color;
        float baseScale = 1f;
        const float Life = 0.55f;

        public static void ResetStatics() => activeCount = 0;

        protected override void OnCreated()
        {
            tr = transform;
            text = gameObject.AddComponent<TextMesh>();
            Font font = UIFactory.Font;
            text.font = font;
            text.fontSize = 48;
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            var mr = GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.sortingOrder = Gfx.OrderPopup;
        }

        public static void Show(Vector2 pos, float amount, Element element)
        {
            GameConfig cfg = GameManager.Config;
            if (cfg == null || !cfg.showDamageNumbers) return;
            if (activeCount >= cfg.maxDamagePopups) return;
            if (amount < 1f) return;

            GameDatabase db = GameManager.Db;
            DamagePopup p = Pool<DamagePopup>.Get();
            activeCount++;
            p.tr.position = pos + new Vector2(Random.Range(-0.25f, 0.25f), 0.4f);
            p.text.text = Mathf.RoundToInt(amount).ToString();
            p.color = element == Element.Fire ? db.fireColor : element == Element.Water ? db.waterColor : Color.white;
            p.text.color = p.color;
            p.time = 0f;
            // 큰 피해일수록 숫자가 크다
            p.baseScale = Mathf.Clamp(0.8f + amount / 70f, 0.8f, 1.7f);
            p.tr.localScale = Vector3.one * (p.baseScale * 1.7f);
        }

        void Update()
        {
            time += Time.deltaTime;
            float t = time / Life;
            if (t >= 1f) { Release(); return; }
            tr.position += new Vector3(0f, 1.6f * Time.deltaTime, 0f);
            // 튀어나오듯 크게 떴다가 제 크기로 줄어든다
            float pop = Mathf.Clamp01(1f - t * 5f);
            tr.localScale = Vector3.one * (baseScale * (1f + 0.7f * pop * pop));
            text.color = Gfx.WithAlpha(color, 1f - t * t);
        }

        protected override void OnReleased()
        {
            activeCount = Mathf.Max(0, activeCount - 1);
        }
    }
}
