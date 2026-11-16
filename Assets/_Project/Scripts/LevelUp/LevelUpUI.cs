using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElementMage
{
    /// <summary>
    /// 레벨업 카드 화면. 카드 컨테이너가 Vertical Layout Group이라 후보 수(1~4장)와 무관하게 자동 정렬된다.
    /// 게임이 일시정지(timeScale 0)된 동안 동작하므로 모든 연출은 unscaled time을 쓴다.
    /// </summary>
    public class LevelUpUI : MonoBehaviour
    {
        const float OpenLockTime = 0.3f;     // 조이스틱을 누르던 손가락이 카드를 잘못 누르지 않게 하는 짧은 잠금
        const float HiddenDelay = 0.45f;     // 3장이 먼저 보인 뒤 히든 카드가 나오기까지의 시간

        GameDatabase db;
        RectTransform root, container;
        CanvasGroup group;
        Text title, subtitle;
        readonly List<GameObject> cards = new List<GameObject>(4);
        List<LevelUpOption> options;
        Action<LevelUpOption> onPicked;

        float lockTimer;
        RectTransform hiddenCard;
        float hiddenDelayTimer, hiddenRevealTimer, hiddenRevealTime;

        public bool IsOpen => root != null && root.gameObject.activeSelf;
        /// <summary>입력이 잠겨 있는 동안은 카드를 고를 수 없다</summary>
        public bool InputLocked => lockTimer > 0f || hiddenDelayTimer > 0f || hiddenRevealTimer > 0f;
        public IReadOnlyList<LevelUpOption> Options => options;

        public static LevelUpUI Create(Transform canvas, GameDatabase database)
        {
            Image dim = UIFactory.Image("LevelUpUI", canvas, null, new Color(0.02f, 0.03f, 0.08f, 0.82f), false, true);
            UIFactory.Stretch(dim.rectTransform);
            var ui = dim.gameObject.AddComponent<LevelUpUI>();
            ui.db = database;
            ui.root = dim.rectTransform;
            ui.group = dim.gameObject.AddComponent<CanvasGroup>();

            ui.title = UIFactory.Label("Title", ui.root, "레벨 업!", 84, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(ui.title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 120f));
            ui.subtitle = UIFactory.Label("Subtitle", ui.root, "", 38, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleCenter);
            UIFactory.Place(ui.subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(1000f, 60f));

            // 카드 컨테이너: 후보가 4장이어도 들어가는 크기
            ui.container = UIFactory.Rect("Cards", ui.root);
            UIFactory.Stretch(ui.container, 50f, 350f, 50f, 110f);
            var layout = ui.container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 26f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ui.root.gameObject.SetActive(false);
            return ui;
        }

        public void Show(string titleText, string subtitleText, List<LevelUpOption> list, Action<LevelUpOption> picked)
        {
            options = list;
            onPicked = picked;
            title.text = titleText;
            subtitle.text = subtitleText;

            for (int i = 0; i < cards.Count; i++) Destroy(cards[i]);
            cards.Clear();
            hiddenCard = null;
            hiddenDelayTimer = 0f;
            hiddenRevealTimer = 0f;

            for (int i = 0; i < list.Count; i++)
            {
                RectTransform card = BuildCard(list[i]);
                cards.Add(card.gameObject);
                if (list[i].isHidden)
                {
                    // 히든 카드는 숨겨 두었다가 연출과 함께 등장시킨다
                    hiddenCard = card;
                    card.gameObject.SetActive(false);
                    hiddenDelayTimer = HiddenDelay;
                    hiddenRevealTime = Mathf.Max(0.05f, db.config.hiddenCardRevealTime);
                }
            }

            lockTimer = OpenLockTime;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            ApplyLock();
        }

        public void Hide()
        {
            root.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (lockTimer > 0f) lockTimer -= dt;

            if (hiddenCard != null)
            {
                if (hiddenDelayTimer > 0f)
                {
                    hiddenDelayTimer -= dt;
                    if (hiddenDelayTimer <= 0f)
                    {
                        hiddenCard.gameObject.SetActive(true);
                        hiddenCard.localScale = new Vector3(0f, 0f, 1f);
                        hiddenRevealTimer = hiddenRevealTime;
                    }
                }
                else if (hiddenRevealTimer > 0f)
                {
                    // 슈르륵: 옆에서 밀려 들어오며 커졌다가 제자리로
                    hiddenRevealTimer -= dt;
                    float t = 1f - Mathf.Clamp01(hiddenRevealTimer / hiddenRevealTime);
                    float back = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);   // easeOutBack
                    hiddenCard.localScale = new Vector3(Mathf.Lerp(0.2f, 1f, t) * back, back, 1f);
                    hiddenCard.localRotation = Quaternion.Euler(0f, 0f, (1f - t) * -8f);
                    if (hiddenRevealTimer <= 0f)
                    {
                        hiddenCard.localScale = Vector3.one;
                        hiddenCard.localRotation = Quaternion.identity;
                    }
                }
            }
            ApplyLock();
        }

        void ApplyLock()
        {
            bool locked = InputLocked;
            group.interactable = !locked;
        }

        void Pick(LevelUpOption option)
        {
            if (InputLocked || !IsOpen) return;
            Hide();
            onPicked?.Invoke(option);
        }

        /// <summary>자동 플레이 테스트용: 입력 잠금 규칙은 그대로 따른다</summary>
        public bool DebugPick(int index)
        {
            if (!IsOpen || InputLocked || options == null || index < 0 || index >= options.Count) return false;
            Pick(options[index]);
            return true;
        }

        RectTransform BuildCard(LevelUpOption option)
        {
            Color cardColor = option.isHidden ? new Color(0.28f, 0.2f, 0.06f, 0.98f) : new Color(0.11f, 0.13f, 0.2f, 0.97f);
            Button button = UIFactory.Button("Card", container, db.roundedRect, cardColor, out Image bg);
            RectTransform card = bg.rectTransform;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 310f;
            element.minHeight = 250f;
            element.flexibleHeight = 0f;

            Image outline = UIFactory.Image("Outline", card, db.roundedOutline, option.accent, true);
            UIFactory.Stretch(outline.rectTransform);

            // 아이콘
            Image icon = UIFactory.Image("Icon", card, option.icon, Color.white);
            icon.preserveAspect = true;
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(190f, 190f));

            // 텍스트
            Text tag = UIFactory.Label("Tag", card, option.tag, 30, Color.Lerp(option.accent, Color.white, 0.25f), TextAnchor.UpperLeft);
            tag.rectTransform.anchorMin = new Vector2(0f, 1f);
            tag.rectTransform.anchorMax = new Vector2(1f, 1f);
            tag.rectTransform.pivot = new Vector2(0f, 1f);
            tag.rectTransform.offsetMin = new Vector2(265f, -68f);
            tag.rectTransform.offsetMax = new Vector2(-250f, -26f);

            Text level = UIFactory.Label("Level", card, option.levelText, 34, option.accent, TextAnchor.UpperRight, FontStyle.Bold);
            level.rectTransform.anchorMin = new Vector2(1f, 1f);
            level.rectTransform.anchorMax = new Vector2(1f, 1f);
            level.rectTransform.pivot = new Vector2(1f, 1f);
            level.rectTransform.anchoredPosition = new Vector2(-36f, -26f);
            level.rectTransform.sizeDelta = new Vector2(330f, 46f);

            Text name = UIFactory.Label("Name", card, option.title, 52, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.offsetMin = new Vector2(265f, -140f);
            name.rectTransform.offsetMax = new Vector2(-36f, -70f);

            Text desc = UIFactory.Label("Description", card, option.description, 33, new Color(0.84f, 0.87f, 0.94f), TextAnchor.UpperLeft);
            desc.lineSpacing = 1.1f;
            desc.rectTransform.anchorMin = new Vector2(0f, 0f);
            desc.rectTransform.anchorMax = new Vector2(1f, 1f);
            desc.rectTransform.offsetMin = new Vector2(265f, 22f);
            desc.rectTransform.offsetMax = new Vector2(-36f, -150f);
            desc.resizeTextForBestFit = true;
            desc.resizeTextMinSize = 22;
            desc.resizeTextMaxSize = 33;

            LevelUpOption captured = option;
            button.onClick.AddListener(() => Pick(captured));
            return card;
        }
    }
}
