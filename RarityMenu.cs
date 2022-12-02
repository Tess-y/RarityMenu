using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
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
using UnityEngine.UI;
using static PlayerInRangeTrigger;

namespace RarntyMenu
{
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class RarityMenu : BaseUnityPlugin
    {
        private const string ModId = "Rarity.Toggle";
        private const string ModName = "Rarity Toggle";
        public const string Version = "0.1.2";
        bool ready = false;
        int maxRarity = 2;

        internal static List<CardInfo> defaultCards
        {
            get
            {
                return ((CardInfo[])typeof(CardManager).GetField("defaultCards", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).ToList();
            }
        }
        internal static List<CardInfo> activeCards
        {
            get
            {
                return ((ObservableCollection<CardInfo>)typeof(CardManager).GetField("activeCards", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).ToList();
            }
        }
        internal static List<CardInfo> inactiveCards
        {
            get
            {
                return (List<CardInfo>)typeof(CardManager).GetField("inactiveCards", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            }
            set { }
        }

        internal static List<CardInfo> allCards
        {
            get
            {
                var r = activeCards.Concat(inactiveCards).ToList();
                r.Sort((c1,c2) => c1.cardName.CompareTo(c2.cardName));
                return r;
            }
            set { }
        }

        public static Dictionary<string, ConfigEntry<int>> CardRaritys = new Dictionary<string, ConfigEntry<int>>();
        public static Dictionary<string, int> CardDefaultRaritys = new Dictionary<string, int>();
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
                foreach (CardInfo card in allCards)
                {
                    if (card.gameObject.GetComponent<CustomCard>() != null)
                    {
                        mod = card.gameObject.GetComponent<CustomCard>().GetModName().ToLower();
                    }
                    else
                    {
                        mod = "Vanilla";
                    }
                    CardRaritys[card.name] = Config.Bind(ModId, card.name, -1, $"Rarity value of card {card.cardName} from {mod}");
                    CardDefaultRaritys[card.name] = (int)card.rarity;
                    card.rarity = (CardInfo.Rarity)(CardRaritys[card.name].Value >= 0?CardRaritys[card.name].Value : CardDefaultRaritys[card.name]);
                    // mod
                    if (!ModCards.ContainsKey(mod)) ModCards.Add(mod, new List<CardInfo>());
                    ModCards[mod].Add(card);
                }
                maxRarity = Enum.GetValues(typeof(CardInfo.Rarity)).Length - 1;


                ready = true;
            });
            Unbound.RegisterMenu(ModName, delegate () { }, menu => Unbound.Instance.StartCoroutine(SetupGUI(menu)), null, false);
            gameObject.AddComponent<Sync>();
            //Unbound.RegisterHandshake(ModId, this.OnHandShakeCompleted);
        }


        private void OnHandShakeCompleted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC(typeof(RarityMenu), nameof(SyncSettings), new object[] { CardRaritys.Keys.ToArray(), CardRaritys.Values.Select(c => c.Value).ToArray() });
            }
        }
        [UnboundRPC]
        internal static void SyncSettings(string[] cards, int[] rarities)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                allCards.Find(c=>c.name == cards[i]).rarity = (CardInfo.Rarity)(rarities[i] >= 0 ? rarities[i] : CardDefaultRaritys[cards[i]]);
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
            foreach(string mod in ModCards.Keys)
            {
                ModGUI(MenuHandler.CreateMenu(mod, () => { }, menu, 60, true, true, menu.transform.parent.gameObject),mod);
            }
        }

        private void ModGUI(GameObject menu, string mod)
        {
            MenuHandler.CreateText(mod.ToUpper(), menu, out _, 60, false, null, null, null, null);
            foreach(CardInfo card in ModCards[mod])
            {
                MenuHandler.CreateText(card.cardName, menu, out _, 30, color: CardChoice.instance.GetCardColor(card.colorTheme));
                Color common = new Color(0.0978f, 0.1088f, 0.1321f);
                Color c = RarityLib.Utils.RarityUtils.GetRarityData(CardRaritys[card.name].Value >= 0 ? ((CardInfo.Rarity)CardRaritys[card.name].Value) : card.rarity).colorOff;
                CardRaritysTexts[card.name] = CreateSliderWithoutInput(CardRaritys[card.name].Value >= 0?((CardInfo.Rarity)CardRaritys[card.name].Value).ToString(): "DEFAULT", menu, 30, -1, maxRarity, CardRaritys[card.name].Value, (value) =>
                {
                    CardRaritys[card.name].Value = (int)value;
                    card.rarity = (CardInfo.Rarity)(CardRaritys[card.name].Value >= 0 ? CardRaritys[card.name].Value : CardDefaultRaritys[card.name]);
                    try
                    {
                        Color common = new Color(0.0978f, 0.1088f, 0.1321f);
                        Color c = RarityLib.Utils.RarityUtils.GetRarityData(CardRaritys[card.name].Value >= 0 ? ((CardInfo.Rarity)CardRaritys[card.name].Value) : card.rarity).colorOff;
                        CardRaritysTexts[card.name].text = value>=0?card.rarity.ToString().ToUpper() : "DEFAULT";
                        CardRaritysTexts[card.name].color = c.Equals(common) ? Color.white : c;
                    }
                    catch { }
                }, out _, true, color:c.Equals(common)?Color.white:c).GetComponentsInChildren<TextMeshProUGUI>()[2];
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
    [HarmonyPatch(typeof(ToggleCardsMenuHandler),nameof(ToggleCardsMenuHandler.UpdateVisualsCardObj))]
    public class Patch
    {
        public static void Prefix(GameObject cardObject)
        {
            Unbound.Instance.ExecuteAfterFrames(15, () => { if (ToggleCardsMenuHandler.cardMenuCanvas.gameObject.activeSelf) {
                    string name = cardObject.GetComponentInChildren<CardInfo>().name.Substring(0, cardObject.GetComponentInChildren<CardInfo>().name.Length - 7);
                    cardObject.GetComponentInChildren<CardInfo>().rarity = (CardInfo.Rarity)(RarityMenu.CardRaritys[name].Value >= 0 ? RarityMenu.CardRaritys[name].Value : RarityMenu.CardDefaultRaritys[name]);
                    cardObject.GetComponentsInChildren<CardRarityColor>().ToList().ForEach(r => r.Toggle(true)); 
                } });
        }
    }

    [DisallowMultipleComponent]
    public class Sync : MonoBehaviourPunCallbacks
    {
        public static Sync instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                DestroyImmediate(this);
            }
        }
        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                RaiseEventOptions options = new RaiseEventOptions();
                options.TargetActors = new int[] { newPlayer.ActorNumber };
                NetworkingManager.RPC(typeof(RarityMenu), nameof(RarityMenu.SyncSettings), options, new object[] { RarityMenu.CardRaritys.Keys.ToArray(), RarityMenu.CardRaritys.Values.Select(c => c.Value).ToArray() });
            }
        }
    }
}
