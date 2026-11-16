using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElementMage
{
    /// <summary>
    /// 로비 화면. 장비 / 전투 / 도감 세 페이지를 좌우로 밀어 넘기고, 하단 탭으로도 이동한다.
    /// 전투 페이지에서 스테이지와 원소 슬롯 2개를 고르고 시작한다.
    /// 게임이 멈춘 상태(timeScale 0)에서 동작하므로 연출은 unscaled time을 쓴다.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        const int PageEquip = 0, PageBattle = 1, PageCodex = 2, PageCount = 3;
        static readonly string[] TabNames = { "장비", "전투", "도감" };
        static readonly Color PanelColor = new Color(0.11f, 0.13f, 0.21f, 1f);
        static readonly Color PanelLight = new Color(0.17f, 0.2f, 0.31f, 1f);
        static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        static readonly Color TextDim = new Color(0.72f, 0.77f, 0.88f);

        GameManager gm;
        GameDatabase db;
        RectTransform root;
        LobbyPager pager;
        Action<Element, Element, int> onStart;

        // 탭
        readonly RectTransform[] tabRects = new RectTransform[PageCount];
        readonly Image[] tabBacks = new Image[PageCount];
        readonly Text[] tabLabels = new Text[PageCount];

        // 전투 페이지
        int stageIndex;
        Element slotA, slotB;
        Text stageNumber, stageName, stageDesc, comboText, startLabel;
        Image stagePreview, slotAIcon, slotBIcon, startBack;
        GameObject stageLock;
        Button prevButton, nextButton;

        // 장비 페이지
        readonly Dictionary<EquipSlot, Image> slotIcons = new Dictionary<EquipSlot, Image>();
        readonly Dictionary<EquipSlot, Image> slotFrames = new Dictionary<EquipSlot, Image>();
        readonly List<GameObject> inventoryTiles = new List<GameObject>();
        RectTransform inventoryGrid;
        Text bonusText, equipInfo, equipComboText;
        Image equipSlotAIcon, equipSlotBIcon;

        // 도감 페이지
        Text codexInfo;

        public static LobbyUI Create(Transform canvas, GameManager manager, Action<Element, Element, int> startRun)
        {
            Image back = UIFactory.Image("LobbyUI", canvas, null, new Color(0.045f, 0.06f, 0.11f, 1f), false, true);
            UIFactory.Stretch(back.rectTransform);
            var ui = back.gameObject.AddComponent<LobbyUI>();
            ui.gm = manager;
            ui.db = manager.Database;
            ui.root = back.rectTransform;
            ui.onStart = startRun;
            ui.Build();
            return ui;
        }

        public void Hide() => root.gameObject.SetActive(false);

        /// <summary>자동 플레이 테스트용: 해당 페이지를 즉시 보여 준다</summary>
        public void DebugShowPage(int page) => pager.GoTo(page, true);

        // ------------------------------------------------------------------ 구성

        void Build()
        {
            RectTransform safe = UIFactory.SafeArea(root);

            // 상단 제목
            Text title = UIFactory.Label("Title", safe, "원소 마법사", 64, new Color(1f, 0.93f, 0.7f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 100f));

            // 페이지 영역 (가로 스크롤 + 스냅)
            RectTransform view = UIFactory.Rect("Pages", safe);
            UIFactory.Stretch(view, 0f, 140f, 0f, 220f);
            view.gameObject.AddComponent<RectMask2D>();
            // 빈 곳을 잡아도 밀 수 있도록 투명한 입력 영역을 둔다
            Image hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            RectTransform content = UIFactory.Rect("Content", view);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(PageCount, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.inertia = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 0f;

            pager = view.gameObject.AddComponent<LobbyPager>();
            pager.Init(scroll, PageCount, PageBattle);

            BuildEquipPage(MakePage(content, PageEquip));
            BuildBattlePage(MakePage(content, PageBattle));
            BuildCodexPage(MakePage(content, PageCodex));
            BuildTabs(safe);

            stageIndex = Mathf.Clamp(SaveData.SelectedStage, 0, Mathf.Max(0, db.stages.Count - 1));
            slotA = SaveData.SlotA == Element.Water ? Element.Water : Element.Fire;
            slotB = SaveData.SlotB == Element.Fire ? Element.Fire : Element.Water;
            RefreshBattle();
            RefreshEquipment();
        }

        static RectTransform MakePage(RectTransform content, int index)
        {
            RectTransform page = UIFactory.Rect("Page" + index, content);
            page.anchorMin = new Vector2(index / (float)PageCount, 0f);
            page.anchorMax = new Vector2((index + 1) / (float)PageCount, 1f);
            page.offsetMin = Vector2.zero;
            page.offsetMax = Vector2.zero;
            return page;
        }

        void BuildTabs(RectTransform parent)
        {
            Image bar = UIFactory.Image("TabBar", parent, null, new Color(0.07f, 0.085f, 0.15f, 1f), false, true);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = new Vector2(0f, 200f);

            Sprite[] icons = { db.tabEquipIcon, db.tabBattleIcon, db.tabCodexIcon };
            for (int i = 0; i < PageCount; i++)
            {
                Button button = UIFactory.Button("Tab" + i, bar.transform, db.roundedRect, PanelColor, out Image bg);
                RectTransform rt = bg.rectTransform;
                rt.anchorMin = new Vector2(i / (float)PageCount, 0f);
                rt.anchorMax = new Vector2((i + 1) / (float)PageCount, 1f);
                rt.offsetMin = new Vector2(10f, 14f);
                rt.offsetMax = new Vector2(-10f, -14f);
                tabRects[i] = rt;
                tabBacks[i] = bg;

                Image icon = UIFactory.Image("Icon", rt, icons[i], Color.white);
                icon.preserveAspect = true;
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(92f, 92f));
                tabLabels[i] = UIFactory.Label("Label", rt, TabNames[i], 38, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                UIFactory.Stretch(tabLabels[i].rectTransform, 0f, 0f, 0f, 12f);

                int page = i;
                button.onClick.AddListener(() => pager.GoTo(page, false));
            }
        }

        void Update()
        {
            // 현재 페이지의 탭을 키우고 밝게 (탕탕특공대식 강조)
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < PageCount; i++)
            {
                bool on = i == pager.Current;
                float scale = Mathf.Lerp(tabRects[i].localScale.x, on ? 1.08f : 0.94f, 1f - Mathf.Exp(-16f * dt));
                tabRects[i].localScale = new Vector3(scale, scale, 1f);
                tabBacks[i].color = on ? new Color(0.95f, 0.6f, 0.2f, 1f) : PanelColor;
                tabLabels[i].color = on ? Color.white : TextDim;
            }
        }

        // ------------------------------------------------------------------ 전투 페이지

        void BuildBattlePage(RectTransform page)
        {
            // 스테이지 카드
            Image card = UIFactory.Image("StageCard", page, db.roundedRect, PanelColor, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(860f, 700f));
            Image cardOutline = UIFactory.Image("Outline", card.transform, db.roundedOutline, new Color(0.45f, 0.55f, 0.8f), true);
            UIFactory.Stretch(cardOutline.rectTransform);

            stageNumber = UIFactory.Label("Number", card.transform, "", 36, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(stageNumber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(700f, 50f));
            stageName = UIFactory.Label("Name", card.transform, "", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Place(stageName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(700f, 90f));

            // 미리보기: 바닥 타일 위에 적 몇 마리
            RectTransform previewRoot = UIFactory.Rect("Preview", card.transform);
            UIFactory.Place(previewRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(660f, 340f));
            previewRoot.gameObject.AddComponent<RectMask2D>();
            stagePreview = UIFactory.Image("Ground", previewRoot, db.groundTile, Color.white);
            UIFactory.Place(stagePreview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 660f));
            PlaceSprite(previewRoot, db.boss != null ? db.boss.sprite : null, new Vector2(150f, 10f), 230f);
            if (db.spawnTable.Count > 0) PlaceSprite(previewRoot, db.spawnTable[0].enemy.sprite, new Vector2(-190f, -70f), 120f);
            if (db.spawnTable.Count > 1) PlaceSprite(previewRoot, db.spawnTable[1].enemy.sprite, new Vector2(-70f, 80f), 120f);
            if (db.spawnTable.Count > 2) PlaceSprite(previewRoot, db.spawnTable[2].enemy.sprite, new Vector2(-30f, -90f), 140f);
            Image frame = UIFactory.Image("Frame", previewRoot, db.roundedOutline, new Color(0.03f, 0.04f, 0.08f, 1f), true);
            UIFactory.Stretch(frame.rectTransform, -4f, -4f, -4f, -4f);

            stageDesc = UIFactory.Label("Desc", card.transform, "", 34, TextDim);
            stageDesc.lineSpacing = 1.15f;
            UIFactory.Place(stageDesc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(760f, 120f));

            // 잠금 표시
            Image lockDim = UIFactory.Image("Lock", previewRoot, null, new Color(0.02f, 0.03f, 0.07f, 0.78f));
            UIFactory.Stretch(lockDim.rectTransform);
            stageLock = lockDim.gameObject;
            Image lockIcon = UIFactory.Image("Icon", lockDim.transform, db.lockIcon, Color.white);
            UIFactory.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(120f, 120f));
            Text lockText = UIFactory.Label("Text", lockDim.transform, "이전 스테이지를 클리어하면 열립니다", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            UIFactory.Place(lockText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(640f, 50f));

            // 좌우 화살표
            prevButton = MakeArrow(card.transform, "<", new Vector2(0f, 0.5f), new Vector2(54f, -20f), () => ChangeStage(-1));
            nextButton = MakeArrow(card.transform, ">", new Vector2(1f, 0.5f), new Vector2(-54f, -20f), () => ChangeStage(1));

            // 원소 슬롯
            // 원소는 장비 탭에서 고른다. 여기서는 현재 조합만 보여 주고, 누르면 장비 탭으로 넘어간다.
            Text slotTitle = UIFactory.Label("SlotTitle", page, "장착 원소 (장비 탭에서 변경)", 34, TextDim);
            UIFactory.Place(slotTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -745f), new Vector2(800f, 50f));
            slotAIcon = MakeElementSlot(page, new Vector2(0.5f, 1f), new Vector2(-130f, -800f), 190f, () => pager.GoTo(PageEquip, false));
            slotBIcon = MakeElementSlot(page, new Vector2(0.5f, 1f), new Vector2(130f, -800f), 190f, () => pager.GoTo(PageEquip, false));
            Text plus = UIFactory.Label("Plus", page, "+", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(plus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -860f), new Vector2(80f, 80f));
            comboText = UIFactory.Label("Combo", page, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(comboText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1010f), new Vector2(980f, 100f));

            // 시작 버튼
            Button start = UIFactory.Button("Start", page, db.roundedRect, new Color(0.95f, 0.6f, 0.2f, 1f), out startBack);
            UIFactory.Place(startBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(620f, 170f));
            startLabel = UIFactory.Label("Label", startBack.transform, "전투 시작", 66, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UIFactory.Stretch(startLabel.rectTransform);
            start.onClick.AddListener(StartRun);
        }

        static void PlaceSprite(Transform parent, Sprite sprite, Vector2 pos, float size)
        {
            if (sprite == null) return;
            Image img = UIFactory.Image("Enemy", parent, sprite, Color.white);
            img.preserveAspect = true;
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        }

        Button MakeArrow(Transform parent, string glyph, Vector2 anchor, Vector2 pos, Action onClick)
        {
            Button button = UIFactory.Button("Arrow", parent, db.roundedRect, PanelLight, out Image bg);
            UIFactory.Place(bg.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(84f, 130f));
            Text label = UIFactory.Label("Glyph", bg.transform, glyph, 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(label.rectTransform);
            button.onClick.AddListener(() => onClick());
            return button;
        }

        Image MakeElementSlot(Transform parent, Vector2 anchor, Vector2 pos, float size, Action onClick)
        {
            Button button = UIFactory.Button("ElementSlot", parent, db.roundedRect, PanelLight, out Image bg);
            UIFactory.Place(bg.rectTransform, anchor, anchor, pos, new Vector2(size, size));
            Image frame = UIFactory.Image("Frame", bg.transform, db.roundedOutline, Gold, true);
            UIFactory.Stretch(frame.rectTransform);
            Image icon = UIFactory.Image("Icon", bg.transform, null, Color.white);
            icon.preserveAspect = true;
            float pad = size * 0.12f;
            UIFactory.Stretch(icon.rectTransform, pad, pad, pad, pad);
            button.onClick.AddListener(() => onClick());
            return icon;
        }

        static Element Toggle(Element e) => e == Element.Fire ? Element.Water : Element.Fire;

        /// <summary>장비 탭에서 원소 슬롯을 눌렀을 때: 불/물을 바꾸고 바로 저장한다</summary>
        void ToggleElementSlot(bool first)
        {
            if (first) slotA = Toggle(slotA);
            else slotB = Toggle(slotB);
            SaveData.SlotA = slotA;
            SaveData.SlotB = slotB;
            RefreshBattle();
        }

        void ChangeStage(int delta)
        {
            if (db.stages.Count == 0) return;
            stageIndex = Mathf.Clamp(stageIndex + delta, 0, db.stages.Count - 1);
            RefreshBattle();
        }

        bool StageUnlocked => stageIndex <= SaveData.UnlockedStage;

        void RefreshBattle()
        {
            if (db.stages.Count > 0)
            {
                StageData stage = db.stages[stageIndex];
                stageNumber.text = "스테이지 " + (stageIndex + 1);
                stageName.text = stage.displayName;
                stageDesc.text = stage.description;
                stagePreview.color = stage.groundTint;
                gm.PreviewStage(stage);
            }
            stageLock.SetActive(!StageUnlocked);
            prevButton.gameObject.SetActive(stageIndex > 0);
            nextButton.gameObject.SetActive(stageIndex < db.stages.Count - 1);

            Sprite spriteA = slotA == Element.Fire ? db.fireEmblem : db.waterEmblem;
            Sprite spriteB = slotB == Element.Fire ? db.fireEmblem : db.waterEmblem;
            slotAIcon.sprite = spriteA;
            slotBIcon.sprite = spriteB;
            string combo, detail;
            if (slotA != slotB) { combo = "불 + 물"; detail = "양쪽 일반기 8개 · 원소 반응 '증발 폭발'"; }
            else if (slotA == Element.Fire) { combo = "불 + 불"; detail = "불 일반기 4개 · 궁극기 '화염 기둥'"; }
            else { combo = "물 + 물"; detail = "물 일반기 4개 · 궁극기 '심해 소용돌이'"; }
            comboText.text = combo + "\n" + detail;
            // 장비 탭의 원소 슬롯도 같은 상태를 보여 준다
            if (equipSlotAIcon != null)
            {
                equipSlotAIcon.sprite = spriteA;
                equipSlotBIcon.sprite = spriteB;
                equipComboText.text = combo + "  ·  " + detail;
            }

            startBack.color = StageUnlocked ? new Color(0.95f, 0.6f, 0.2f, 1f) : new Color(0.35f, 0.37f, 0.45f, 1f);
            startLabel.text = StageUnlocked ? "전투 시작" : "잠김";
        }

        void StartRun()
        {
            if (!StageUnlocked) return;
            SaveData.SelectedStage = stageIndex;
            SaveData.SlotA = slotA;
            SaveData.SlotB = slotB;
            onStart?.Invoke(slotA, slotB, stageIndex);
        }

        // ------------------------------------------------------------------ 장비 페이지

        void BuildEquipPage(RectTransform page)
        {
            Image panel = UIFactory.Image("Character", page, db.roundedRect, PanelColor, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(980f, 720f));

            Image glow = UIFactory.Image("Glow", panel.transform, db.softCircle, new Color(0.45f, 0.4f, 0.9f, 0.35f));
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(460f, 460f));
            Image hero = UIFactory.Image("Hero", panel.transform, db.player, Color.white);
            hero.preserveAspect = true;
            UIFactory.Place(hero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(320f, 320f));

            // 좌우에 슬롯 3개씩
            EquipSlot[] left = { EquipSlot.Weapon, EquipSlot.Necklace, EquipSlot.Gloves };
            EquipSlot[] right = { EquipSlot.Armor, EquipSlot.Belt, EquipSlot.Boots };
            for (int i = 0; i < 3; i++)
            {
                MakeEquipSlot(panel.transform, left[i], new Vector2(-370f, 235f - i * 185f));
                MakeEquipSlot(panel.transform, right[i], new Vector2(370f, 235f - i * 185f));
            }

            // 캐릭터 아래: 원소 슬롯 2개 (눌러서 불/물 전환)
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Text elementTitle = UIFactory.Label("ElementTitle", panel.transform, "원소 (눌러서 변경)", 28, TextDim);
            UIFactory.Place(elementTitle.rectTransform, mid, mid, new Vector2(0f, -62f), new Vector2(420f, 40f));
            equipSlotAIcon = MakeElementSlot(panel.transform, mid, new Vector2(-95f, -160f), 140f, () => ToggleElementSlot(true));
            equipSlotBIcon = MakeElementSlot(panel.transform, mid, new Vector2(95f, -160f), 140f, () => ToggleElementSlot(false));
            Text elementPlus = UIFactory.Label("Plus", panel.transform, "+", 54, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(elementPlus.rectTransform, mid, mid, new Vector2(0f, -160f), new Vector2(50f, 60f));
            equipComboText = UIFactory.Label("Combo", panel.transform, "", 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(equipComboText.rectTransform, mid, mid, new Vector2(0f, -262f), new Vector2(940f, 40f));
            equipComboText.resizeTextForBestFit = true;
            equipComboText.resizeTextMinSize = 18;
            equipComboText.resizeTextMaxSize = 28;

            bonusText = UIFactory.Label("Bonus", panel.transform, "", 32, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(bonusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(940f, 60f));
            bonusText.resizeTextForBestFit = true;
            bonusText.resizeTextMinSize = 20;
            bonusText.resizeTextMaxSize = 32;

            Text title = UIFactory.Label("InventoryTitle", page, "보유 장비 (눌러서 장착 · 해제)", 34, TextDim, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -755f), new Vector2(960f, 50f));

            inventoryGrid = UIFactory.Rect("Inventory", page);
            UIFactory.Place(inventoryGrid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -812f), new Vector2(980f, 540f));
            var grid = inventoryGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(230f, 255f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            Image info = UIFactory.Image("Info", page, db.roundedRect, PanelColor, true);
            UIFactory.Place(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(980f, 110f));
            equipInfo = UIFactory.Label("Text", info.transform, "장비를 누르면 효과가 표시됩니다", 32, TextDim);
            UIFactory.Stretch(equipInfo.rectTransform, 20f, 6f, 20f, 6f);
        }

        void MakeEquipSlot(Transform parent, EquipSlot slot, Vector2 pos)
        {
            Button button = UIFactory.Button("Slot_" + slot, parent, db.roundedRect, PanelLight, out Image bg);
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(165f, 165f));
            Image frame = UIFactory.Image("Frame", bg.transform, db.roundedOutline, new Color(0.4f, 0.45f, 0.6f), true);
            UIFactory.Stretch(frame.rectTransform);
            Text label = UIFactory.Label("Label", bg.transform, EquipmentData.SlotName(slot), 28, new Color(0.5f, 0.55f, 0.7f));
            UIFactory.Stretch(label.rectTransform);
            Image icon = UIFactory.Image("Icon", bg.transform, null, Color.white);
            icon.preserveAspect = true;
            UIFactory.Stretch(icon.rectTransform, 12f, 12f, 12f, 12f);
            slotIcons[slot] = icon;
            slotFrames[slot] = frame;
            button.onClick.AddListener(() => Unequip(slot));
        }

        EquipmentData FindEquipped(EquipSlot slot)
        {
            string id = SaveData.GetEquipped(slot);
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < db.equipment.Count; i++)
                if (db.equipment[i] != null && db.equipment[i].Id == id) return db.equipment[i];
            return null;
        }

        void Unequip(EquipSlot slot)
        {
            EquipmentData item = FindEquipped(slot);
            if (item == null) return;
            SaveData.SetEquipped(slot, "");
            equipInfo.text = item.displayName + " 해제";
            RefreshEquipment();
        }

        void ToggleEquip(EquipmentData item)
        {
            bool equipped = SaveData.GetEquipped(item.slot) == item.Id;
            SaveData.SetEquipped(item.slot, equipped ? "" : item.Id);
            equipInfo.text = item.displayName + " (" + EquipmentData.SlotName(item.slot) + ")  " + item.Describe() +
                             (equipped ? "  · 해제" : "  · 장착");
            RefreshEquipment();
        }

        void RefreshEquipment()
        {
            foreach (KeyValuePair<EquipSlot, Image> pair in slotIcons)
            {
                EquipmentData item = FindEquipped(pair.Key);
                pair.Value.sprite = item != null ? item.icon : null;
                pair.Value.enabled = item != null;
                slotFrames[pair.Key].color = item != null ? Gold : new Color(0.4f, 0.45f, 0.6f);
            }

            for (int i = 0; i < inventoryTiles.Count; i++) Destroy(inventoryTiles[i]);
            inventoryTiles.Clear();
            for (int i = 0; i < db.equipment.Count; i++)
            {
                EquipmentData item = db.equipment[i];
                if (item == null) continue;
                bool equipped = SaveData.GetEquipped(item.slot) == item.Id;

                Button button = UIFactory.Button("Item", inventoryGrid, db.roundedRect, equipped ? new Color(0.3f, 0.25f, 0.1f, 1f) : PanelColor, out Image bg);
                Image frame = UIFactory.Image("Frame", bg.transform, db.roundedOutline, equipped ? Gold : new Color(0.4f, 0.45f, 0.6f), true);
                UIFactory.Stretch(frame.rectTransform);
                Image icon = UIFactory.Image("Icon", bg.transform, item.icon, Color.white);
                icon.preserveAspect = true;
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(150f, 150f));
                Text name = UIFactory.Label("Name", bg.transform, item.displayName, 28, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                UIFactory.Stretch(name.rectTransform, 8f, 0f, 8f, 40f);
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 28;
                Text state = UIFactory.Label("State", bg.transform, equipped ? "장착 중" : EquipmentData.SlotName(item.slot), 24,
                    equipped ? Gold : TextDim, TextAnchor.LowerCenter);
                UIFactory.Stretch(state.rectTransform, 8f, 0f, 8f, 10f);

                EquipmentData captured = item;
                button.onClick.AddListener(() => ToggleEquip(captured));
                inventoryTiles.Add(bg.gameObject);
            }

            bonusText.text = EquipmentBonus.Compute(db).Describe();
        }

        // ------------------------------------------------------------------ 도감 페이지

        void BuildCodexPage(RectTransform page)
        {
            Text title = UIFactory.Label("Title", page, "스킬 도감", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(900f, 70f));

            RectTransform gridRoot = UIFactory.Rect("Grid", page);
            UIFactory.Place(gridRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(980f, 1010f));
            var grid = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(482f, 130f);
            grid.spacing = new Vector2(16f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (SkillData skill in db.skills)
            {
                if (skill == null) continue;
                Color accent = skill.element == Element.Fire ? db.fireColor : db.waterColor;
                string tag = (skill.element == Element.Fire ? "불" : "물") + (skill.kind == SkillKind.Ultimate ? " · 궁극기" : " · 일반기");
                string info = skill.displayName + " (" + tag + ")\n" + skill.summary;
                AddCodexCell(gridRoot, skill.icon, skill.displayName, tag, accent, info);
            }
            foreach (PassiveData passive in db.passives)
            {
                if (passive == null) continue;
                string info = passive.displayName + " (패시브, 최대 Lv." + passive.maxLevel + ")\n" + passive.DescribeLevel(passive.maxLevel) + " (최대 레벨 기준)";
                AddCodexCell(gridRoot, passive.icon, passive.displayName, "패시브", new Color(0.75f, 0.8f, 0.9f), info);
            }

            Image infoPanel = UIFactory.Image("Info", page, db.roundedRect, PanelColor, true);
            UIFactory.Place(infoPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(980f, 200f));
            codexInfo = UIFactory.Label("Text", infoPanel.transform, "스킬을 누르면 설명이 표시됩니다", 32, TextDim);
            codexInfo.lineSpacing = 1.15f;
            UIFactory.Stretch(codexInfo.rectTransform, 24f, 10f, 24f, 10f);
        }

        void AddCodexCell(Transform parent, Sprite icon, string name, string tag, Color accent, string info)
        {
            Button button = UIFactory.Button("Cell", parent, db.roundedRect, PanelColor, out Image bg);
            Image img = UIFactory.Image("Icon", bg.transform, icon, Color.white);
            img.preserveAspect = true;
            UIFactory.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(100f, 100f));
            Text title = UIFactory.Label("Name", bg.transform, name, 36, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Stretch(title.rectTransform, 134f, 22f, 10f, 50f);
            Text sub = UIFactory.Label("Tag", bg.transform, tag, 26, Color.Lerp(accent, Color.white, 0.2f), TextAnchor.LowerLeft);
            UIFactory.Stretch(sub.rectTransform, 134f, 60f, 10f, 22f);
            button.onClick.AddListener(() => codexInfo.text = info);
        }
    }

    /// <summary>
    /// 가로 페이지 넘김. ScrollRect의 드래그가 끝나면 가장 알맞은 페이지로 부드럽게 붙인다.
    /// 살짝만 밀어도 다음 페이지로 넘어가도록 이동량 기준을 낮게 잡았다.
    /// </summary>
    public class LobbyPager : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        const float SwipeThreshold = 0.16f;   // 페이지 폭 대비 이만큼 밀면 넘어간다

        ScrollRect scroll;
        int pageCount;
        bool dragging, initialized;
        float target;

        public int Current { get; private set; }

        public void Init(ScrollRect scrollRect, int pages, int startPage)
        {
            scroll = scrollRect;
            pageCount = Mathf.Max(1, pages);
            Current = Mathf.Clamp(startPage, 0, pageCount - 1);
            target = Normalized(Current);
        }

        float Normalized(int page) => pageCount <= 1 ? 0f : page / (float)(pageCount - 1);

        public void GoTo(int page, bool instant)
        {
            Current = Mathf.Clamp(page, 0, pageCount - 1);
            target = Normalized(Current);
            dragging = false;
            if (instant) scroll.horizontalNormalizedPosition = target;
        }

        public void OnBeginDrag(PointerEventData e) => dragging = true;

        public void OnEndDrag(PointerEventData e)
        {
            dragging = false;
            float pos = scroll.horizontalNormalizedPosition * (pageCount - 1);
            float moved = pos - Current;
            int next = Current;
            if (moved > SwipeThreshold) next = Current + 1;
            else if (moved < -SwipeThreshold) next = Current - 1;
            GoTo(next, false);
        }

        void LateUpdate()
        {
            if (scroll == null) return;
            if (!initialized)
            {
                // 레이아웃이 잡힌 뒤 첫 프레임에 시작 페이지로 맞춘다
                initialized = true;
                scroll.horizontalNormalizedPosition = target;
                return;
            }
            if (dragging) return;
            float pos = scroll.horizontalNormalizedPosition;
            if (Mathf.Abs(pos - target) < 0.0005f)
            {
                if (pos != target) scroll.horizontalNormalizedPosition = target;
                return;
            }
            scroll.horizontalNormalizedPosition = Mathf.Lerp(pos, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        }
    }
}
