using System;
using UnityEngine;
using UnityEngine.UI;

namespace ElementMage
{
    /// <summary>승리·패배 결과 화면</summary>
    public class ResultUI : MonoBehaviour
    {
        RectTransform root;
        Text title, stats;

        public static ResultUI Create(Transform canvas, GameDatabase db, Action onRestart)
        {
            Image back = UIFactory.Image("ResultUI", canvas, null, new Color(0.02f, 0.03f, 0.08f, 0.88f), false, true);
            UIFactory.Stretch(back.rectTransform);
            var ui = back.gameObject.AddComponent<ResultUI>();
            ui.root = back.rectTransform;

            ui.title = UIFactory.Label("Title", ui.root, "", 150, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(ui.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1000f, 220f));

            Image panel = UIFactory.Image("Panel", ui.root, db.roundedRect, new Color(0.11f, 0.13f, 0.2f, 0.97f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(820f, 440f));
            ui.stats = UIFactory.Label("Stats", panel.transform, "", 46, new Color(0.9f, 0.92f, 0.97f));
            ui.stats.lineSpacing = 1.35f;
            UIFactory.Stretch(ui.stats.rectTransform, 40f, 30f, 40f, 30f);

            Button button = UIFactory.Button("Restart", ui.root, db.roundedRect, new Color(0.95f, 0.6f, 0.2f, 1f), out Image bg);
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(560f, 150f));
            Text label = UIFactory.Label("Label", bg.transform, "로비로", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Stretch(label.rectTransform);
            button.onClick.AddListener(() => onRestart());

            ui.root.gameObject.SetActive(false);
            return ui;
        }

        public void Show(bool victory, float seconds, int level, int kills)
        {
            title.text = victory ? "승리!" : "패배";
            title.color = victory ? new Color(1f, 0.88f, 0.4f) : new Color(1f, 0.45f, 0.45f);
            int s = Mathf.FloorToInt(seconds);
            stats.text = "생존 시간  " + (s / 60).ToString("00") + ":" + (s % 60).ToString("00") +
                         "\n도달 레벨  Lv." + level +
                         "\n처치 수  " + kills;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }
    }
}
