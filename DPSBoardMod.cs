using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable InconsistentNaming, ArrangeTypeMemberModifiers, HeapView.ObjectAllocation.Evident, UnusedMember.Local
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault, AccessToStaticMemberViaDerivedType
namespace DPSBoard
{
    public class DPSBoardMod : MelonMod
    {
        private static GameObject dpsOverlay;
        private static GameObject dpsContainer;
        private static readonly Dictionary<PlayerControl, DPSEntry> dpsEntries = new Dictionary<PlayerControl, DPSEntry>();
        internal static readonly Dictionary<PlayerControl, int> chapterStartDamage = new Dictionary<PlayerControl, int>();
        private static float updateTimer;
        private const float UPDATE_INTERVAL = 0.5f;
        private static bool isVisible;
        
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("VellumMod has started!");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            // Clean up when changing scenes
            if (dpsOverlay == null) return;
            GameObject.Destroy(dpsOverlay);
            dpsOverlay = null;
            dpsContainer = null;
            dpsEntries.Clear();
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F4))
            {
                ToggleDPSBoard();
            }

            UpdateDPSDisplayIfVisible();
        }

        private static void UpdateDPSDisplayIfVisible()
        {
            if (!isVisible || dpsOverlay == null || (updateTimer += Time.deltaTime) < UPDATE_INTERVAL) return;
            updateTimer = 0f;
            UpdateDPSDisplay();
        }

        private static void ToggleDPSBoard()
        {
            isVisible = !isVisible;
            
            if (isVisible)
            {
                if (dpsOverlay == null)
                {
                    CreateDPSOverlay();
                }
                dpsOverlay.SetActive(true);
                MelonLogger.Msg("DPS Board Enabled");
            }
            else if (dpsOverlay != null)
            {
                dpsOverlay.SetActive(false);
                MelonLogger.Msg("DPS Board Disabled");
            }
        }

        private static void CreateDPSOverlay()
        {
            CreateMainOverlayCanvas();
            GameObject backgroundPanel = CreateBackgroundPanel();
            AddTitle(backgroundPanel);
            CreateDPSEntryContainer(backgroundPanel);
        }

        private static void CreateDPSEntryContainer(GameObject backgroundPanel)
        {
            dpsContainer = new GameObject("DPSContainer");
            dpsContainer.transform.SetParent(backgroundPanel.transform, false);
            
            RectTransform containerRect = dpsContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.05f);
            containerRect.anchorMax = new Vector2(1, 0.9f);
            containerRect.offsetMin = new Vector2(10, 5);
            containerRect.offsetMax = new Vector2(-10, -5);

            VerticalLayoutGroup layoutGroup = dpsContainer.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 5;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
        }

        private static void AddTitle(GameObject backgroundPanel)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(backgroundPanel.transform, false);
            
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.9f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(10, 0);
            titleRect.offsetMax = new Vector2(-10, -5);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "DPS Board";
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
        }

        private static GameObject CreateBackgroundPanel()
        {
            GameObject backgroundPanel = new GameObject("BackgroundPanel");
            backgroundPanel.transform.SetParent(dpsOverlay.transform, false);
            
            RectTransform bgRect = backgroundPanel.AddComponent<RectTransform>();
            // This displays it above the healthbar
            // bgRect.anchorMin = new Vector2(0.02f, 0.3f);
            // bgRect.anchorMax = new Vector2(0.25f, 0.7f);
            // This displays it in the top right corner
            bgRect.anchorMin = new Vector2(0.75f, 0.7f);
            bgRect.anchorMax = new Vector2(0.98f, 0.98f);

            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = backgroundPanel.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);
            return backgroundPanel;
        }

        private static void CreateMainOverlayCanvas()
        {
            dpsOverlay = new GameObject("DPSBoardOverlay");
            GameObject.DontDestroyOnLoad(dpsOverlay);

            Canvas canvas = dpsOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1_000;

            CanvasScaler scaler = dpsOverlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1_920, 1_080);

            dpsOverlay.AddComponent<GraphicRaycaster>();
        }

        private static void UpdateDPSDisplay()
        {
            if (PlayerControl.AllPlayers == null || dpsContainer == null) return;

            IEnumerable<(PlayerControl player, float CurrentDPS)> playerDPSList = PlayerControl.AllPlayers
                .Where(player => player != null && player.Net != null && player.Net.CurrentDPS > 0)
                .OrderByDescending(player => player.Net.CurrentDPS)
                .Select(player => (player, player.Net.CurrentDPS));

            List<PlayerControl> toRemove = dpsEntries.Keys
                .Where(player => playerDPSList.All(p => p.player != player))
                .ToList();

            foreach (PlayerControl player in toRemove)
            {
                if (dpsEntries[player].gameObject != null) GameObject.Destroy(dpsEntries[player].gameObject);
                dpsEntries.Remove(player);
            }

            int index = 0;
            foreach ((PlayerControl player, float dps) in playerDPSList)
            {
                if (!dpsEntries.ContainsKey(player))
                {
                    CreateDPSEntry(player);
                }

                if (dpsEntries.TryGetValue(player, out DPSEntry entry))
                {
                    entry.UpdateStats(dps);
                    entry.gameObject.transform.SetSiblingIndex(index);
                }
                index++;
            }
        }

        private static void CreateDPSEntry(PlayerControl player)
        {
            GameObject entryObj = CreateEntryObject(player);
            dpsEntries[player] = new DPSEntry
            {
                gameObject = entryObj,
                nameText = CreatePlayerName(player, entryObj),
                dpsText = CreateDPSText(entryObj),
                player = player
            };
        }

        private static GameObject CreateEntryObject(PlayerControl player)
        {
            GameObject entryObj = new GameObject($"DPSEntry_{player.Username}");
            entryObj.transform.SetParent(dpsContainer.transform, false);

            RectTransform entryRect = entryObj.AddComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0, 30);

            CreateHorizontalLayout(entryObj);
            return entryObj;
        }

        private static TextMeshProUGUI CreateDPSText(GameObject entryObj)
        {
            GameObject dpsObj = new GameObject("DPSValue");
            dpsObj.transform.SetParent(entryObj.transform, false);
            
            RectTransform dpsRect = dpsObj.AddComponent<RectTransform>();
            dpsRect.sizeDelta = new Vector2(100, 0);

            TextMeshProUGUI dpsText = dpsObj.AddComponent<TextMeshProUGUI>();
            dpsText.text = "0";
            dpsText.fontSize = 18;
            dpsText.alignment = TextAlignmentOptions.Right;
            dpsText.color = Color.yellow;
            dpsText.fontStyle = FontStyles.Bold;
            return dpsText;
        }

        private static TextMeshProUGUI CreatePlayerName(PlayerControl player, GameObject entryObj)
        {
            GameObject nameObj = new GameObject("PlayerName");
            nameObj.transform.SetParent(entryObj.transform, false);
            
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(150, 0);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            // nameText.text = player.Username;
            nameText.text = "SomePlayer";
            nameText.fontSize = 18;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.color = GetPlayerColor(player);
            return nameText;
        }

        private static void CreateHorizontalLayout(GameObject entryObj)
        {
            HorizontalLayoutGroup hLayout = entryObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlHeight = true;
            hLayout.childControlWidth = false;
            hLayout.childForceExpandHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.padding = new RectOffset(5, 5, 2, 2);
        }

        private static Color GetPlayerColor(PlayerControl player)
        {
            if (player?.actions?.core?.Root == null) return Color.white;
            switch (player.actions.core.Root.magicColor)
            {
                case MagicColor.Red:    return new Color(1f, 0.3f, 0.3f);
                case MagicColor.Blue:   return new Color(0.3f, 0.6f, 1f);
                case MagicColor.Green:  return new Color(0.3f, 1f, 0.3f);
                case MagicColor.Yellow: return new Color(1f, 1f, 0.3f);
                case MagicColor.Purple: return new Color(0.8f, 0.3f, 1f);
            }
            return Color.white;
        }

        private class DPSEntry
        {
            public GameObject gameObject;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI dpsText;
            public PlayerControl player;
            private TextMeshProUGUI totalDamageText;
            private TextMeshProUGUI chapterDamageText;


            public void UpdateStats(float dps)
            {
                if (dpsText != null)
                {
                    int dpsInt = (int)dps;
                    if (dpsInt < 1_000)
                    {
                        dpsText.text = dpsInt.ToString();
                    }
                    else if (dpsInt < 1_000_000)
                    {
                        dpsText.text = $"{dpsInt / 1000f:F1}K";
                    }
                    else
                    {
                        dpsText.text = $"{dpsInt / 1_000_000f:F1}M";
                    }
                }

                if (nameText != null && player != null)
                {
                    nameText.color = GetPlayerColor(player);
                }

                // Add or update total damage and chapter damage
                if (player == null || player.PStats == null) return;
                int total = player.PStats.TotalDamage();
                int chapter = total;
                if (chapterStartDamage.TryGetValue(player, out int start))
                    chapter = total - start;

                if (totalDamageText == null)
                {
                    totalDamageText = CreateStatText(gameObject, "TotalDamage", Color.cyan, 100);
                }
                if (chapterDamageText == null)
                {
                    chapterDamageText = CreateStatText(gameObject, "ChapterDamage", Color.green, 100);
                }

                totalDamageText.text = $"Tome: {FormatNumber(total)}";
                chapterDamageText.text = $"Chapter: {FormatNumber(chapter)}";
            }

            private static TextMeshProUGUI CreateStatText(GameObject parent, string name, Color color, float width)
            {
                GameObject statObj = new GameObject(name);
                statObj.transform.SetParent(parent.transform, false);
                RectTransform rect = statObj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(width, 0);
                TextMeshProUGUI statText = statObj.AddComponent<TextMeshProUGUI>();
                statText.fontSize = 14;
                statText.alignment = TextAlignmentOptions.Right;
                statText.color = color;
                statText.fontStyle = FontStyles.Normal;
                return statText;
            }

            private static string FormatNumber(int value)
            {
                if (value < 1_000) return value.ToString();
                return value < 1_000_000 ? $"{value / 1000f:F1}K" : $"{value / 1_000_000f:F1}M";
            }
        }
    }

    // Harmony patch to ensure DPS tracking is always active
    [HarmonyPatch(typeof(CombatTextController), "LateUpdate")]
    public static class CombatTextController_LateUpdate_Patch
    {
        public static void Postfix(CombatTextController __instance)
        {
            if (PlayerControl.myInstance != null && PlayerControl.myInstance.Net != null)
            {
                PlayerControl.myInstance.Net.CurrentDPS = CombatTextController.CurrentDPS;
            }
        }
    }

    // Harmony patch to hook chapter start and record damage
    [HarmonyPatch(typeof(GameRecord), "NextChapter")]
    public static class GameRecord_NextChapter_Patch
    {
        public static void Postfix()
        {
            if (PlayerControl.AllPlayers == null) return;
            foreach (PlayerControl player in PlayerControl.AllPlayers)
            {
                if (player?.PStats != null)
                {
                    DPSBoardMod.chapterStartDamage[player] = player.PStats.TotalDamage();
                }
            }
        }
    }
}
