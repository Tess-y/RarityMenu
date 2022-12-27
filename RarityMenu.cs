using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using RarityLib.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using TMPro;
using UnboundLib;
using UnboundLib.Cards;
using UnboundLib.Networking;
using UnboundLib.Utils;
using UnboundLib.Utils.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RarntyMenu
{
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class RarityMenu : BaseUnityPlugin
    {
        private const string ModId = "Rarity.Toggle";
        private const string ModName = "Rarity Toggle";
        public const string Version = "0.2.1";
        bool ready = false;
        int maxRarity = 2;
        static bool first = true;

        internal static List<Card> allCards
        {
            get
            {
                return CardManager.cards.Values.OrderBy(card => card.cardInfo.cardName).ToList();
            }
        }

        public static Dictionary<string, ConfigEntry<string>> CardRaritys = new Dictionary<string, ConfigEntry<string>>();
        public static Dictionary<string, CardInfo.Rarity> CardDefaultRaritys = new Dictionary<string, CardInfo.Rarity>();
        public static Dictionary<string, TextMeshProUGUI> CardRaritysTexts = new Dictionary<string, TextMeshProUGUI>();
        public static Dictionary<string, List<CardInfo>> ModCards = new Dictionary<string, List<CardInfo>>();

        public void Awake()
        {
            new Harmony(ModId).PatchAll();
        }
        public void Start()
        {
            Unbound.Instance.ExecuteAfterFrames(60, () =>
            {
                string mod = "Vanilla";
                foreach (Card card in allCards)
                {
                    CardInfo cardInfo = card.cardInfo;
                    mod = card.category;
                    CardRaritys[cardInfo.name] = Config.Bind(ModId, cardInfo.name, "DEFAULT", $"Rarity value of card {cardInfo.cardName} from {mod}");
                    CardDefaultRaritys[cardInfo.name] = cardInfo.rarity;
                    CardInfo.Rarity cardRarity = RarityUtils.GetRarity(CardRaritys[cardInfo.name].Value);
                    if (cardRarity == CardInfo.Rarity.Common && CardRaritys[cardInfo.name].Value != "Common")
                    {
                        cardRarity = CardDefaultRaritys[cardInfo.name];
                        CardRaritys[cardInfo.name].Value = "DEFAULT";
                    }
                    cardInfo.rarity = cardRarity;
                    // mod
                    if (!ModCards.ContainsKey(mod)) ModCards.Add(mod, new List<CardInfo>());
                    ModCards[mod].Add(cardInfo);
                }
                maxRarity = Enum.GetValues(typeof(CardInfo.Rarity)).Length - 1;


                ready = true;
            });
            Unbound.RegisterMenu(ModName, delegate () { }, menu => Unbound.Instance.StartCoroutine(SetupGUI(menu)), null, false);
            Unbound.RegisterHandshake(ModId, this.OnHandShakeCompleted);
            SceneManager.sceneLoaded += (r, d) => first = true;
        }


        private void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC(typeof(RarityMenu), nameof(SyncSettings), new object[] { CardRaritys.Keys.ToArray(), CardRaritys.Values.Select(c => c.Value).ToArray() });
            }
        }
        [UnboundRPC]
        private static void SyncSettings(string[] cards, string[] rarities)
        {
            if (first)
            {
                first = false;
                Dictionary<string, string> cardRarities = new Dictionary<string, string>();
                for (int i = 0; i < cards.Length; i++)
                {
                    cardRarities[cards[i]] = rarities[i];
                }
                allCards.ForEach(c => c.cardInfo.rarity = cardRarities[c.cardInfo.name] != "DEFAULT" ? RarityUtils.GetRarity(cardRarities[c.cardInfo.name]) : CardDefaultRaritys[c.cardInfo.name]);
            }
        }


        private IEnumerator SetupGUI(GameObject menu)
        {
            yield return new WaitUntil(() => ready);
            yield return new WaitForSecondsRealtime(0.1f);
            NewGUI(menu);
            yield break;
        }

        private void NewGUI(GameObject menu)
        {
            MenuHandler.CreateText(ModName, menu, out _, 60, false, null, null, null, null);
            foreach (string mod in ModCards.Keys.OrderBy(m => m == "Vanilla" ? m : $"Z{m}"))
            {
                ModGUI(MenuHandler.CreateMenu(mod, () => { }, menu, 60, true, true, menu.transform.parent.gameObject), mod);
            }
        }

        private void ModGUI(GameObject menu, string mod)
        {
            MenuHandler.CreateText(mod.ToUpper(), menu, out _, 60, false, null, null, null, null);
            foreach (CardInfo card in ModCards[mod])
            {
                MenuHandler.CreateText(card.cardName, menu, out _, 30, color: CardChoice.instance.GetCardColor(card.colorTheme));
                Color common = new Color(0.0978f, 0.1088f, 0.1321f);
                Color c = RarityUtils.GetRarityData(CardRaritys[card.name].Value != "DEFAULT" ? RarityUtils.GetRarity(CardRaritys[card.name].Value) : CardDefaultRaritys[card.name]).colorOff;
                CardRaritysTexts[card.name] = CreateSliderWithoutInput(CardRaritys[card.name].Value, menu, 30, -1, maxRarity, CardRaritys[card.name].Value == "DEFAULT" ? -1 : (int)RarityUtils.GetRarity(CardRaritys[card.name].Value), (value) =>
                {

                    if (value >= 0)
                        CardRaritys[card.name].Value = RarityUtils.GetRarityData((CardInfo.Rarity)(int)value).name;
                    else
                        CardRaritys[card.name].Value = "DEFAULT";
                    card.rarity = CardRaritys[card.name].Value != "DEFAULT" ? RarityUtils.GetRarity(CardRaritys[card.name].Value) : CardDefaultRaritys[card.name];
                    try
                    {
                        Color common = new Color(0.0978f, 0.1088f, 0.1321f);
                        Color c = RarityUtils.GetRarityData(CardRaritys[card.name].Value != "DEFAULT" ? RarityUtils.GetRarity(CardRaritys[card.name].Value) : CardDefaultRaritys[card.name]).colorOff;
                        CardRaritysTexts[card.name].text = value >= 0 ? card.rarity.ToString().ToUpper() : "DEFAULT";
                        CardRaritysTexts[card.name].color = c.Equals(common) ? Color.white : c;
                    }
                    catch { }
                }, out _, true, color: c.Equals(common) ? Color.white : c).GetComponentsInChildren<TextMeshProUGUI>()[2];
            }
        }



        private static GameObject CreateSliderWithoutInput(string text, GameObject parent, int fontSize, float minValue, float maxValue, float defaultValue,
            UnityAction<float> onValueChangedAction, out Slider slider, bool wholeNumbers = false, Color? sliderColor = null, Slider.Direction direction = Slider.Direction.LeftToRight, bool forceUpper = true, Color? color = null, TMP_FontAsset font = null, Material fontMaterial = null, TextAlignmentOptions? alignmentOptions = null)
        {
            GameObject sliderObj = MenuHandler.CreateSlider(text, parent, fontSize, minValue, maxValue, defaultValue, onValueChangedAction, out slider, wholeNumbers, sliderColor, direction, forceUpper, color, font, fontMaterial, alignmentOptions);

            UnityEngine.GameObject.Destroy(sliderObj.GetComponentInChildren<TMP_InputField>().gameObject);

            return sliderObj;

        }
    }

    [Serializable]
    [HarmonyPatch(typeof(ToggleCardsMenuHandler), nameof(ToggleCardsMenuHandler.UpdateVisualsCardObj))]
    public class Patch
    {
        public static void Prefix(GameObject cardObject)
        {
            Unbound.Instance.ExecuteAfterFrames(15, () => {
                if (ToggleCardsMenuHandler.cardMenuCanvas.gameObject.activeSelf)
                {
                    string name = cardObject.GetComponentInChildren<CardInfo>().name.Substring(0, cardObject.GetComponentInChildren<CardInfo>().name.Length - 7);
                    cardObject.GetComponentInChildren<CardInfo>().rarity = RarityMenu.CardRaritys[name].Value != "DEFAULT" ? RarityUtils.GetRarity(RarityMenu.CardRaritys[name].Value) : RarityMenu.CardDefaultRaritys[name];
                    cardObject.GetComponentsInChildren<CardRarityColor>().ToList().ForEach(r => r.Toggle(true));
                }
            });
        }
    }
}