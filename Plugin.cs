using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static UnityEngine.ParticleSystem.PlaybackState;
using UnityEngine.TextCore.Text;


namespace Deck_Distributor
{
    [BepInPlugin("com.meds.deckdistributor", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new("com.meds.deckdistributor");
        internal static ManualLogSource Log;
        private void Awake()
        {
            // Plugin startup logic
            Log = Logger;
            Logger.LogInfo($"Deck Distributor is at your disposal!");
            harmony.PatchAll();
        }
    }
    [HarmonyPatch]
    public class DDDDDuel
    {
        // public static Dictionary<int, BotonGeneric> medsImportButtons;
        // public static Dictionary<int, BotonGeneric> medsExportButtons;
        // public static BotonGeneric medsImportButton;
        // public static BotonGeneric medsExportButton;
        public static int currentSlot;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DeckSlot), "Awake")]
        public static void AwakePrefix(ref DeckSlot __instance)
        {
            Plugin.Log.LogDebug("making import button for slot " + __instance.slot);
            // create import/export buttons
            BotonGeneric medsImportButton = UnityEngine.Object.Instantiate(__instance.saveButton.GetComponent<BotonGeneric>(), __instance.saveButton.parent);
            medsImportButton.transform.SetSiblingIndex(__instance.title.transform.GetSiblingIndex() - 1);
            medsImportButton.name = "medsDeckImportButton";
            medsImportButton.auxInt = __instance.slot;
            medsImportButton.transform.position = __instance.saveButton.parent.position + UnityEngine.Vector3.left * 0.9f;
            medsImportButton.idTranslate = "";
            medsImportButton.color = new UnityEngine.Color(0.1f, 0.3f, 0.3f);
            medsImportButton.SetText("Import");
            medsImportButton.enabled = true;
            medsImportButton.gameObject.SetActive(true);
            Plugin.Log.LogDebug("making export button for slot " + __instance.slot);
            BotonGeneric medsExportButton = UnityEngine.Object.Instantiate(__instance.saveButton.GetComponent<BotonGeneric>(), __instance.saveButton.parent);
            medsExportButton.transform.SetSiblingIndex(__instance.title.transform.GetSiblingIndex() - 1);
            medsExportButton.name = "medsDeckExportButton";
            medsExportButton.auxInt = __instance.slot;
            medsExportButton.transform.position = __instance.saveButton.parent.position + UnityEngine.Vector3.left * 0.9f;
            medsExportButton.idTranslate = "";
            //medsExportButton.SetBackgroundColor(new UnityEngine.Color(0.3765f, 0.3059f, 0.4157f));
            medsExportButton.color = new UnityEngine.Color(0.3f, 0.3f, 0.1f);
            medsExportButton.SetText("Export");
            medsExportButton.enabled = true;
            medsExportButton.gameObject.SetActive(true);
            medsExportButton.SetText("Export");
            Plugin.Log.LogDebug("import/export buttons have been created for slot " + __instance.slot);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DeckSlot), "SetEmpty")]
        public static void SetEmptyPostfix(ref DeckSlot __instance)
        {
            __instance.saveButton.parent.Find("medsDeckImportButton").gameObject.SetActive(true);
            __instance.saveButton.parent.Find("medsDeckExportButton").gameObject.SetActive(false);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DeckSlot), "SetActive")]
        public static void SetActivePostfix(ref DeckSlot __instance)
        {
            __instance.saveButton.parent.Find("medsDeckImportButton").gameObject.SetActive(false);
            __instance.saveButton.parent.Find("medsDeckExportButton").gameObject.SetActive(true);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BotonGeneric), "Clicked")]
        public static bool ClickedPrefix(ref BotonGeneric __instance)
        {
            if (__instance.gameObject.name == "medsDeckImportButton")
            {
                currentSlot = __instance.auxInt;
                AlertManager.buttonClickDelegate = new AlertManager.OnButtonClickDelegate(ImportDeckSlot);
                AlertManager.Instance.AlertPasteCopy(Texts.Instance.GetText("pressForImportTree"));
                return false;
            }
            else if (__instance.gameObject.name == "medsDeckExportButton")
            {
                Hero hero = AtOManager.Instance.GetHero(CardCraftManager.Instance.heroIndex);
                if (hero != null)
                {
                    List<string> stringList = new List<string>();
                    stringList.Add(hero.SourceName);
                    if (PlayerManager.Instance.PlayerSavedDeck.DeckTitle.ContainsKey(hero.SourceName) && PlayerManager.Instance.PlayerSavedDeck.DeckTitle[hero.SourceName][__instance.auxInt] != null && PlayerManager.Instance.PlayerSavedDeck.DeckTitle[hero.SourceName][__instance.auxInt] != "")
                        stringList.Add(PlayerManager.Instance.PlayerSavedDeck.DeckTitle[hero.SourceName][__instance.auxInt].Replace("|",""));
                    else
                        stringList.Add("unnamed");
                    for (int index = 0; index < hero.Cards.Count; ++index)
                    {
                        CardData cardData = Globals.Instance.GetCardData(hero.Cards[index]);
                        if (cardData.CardClass != Enums.CardClass.Injury && cardData.CardClass != Enums.CardClass.Boon && cardData.CardUpgraded != Enums.CardUpgraded.Rare)
                            stringList.Add(hero.Cards[index]);
                    }
                    string inputText = Functions.CompressString(string.Join("|", stringList.ToArray()));
                    AlertManager.Instance.AlertCopyPaste("Share code for this deck configuration", inputText);
                }
                return false;
            }
            return true;
        }

        public static void ImportDeckSlot()
        {
            AlertManager.buttonClickDelegate -= new AlertManager.OnButtonClickDelegate(ImportDeckSlot);
            if (!AlertManager.Instance.GetConfirmAnswer())
                return;
            string compressedText = Functions.OnlyAscii(AlertManager.Instance.GetInputPCValue()).Trim();
            string newDeckText = "";
            Plugin.Log.LogDebug("IMPORTING DECK FOR SLOT " + currentSlot + ": " + compressedText);
            try
            {
                newDeckText = Functions.DecompressString(compressedText);
                string[] newDeck = newDeckText.Split('|');
                if (newDeck.Length > 2)
                {
                    Hero hero = AtOManager.Instance.GetHero(CardCraftManager.Instance.heroIndex);
                    if (newDeck[0] == hero.SourceName)
                    {
                        List<string> stringList = new List<string>();
                        for (int index = 2; index < newDeck.Length; ++index)
                        {
                            CardData cardData = Globals.Instance.GetCardData(newDeck[index]);
                            if (cardData != (CardData)null && cardData.CardClass != Enums.CardClass.Injury && cardData.CardClass != Enums.CardClass.Boon && cardData.CardUpgraded != Enums.CardUpgraded.Rare)
                                stringList.Add(newDeck[index]);
                        }
                        //string sourceName = AtOManager.Instance.GetHero(this.heroIndex).SourceName;
                        if (!PlayerManager.Instance.PlayerSavedDeck.DeckTitle.ContainsKey(hero.SourceName))
                            PlayerManager.Instance.PlayerSavedDeck.DeckTitle.Add(hero.SourceName, new string[20]);
                        if (!PlayerManager.Instance.PlayerSavedDeck.DeckCards.ContainsKey(hero.SourceName))
                            PlayerManager.Instance.PlayerSavedDeck.DeckCards.Add(hero.SourceName, new List<string>[20]);
                        PlayerManager.Instance.PlayerSavedDeck.DeckTitle[hero.SourceName][currentSlot] = newDeck[1];
                        PlayerManager.Instance.PlayerSavedDeck.DeckCards[hero.SourceName][currentSlot] = stringList;
                        SaveManager.SavePlayerDeck();
                        // call CardCraftManager.LoadDecks
                        MethodInfo loadDecks = CardCraftManager.Instance.GetType().GetMethod("LoadDecks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        loadDecks.Invoke(CardCraftManager.Instance, new object[] { });
                    }
                    else
                    {
                        // alert: not for this hero!
                        AlertManager.Instance.AlertConfirm("ERROR!\nThe deck you are trying to import is not for the current hero.");
                    }
                }
            }
            catch
            {
                AlertManager.Instance.AlertConfirm("ERROR!\nInvalid deck import code.");
            }
        }
    }
}
