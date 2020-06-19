﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CaptivityEvents.Brothel;
using CaptivityEvents.CampaignBehaviors;
using CaptivityEvents.Custom;
using CaptivityEvents.Events;
using CaptivityEvents.Helper;
using CaptivityEvents.Models;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.TwoDimension;
using Path = System.IO.Path;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace CaptivityEvents
{
    public class CESubModule : MBSubModuleBase
    {
        // Brothel State
        public enum BrothelState
        {
            Normal,
            Start,
            FadeIn,
            Black,
            FadeOut
        }

        // Dungeon State
        public enum DungeonState
        {
            Normal,
            StartWalking,
            FadeIn
        }

        // Hunt State
        public enum HuntState
        {
            Normal,
            StartHunt,
            HeadStart,
            Hunting,
            AfterBattle
        }

        public static List<CEEvent> CEEvents = new List<CEEvent>();
        private static List<CECustom> _ceFlags = new List<CECustom>();

        public static readonly List<CEEvent> CEEventList = new List<CEEvent>();
        public static readonly List<CEEvent> CEWaitingList = new List<CEEvent>();
        public static readonly List<CEEvent> CECallableEvents = new List<CEEvent>();

        private static readonly Dictionary<string, Texture> CEEventImageList = new Dictionary<string, Texture>();

        // Captive Menu
        public static bool CaptivePlayEvent;

        public static CharacterObject CaptiveToPlay;

        // Animation Flags
        public static bool AnimationPlayEvent;

        public static List<string> AnimationImageList = null;
        public static int AnimationIndex;
        public static float AnimationSpeed = 0.03f;

        // Last Check
        private static float _lastCheck;

        private static float _dungeonFadeOut = 2f;
        public static DungeonState dungeonState = DungeonState.Normal;

        public static Agent AgentTalkingTo;
        public static GameEntity GameEntity = null;
        public static float PlayerSpeed = 0f;
        public static BrothelState brothelState = BrothelState.Normal;

        private static float _brothelFadeIn = 2f;
        private const float BrothelBlack = 10f;
        private const float BrothelFadeOut = 2f;

        private static float _brothelTimerOne;
        private static float _brothelTimerTwo;
        private static float _brothelTimerThree;

        private const float BrothelSoundMin = 1f;
        private const float BrothelSoundMax = 3f;

        public static HuntState huntState = HuntState.Normal;

        private readonly Dictionary<string, int> _brothelSounds = new Dictionary<string, int>();
        private bool _isLoaded;
        private bool _isLoadedInGame;

        public static void LoadTexture(string name, bool swap = false, bool forcelog = false)
        {
            try
            {
                if (!swap)
                {
                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[34] = name == "default"
                        ? CEEventImageList["default_female_prison"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[13] = name == "default"
                        ? CEEventImageList["default_female"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[28] = name == "default"
                        ? CEEventImageList["default_male_prison"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[12] = name == "default"
                        ? CEEventImageList["default_male"]
                        : CEEventImageList[name];
                }
                else
                {
                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[34] = name == "default"
                        ? CEEventImageList["default_male_prison"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[13] = name == "default"
                        ? CEEventImageList["default_male"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[28] = name == "default"
                        ? CEEventImageList["default_female_prison"]
                        : CEEventImageList[name];

                    UIResourceManager.SpriteData.SpriteCategories["ui_fullbackgrounds"].SpriteSheets[12] = name == "default"
                        ? CEEventImageList["default_female"]
                        : CEEventImageList[name];
                }
            }
            catch (Exception e)
            {
                if (forcelog)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Failure to load " + name + ". Refer to LogFileFC.txt in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs", Colors.Red));
                    CECustomHandler.LogMessage("Failure to load " + name + " - exception : " + e.Message);
                }
                else
                {
                    CECustomHandler.LogToFile("Failed to load the texture of " + name);
                }
            }
        }

        public static void LoadCampaignNotificationTexture(string name, int sheet = 0, bool forcelog = false)
        {
            try
            {
                UIResourceManager.SpriteData.SpriteCategories["ce_notification_icons"].SpriteSheets[sheet] = name == "default"
                    ? CEEventImageList["CE_default_notification"]
                    : CEEventImageList[name];
            }
            catch (Exception e)
            {
                if (forcelog)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Failure to load " + name + ". Refer to LogFileFC.txt in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs", Colors.Red));
                    CECustomHandler.LogMessage("Failure to load " + name + " - exception : " + e.Message);
                }
                else
                {
                    CECustomHandler.LogToFile("Failed to load the texture of " + name);
                }
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            var ceModule = ModuleInfo.GetModules().FirstOrDefault(searchInfo => searchInfo.Id == "zCaptivityEvents");

            if (ceModule != null)
            {
                var moduleVersion = ceModule.Version;
                var nativeModule = ModuleInfo.GetModules().FirstOrDefault(searchInfo => searchInfo.IsNative());

                if (nativeModule != null)
                {
                    var gameVersion = nativeModule.Version;

                    if (gameVersion.Major != moduleVersion.Major || gameVersion.Minor != moduleVersion.Minor || gameVersion.Revision != moduleVersion.Revision)
                    {
                        CECustomHandler.LogMessage("Captivity Events " + moduleVersion + " has the detected the wrong version " + gameVersion);
                        MessageBox.Show("Warning:\n Captivity Events " + moduleVersion + " has the detected the wrong game version. Please download the correct version for " + gameVersion + ". Or continue at your own risk.", "Captivity Events has the detected the wrong version");
                    }
                }
            }

            var modulesFound = Utilities.GetModulesNames();
            var modulePaths = new List<string>();

            CECustomHandler.LogMessage("\n -- Loaded Modules -- \n" + string.Join("\n", modulesFound));

            foreach (var moduleID in modulesFound)
                try
                {
                    var moduleInfo = ModuleInfo.GetModules().FirstOrDefault(searchInfo => searchInfo.Id == moduleID);

                    if (moduleInfo != null && !moduleInfo.DependedModuleIds.Contains("zCaptivityEvents")) continue;

                    try
                    {
                        if (moduleInfo == null) continue;
                        CECustomHandler.LogMessage("Added to ModuleLoader: " + moduleInfo.Name);
                        modulePaths.Insert(0, Path.GetDirectoryName(ModuleInfo.GetPath(moduleInfo.Id)));
                    }
                    catch (Exception)
                    {
                        CECustomHandler.LogMessage("Failed to Load " + moduleInfo.Name + " Events");
                    }
                }
                catch (Exception)
                {
                    CECustomHandler.LogMessage("Failed to fetch DependedModuleIds from " + moduleID);
                }

            // Load Events
            CEEvents = CECustomHandler.GetAllVerifiedXSEFSEvents(modulePaths);
            _ceFlags = CECustomHandler.GetFlags();

            // Load Images
            var fullPath = BasePath.Name + "Modules/zCaptivityEvents/ModuleLoader/";
            var requiredPath = fullPath + "CaptivityRequired";

            // Get Required
            var requiredImages = Directory.GetFiles(requiredPath, "*.png", SearchOption.AllDirectories);

            // Get All in ModuleLoader
            var files = Directory.GetFiles(fullPath, "*.png", SearchOption.AllDirectories);

            // Module Image Load
            if (modulePaths.Count != 0)
                foreach (var filepath in modulePaths)
                    try
                    {
                        var moduleFiles = Directory.GetFiles(filepath, "*.png", SearchOption.AllDirectories);

                        foreach (var file in moduleFiles)
                            if (!CEEventImageList.ContainsKey(Path.GetFileNameWithoutExtension(file)))
                                try
                                {
                                    var texture = TaleWorlds.Engine.Texture.LoadTextureFromPath($"{Path.GetFileName(file)}", $"{Path.GetDirectoryName(file)}");
                                    texture.PreloadTexture();
                                    var texture2D = new Texture(new EngineTexture(texture));
                                    CEEventImageList.Add(Path.GetFileNameWithoutExtension(file), texture2D);
                                }
                                catch (Exception e)
                                {
                                    CECustomHandler.LogMessage("Failure to load " + file + " - exception : " + e);
                                }
                            else CECustomHandler.LogMessage("Failure to load " + file + " - duplicate found.");
                    }
                    catch (Exception) { }

            // Captivity Location Image Load
            try
            {
                foreach (var file in files)
                {
                    if (requiredImages.Contains(file)) continue;

                    if (!CEEventImageList.ContainsKey(Path.GetFileNameWithoutExtension(file)))
                        try
                        {
                            var texture = TaleWorlds.Engine.Texture.LoadTextureFromPath($"{Path.GetFileName(file)}", $"{Path.GetDirectoryName(file)}");
                            texture.PreloadTexture();
                            var texture2D = new Texture(new EngineTexture(texture));
                            CEEventImageList.Add(Path.GetFileNameWithoutExtension(file), texture2D);
                        }
                        catch (Exception e)
                        {
                            CECustomHandler.LogMessage("Failure to load " + file + " - exception : " + e);
                        }
                    else CECustomHandler.LogMessage("Failure to load " + file + " - duplicate found.");
                }

                foreach (var file in requiredImages)
                {
                    if (CEEventImageList.ContainsKey(Path.GetFileNameWithoutExtension(file))) continue;

                    try
                    {
                        var texture = TaleWorlds.Engine.Texture.LoadTextureFromPath($"{Path.GetFileName(file)}", $"{Path.GetDirectoryName(file)}");
                        texture.PreloadTexture();
                        var texture2D = new Texture(new EngineTexture(texture));
                        CEEventImageList.Add(Path.GetFileNameWithoutExtension(file), texture2D);
                    }
                    catch (Exception e)
                    {
                        CECustomHandler.LogMessage("Failure to load " + file + " - exception : " + e);
                    }
                }

                // Load the Notifications Sprite
                // 1.4.1 Checked
                var loadedData = new SpriteData("CESpriteData");
                loadedData.Load(UIResourceManager.UIResourceDepot);

                const string categoryName = "ce_notification_icons";
                const string partNameCaptor = "CEEventNotification\\notification_captor";
                const string partNameEvent = "CEEventNotification\\notification_event";
                var spriteData = UIResourceManager.SpriteData;
                spriteData.SpriteCategories.Add(categoryName, loadedData.SpriteCategories[categoryName]);
                spriteData.SpritePartNames.Add(partNameCaptor, loadedData.SpritePartNames[partNameCaptor]);
                spriteData.SpritePartNames.Add(partNameEvent, loadedData.SpritePartNames[partNameEvent]);
                spriteData.SpriteNames.Add(partNameCaptor, new SpriteGeneric(partNameCaptor, loadedData.SpritePartNames[partNameCaptor]));
                spriteData.SpriteNames.Add(partNameEvent, new SpriteGeneric(partNameEvent, loadedData.SpritePartNames[partNameEvent]));

                var spriteCategory = spriteData.SpriteCategories[categoryName];
                spriteCategory.SpriteSheets.Add(CEEventImageList["CE_default_notification"]);
                spriteCategory.SpriteSheets.Add(CEEventImageList["CE_default_notification"]);
                spriteCategory.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);

                UIResourceManager.BrushFactory.Initialize();

                LoadTexture("default", false, true);
            }
            catch (Exception e)
            {
                CECustomHandler.LogMessage("Failure to load textures. " + e);
            }

            CECustomHandler.LogMessage("Loaded " + CEEventImageList.Count + " images and " + CEEvents.Count + " events.");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            try
            {
                CECustomHandler.LogMessage("Loaded CESettings: "
                                           + (CESettings.Instance.LogToggle
                                               ? "Logs are enabled."
                                               : "Extra Event Logs are disabled enable them through settings."));
            }
            catch (Exception)
            {
                CECustomHandler.LogMessage("OnBeforeInitialModuleScreenSetAsRoot : CESettings is being accessed improperly.");
            }

            try
            {
                var harmony = new Harmony("com.CE.captivityEvents");
                var dict = Harmony.VersionInfo(out var myVersion);
                CECustomHandler.LogMessage("My version: " + myVersion);

                foreach (var entry in dict)
                {
                    var id = entry.Key;
                    var version = entry.Value;
                    CECustomHandler.LogMessage("Mod " + id + " uses Harmony version " + version);
                }

                CECustomHandler.LogMessage(CESettings.Instance != null && CESettings.Instance.EventCaptorNotifications
                                               ? "Patching Map Notifications: No Conflicts Detected : Enabled."
                                               : "EventCaptorNotifications: Disabled.");

                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                CECustomHandler.LogMessage("Failed to load: " + ex);
                MessageBox.Show($"Error Initializing Captivity Events:\n\n{ex}");
            }

            foreach (var listedEvent in CEEvents)
            {
                if (listedEvent.Name.IsStringNoneOrEmpty()) continue;

                if (listedEvent.MultipleListOfCustomFlags != null && listedEvent.MultipleListOfCustomFlags.Count > 0)
                    if (!_ceFlags.Exists(match => match.CEFlags.Any(x => listedEvent.MultipleListOfCustomFlags.Contains(x))))
                        continue;

                if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Overwriteable) && CEEvents.FindAll(matchEvent => { return matchEvent.Name == listedEvent.Name; }).Count > 1) continue;

                if (!CEContext.brothelFlagFemale)
                    if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Captive) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.LocationCity) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.HeroIsProstitute) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Prostitution) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.HeroGenderIsFemale))
                        CEContext.brothelFlagFemale = true;

                if (!CEContext.brothelFlagMale)
                    if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Captive) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.LocationCity) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.HeroIsProstitute) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Prostitution) && listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.HeroGenderIsMale))
                        CEContext.brothelFlagMale = true;

                if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.WaitingMenu))
                {
                    CEWaitingList.Add(listedEvent);
                }
                else
                {
                    if (!listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.CanOnlyBeTriggeredByOtherEvent)) CECallableEvents.Add(listedEvent);

                    CEEventList.Add(listedEvent);
                }
            }

            CECustomHandler.LogMessage("Loaded " + CEWaitingList.Count + " waiting menus ");
            CECustomHandler.LogMessage("Loaded " + CECallableEvents.Count + " callable events ");

            if (_isLoaded) return;

            if (CEEvents.Count > 0)
            {
                try
                {
                    var textObject = new TextObject("{=CEEVENTS1000}Captivity Events Loaded with {EVENT_COUNT} Events and {IMAGE_COUNT} Images.\n^o^ Enjoy your events. Remember to endorse!");
                    textObject.SetTextVariable("EVENT_COUNT", CEEvents.Count);
                    textObject.SetTextVariable("IMAGE_COUNT", CEEventImageList.Count);
                    InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), Colors.Magenta));
                    _isLoaded = true;
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error Initialising Captivity Events:\n\n{e.GetType()}");
                    CECustomHandler.LogMessage("Failed to load: " + e);
                    InformationManager.DisplayMessage(new InformationMessage("{=CEEVENTS1005}Error: Captivity Events failed to load events. Please refer to logs in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs. Mod is disabled.", Colors.Red));
                    _isLoaded = false;
                }
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("{=CEEVENTS1005}Error: Captivity Events failed to load events. Please refer to logs in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs. Mod is disabled.", Colors.Red));
                _isLoaded = false;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            if (!(game.GameType is Campaign) || !_isLoaded) return;
            game.GameTextManager.LoadGameTexts(BasePath.Name + "Modules/zCaptivityEvents/ModuleData/module_strings_xml.xml");
            InitalizeAttributes(game);
            var campaignStarter = (CampaignGameStarter) gameStarter;
            AddBehaviours(campaignStarter);
        }

        public override bool DoLoading(Game game)
        {
            if (Campaign.Current == null) return true;

            if (!CESettings.Instance.PrisonerEscapeBehavior) return base.DoLoading(game);
            var dailyTickHeroEvent = CampaignEvents.DailyTickHeroEvent;

            if (dailyTickHeroEvent != null)
            {
                dailyTickHeroEvent.ClearListeners(Campaign.Current.GetCampaignBehavior<PrisonerEscapeCampaignBehavior>());
                if (!CESettings.Instance.PrisonerAutoRansom) dailyTickHeroEvent.ClearListeners(Campaign.Current.GetCampaignBehavior<DiplomaticBartersBehavior>());
            }
                    IMbEvent<MobileParty> hourlyPartyTick = CampaignEvents.HourlyTickPartyEvent;
                    if (hourlyPartyTick != null)
                    {
                        hourlyPartyTick.ClearListeners(Campaign.Current.GetCampaignBehavior<PrisonerEscapeCampaignBehavior>());
                    }

            var barterablesRequested = CampaignEvents.BarterablesRequested;
            barterablesRequested?.ClearListeners(Campaign.Current.GetCampaignBehavior<SetPrisonerFreeBarterBehavior>());

            return base.DoLoading(game);
        }

        private void InitalizeAttributes(Game game)
        {
            CESkills.RegisterAll(game);
        }

        private void AddBehaviours(CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddBehavior(new CECampaignBehavior());
            if (CESettings.Instance.ProstitutionControl) campaignStarter.AddBehavior(new CEBrothelBehavior());

            if (CESettings.Instance.PrisonerEscapeBehavior)
            {
                campaignStarter.AddBehavior(new CEPrisonerEscapeCampaignBehavior());
                campaignStarter.AddBehavior(new CESetPrisonerFreeBarterBehavior());
            }

            //if (CESettings.Instance.PregnancyToggle)
            //{
            //    ReplaceModel<PregnancyModel, CEDefaultPregnancyModel>(campaignStarter);
            //}
            if (CESettings.Instance.EventCaptiveOn) ReplaceModel<PlayerCaptivityModel, CEPlayerCaptivityModel>(campaignStarter);
            if (CESettings.Instance.EventCaptorOn && CESettings.Instance.EventCaptorDialogue) CEPrisonerDialogue.AddPrisonerLines(campaignStarter);

            AddCustomEvents(campaignStarter);

            if (_isLoadedInGame) return;
            TooltipVM.AddTooltipType(typeof(CEBrothel), CEBrothelToolTip.BrothelTypeTooltipAction);
            LoadBrothelSounds();
            _isLoadedInGame = true;
        }

        protected void ReplaceModel<TBaseType, TChildType>(IGameStarter gameStarter) where TBaseType : GameModel where TChildType : GameModel
        {
            if (!(gameStarter.Models is IList<GameModel> list)) return;
            var flag = false;

            for (var i = 0; i < list.Count; i++)
                if (list[i] is TBaseType)
                {
                    flag = true;
                    if (!(list[i] is TChildType)) list[i] = Activator.CreateInstance<TChildType>();
                }

            if (!flag) gameStarter.AddModel(Activator.CreateInstance<TChildType>());
        }

        protected void ReplaceBehaviour<TBaseType, TChildType>(CampaignGameStarter gameStarter) where TBaseType : CampaignBehaviorBase where TChildType : CampaignBehaviorBase
        {
            if (!(gameStarter.CampaignBehaviors is IList<CampaignBehaviorBase> list)) return;
            var flag = false;

            for (var i = 0; i < list.Count; i++)
                if (list[i] is TBaseType)
                {
                    flag = true;
                    if (!(list[i] is TChildType)) list[i] = Activator.CreateInstance<TChildType>();
                }

            if (!flag) gameStarter.AddBehavior(Activator.CreateInstance<TChildType>());
        }

        private void AddCustomEvents(CampaignGameStarter gameStarter)
        {
            // Waiting Menu Load
            foreach (var waitingEvent in CEWaitingList) AddEvent(gameStarter, waitingEvent, CEEvents);
            // Listed Event Load
            foreach (var listedEvent in CEEventList) AddEvent(gameStarter, listedEvent, CEEvents);
        }

        private void AddEvent(CampaignGameStarter gameStarter, CEEvent listedEvent, List<CEEvent> eventList)
        {
            CECustomHandler.LogToFile("Loading Event: " + listedEvent.Name);

            try
            {
                if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Captor))
                {
                    CEEventLoader.CELoadCaptorEvent(gameStarter, listedEvent, eventList);
                }
                else if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Captive))
                {
                    CEEventLoader.CELoadCaptiveEvent(gameStarter, listedEvent, eventList);
                }
                else if (listedEvent.MultipleRestrictedListOfFlags.Contains(RestrictedListOfFlags.Random))
                {
                    CEEventLoader.CELoadRandomEvent(gameStarter, listedEvent, eventList);
                }
                else
                {
                    CECustomHandler.LogMessage("Failed to load " + listedEvent.Name + " contains no category flag (Captor, Captive, Random)");
                    var textObject = new TextObject("{=CEEVENTS1004}Failed to load event {NAME} : {ERROR} refer to logs in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs for more information");
                    textObject.SetTextVariable("NAME", listedEvent.Name);
                    textObject.SetTextVariable("TEST", "TEST");
                    InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), Colors.Red));
                }
            }
            catch (Exception e)
            {
                CECustomHandler.LogMessage("Failed to load " + listedEvent.Name + " exception: " + e.Message + " stacktrace: " + e.StackTrace);

                if (!_isLoadedInGame)
                {
                    var textObject = new TextObject("{=CEEVENTS1004}Failed to load event {NAME} : {ERROR} refer to logs in Mount & Blade II Bannerlord\\Modules\\zCaptivityEvents\\ModuleLogs for more information");
                    textObject.SetTextVariable("NAME", listedEvent.Name);
                    textObject.SetTextVariable("ERROR", e.Message);
                    InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), Colors.Red));
                }
            }
        }

        private void LoadBrothelSounds()
        {
            _brothelSounds.Add("female_01_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/female/01/stun"));
            _brothelSounds.Add("female_02_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/female/02/stun"));
            _brothelSounds.Add("female_03_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/female/03/stun"));
            _brothelSounds.Add("female_04_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/female/04/stun"));
            _brothelSounds.Add("female_05_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/female/05/stun"));

            _brothelSounds.Add("male_01_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/01/stun"));
            _brothelSounds.Add("male_02_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/02/stun"));
            _brothelSounds.Add("male_03_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/03/stun"));
            _brothelSounds.Add("male_04_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/04/stun"));
            _brothelSounds.Add("male_05_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/05/stun"));
            _brothelSounds.Add("male_06_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/06/stun"));
            _brothelSounds.Add("male_07_stun", SoundEvent.GetEventIdFromString("event:/voice/combat/male/07/stun"));
        }


        protected override void OnApplicationTick(float dt)
        {
            if (Game.Current == null || Game.Current.GameStateManager == null) return;

            // CaptiveState
            if (CaptivePlayEvent)
            {
                // Dungeon
                if (dungeonState != DungeonState.Normal && Game.Current.GameStateManager.ActiveState is MissionState missionStateDungeon && missionStateDungeon.CurrentMission.IsLoadingFinished)
                    switch (dungeonState)
                    {
                        case DungeonState.StartWalking:
                            if (CharacterObject.OneToOneConversationCharacter == null)
                            {
                                try
                                {
                                    var behaviour = Mission.Current.GetMissionBehaviour<MissionCameraFadeView>();

                                    Mission.Current.MainAgentServer.Controller = Agent.ControllerType.AI;

                                    var worldPosition = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, GameEntity.GlobalPosition, false);

                                    if (AgentTalkingTo.CanBeAssignedForScriptedMovement())
                                    {
                                        AgentTalkingTo.SetScriptedPosition(ref worldPosition, false, Agent.AIScriptedFrameFlags.DoNotRun);
                                        _dungeonFadeOut = 2f;
                                    }
                                    else
                                    {
                                        AgentTalkingTo.DisableScriptedMovement();
                                        AgentTalkingTo.HandleStopUsingAction();
                                        AgentTalkingTo.SetScriptedPosition(ref worldPosition, false, Agent.AIScriptedFrameFlags.DoNotRun);
                                        _dungeonFadeOut = 2f;
                                    }

                                    behaviour.BeginFadeOut(_dungeonFadeOut);
                                }
                                catch (Exception)
                                {
                                    CECustomHandler.LogMessage("Failed MissionCameraFadeView.");
                                }

                                _brothelTimerOne = missionStateDungeon.CurrentMission.Time + _dungeonFadeOut;
                                dungeonState = DungeonState.FadeIn;
                            }

                            break;
                        case DungeonState.FadeIn:
                            if (_brothelTimerOne < missionStateDungeon.CurrentMission.Time)
                            {
                                AgentTalkingTo.ResetAI();
                                dungeonState = DungeonState.Normal;
                                Mission.Current.EndMission();
                            }

                            break;
                        case DungeonState.Normal:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                // Party Menu -> Map State
                if (Game.Current.GameStateManager.ActiveState is PartyState) Game.Current.GameStateManager.PopState();

                // Map State -> Play Menu
                if (Game.Current.GameStateManager.ActiveState is MapState mapState)
                {
                    CaptivePlayEvent = false;

                    if (Hero.MainHero.IsFemale)
                    {
                        if (!mapState.AtMenu)
                        {
                            GameMenu.ActivateGameMenu("prisoner_wait");
                        }
                        else
                        {
                            CECampaignBehavior.ExtraProps.MenuToSwitchBackTo = mapState.GameMenuId;
                            CECampaignBehavior.ExtraProps.CurrentBackgroundMeshNameToSwitchBackTo = mapState.MenuContext.CurrentBackgroundMeshName;
                        }

                        var triggeredEvent = CaptiveToPlay.IsFemale
                            ? CEEventList.Find(item => item.Name == "CE_captor_female_sexual_menu")
                            : CEEventList.Find(item => item.Name == "CE_captor_female_sexual_menu_m");
                        triggeredEvent.Captive = CaptiveToPlay;

                        try
                        {
                            GameMenu.SwitchToMenu(triggeredEvent.Name);
                        }
                        catch (Exception)
                        {
                            if (triggeredEvent != null) CECustomHandler.LogMessage("Missing : " + triggeredEvent.Name);
                            else CECustomHandler.LogMessage("Critical Failure CE_captor_female_sexual_menu_m");
                        }

                        mapState.MenuContext.SetBackgroundMeshName(Hero.MainHero.IsFemale
                                                                       ? "wait_prisoner_female"
                                                                       : "wait_prisoner_male");
                    }
                    else
                    {
                        if (!mapState.AtMenu)
                        {
                            GameMenu.ActivateGameMenu("prisoner_wait");
                        }
                        else
                        {
                            CECampaignBehavior.ExtraProps.MenuToSwitchBackTo = mapState.GameMenuId;
                            CECampaignBehavior.ExtraProps.CurrentBackgroundMeshNameToSwitchBackTo = mapState.MenuContext.CurrentBackgroundMeshName;
                        }

                        var triggeredEvent = CaptiveToPlay.IsFemale
                            ? CEEventList.Find(item => item.Name == "CE_captor_male_sexual_menu")
                            : CEEventList.Find(item => item.Name == "CE_captor_male_sexual_menu_m");
                        triggeredEvent.Captive = CaptiveToPlay;

                        try
                        {
                            GameMenu.SwitchToMenu(triggeredEvent.Name);
                        }
                        catch (Exception)
                        {
                            if (triggeredEvent != null) CECustomHandler.LogMessage("Missing : " + triggeredEvent.Name);
                            else CECustomHandler.LogMessage("Critical Failure CE_captor_male_sexual_menu_m");
                        }

                        mapState.MenuContext.SetBackgroundMeshName(Hero.MainHero.IsFemale
                                                                       ? "wait_prisoner_female"
                                                                       : "wait_prisoner_male");
                    }

                    CaptiveToPlay = null;
                }
            }

            // Animated Background Menus
            if (AnimationPlayEvent && Game.Current.GameStateManager.ActiveState is MapState)
                try
                {
                    if (Game.Current.ApplicationTime > _lastCheck)
                    {
                        if (AnimationIndex > AnimationImageList.Count() - 1) AnimationIndex = 0;

                        LoadTexture(AnimationImageList[AnimationIndex]);
                        AnimationIndex++;

                        _lastCheck = Game.Current.ApplicationTime + AnimationSpeed;
                    }
                }
                catch (Exception)
                {
                    AnimationPlayEvent = false;
                }

            // Brothel Event To Play
            if (brothelState != BrothelState.Normal && Game.Current.GameStateManager.ActiveState is MissionState missionStateBrothel && missionStateBrothel.CurrentMission.IsLoadingFinished)
                switch (brothelState)
                {
                    case BrothelState.Start:
                        if (CharacterObject.OneToOneConversationCharacter == null)
                        {
                            try
                            {
                                var behaviour = Mission.Current.GetMissionBehaviour<MissionCameraFadeView>();

                                Mission.Current.MainAgentServer.Controller = Agent.ControllerType.AI;

                                var worldPosition = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, GameEntity.GlobalPosition, false);

                                if (AgentTalkingTo.CanBeAssignedForScriptedMovement())
                                {
                                    AgentTalkingTo.SetScriptedPosition(ref worldPosition, true, Agent.AIScriptedFrameFlags.DoNotRun);
                                    Mission.Current.MainAgent.SetScriptedPosition(ref worldPosition, true, Agent.AIScriptedFrameFlags.DoNotRun);
                                    _brothelFadeIn = 3f;
                                }
                                else
                                {
                                    AgentTalkingTo.DisableScriptedMovement();
                                    AgentTalkingTo.HandleStopUsingAction();
                                    AgentTalkingTo.SetScriptedPosition(ref worldPosition, false, Agent.AIScriptedFrameFlags.DoNotRun);
                                    _brothelFadeIn = 3f;
                                }

                                behaviour.BeginFadeOutAndIn(_brothelFadeIn, BrothelBlack, BrothelFadeOut);
                            }
                            catch (Exception)
                            {
                                CECustomHandler.LogMessage("Failed MissionCameraFadeView.");
                            }

                            _brothelTimerOne = missionStateBrothel.CurrentMission.Time + _brothelFadeIn;
                            brothelState = BrothelState.FadeIn;
                        }

                        break;

                    case BrothelState.FadeIn:
                        if (_brothelTimerOne < missionStateBrothel.CurrentMission.Time)
                        {
                            _brothelTimerOne = missionStateBrothel.CurrentMission.Time + BrothelBlack;
                            _brothelTimerTwo = missionStateBrothel.CurrentMission.Time + MBRandom.RandomFloatRanged(BrothelSoundMin, BrothelSoundMax);
                            _brothelTimerThree = missionStateBrothel.CurrentMission.Time + MBRandom.RandomFloatRanged(BrothelSoundMin, BrothelSoundMax);

                            Hero.MainHero.HitPoints += 10;

                            AgentTalkingTo.ResetAI();
                            Mission.Current.MainAgent.TeleportToPosition(GameEntity.GlobalPosition);
                            brothelState = BrothelState.Black;
                        }

                        break;

                    case BrothelState.Black:
                        if (_brothelTimerOne < missionStateBrothel.CurrentMission.Time)
                        {
                            Mission.Current.MainAgentServer.Controller = Agent.ControllerType.Player;

                            _brothelTimerOne = missionStateBrothel.CurrentMission.Time + BrothelFadeOut;
                            brothelState = BrothelState.FadeOut;
                        }
                        else if (_brothelTimerTwo < missionStateBrothel.CurrentMission.Time)
                        {
                            _brothelTimerTwo = missionStateBrothel.CurrentMission.Time + MBRandom.RandomFloatRanged(BrothelSoundMin, BrothelSoundMax);

                            try
                            {
                                var soundNum = _brothelSounds.Where(sound => sound.Key.StartsWith(Agent.Main.GetAgentVoiceDefinition())).GetRandomElement().Value;
                                Mission.Current.MakeSound(soundNum, Agent.Main.Frame.origin, true, false, -1, -1);
                            }
                            catch (Exception) { }
                        }
                        else if (_brothelTimerThree < missionStateBrothel.CurrentMission.Time)
                        {
                            _brothelTimerThree = missionStateBrothel.CurrentMission.Time + MBRandom.RandomFloatRanged(BrothelSoundMin, BrothelSoundMax);

                            try
                            {
                                var soundNum = _brothelSounds.Where(sound => sound.Key.StartsWith(AgentTalkingTo.GetAgentVoiceDefinition())).GetRandomElement().Value;
                                Mission.Current.MakeSound(soundNum, Agent.Main.Frame.origin, true, false, -1, -1);
                            }
                            catch (Exception) { }
                        }

                        break;

                    case BrothelState.FadeOut:
                        if (_brothelTimerOne < missionStateBrothel.CurrentMission.Time)
                        {
                            AgentTalkingTo = null;
                            brothelState = BrothelState.Normal;
                        }

                        break;
                    case BrothelState.Normal:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            // Hunt Event To Play
            if (huntState == HuntState.Normal) return;

            // Hunt Event States
            if ((huntState == HuntState.StartHunt || huntState == HuntState.HeadStart) && Game.Current.GameStateManager.ActiveState is MissionState missionState && missionState.CurrentMission.IsLoadingFinished)
            {
                try
                {
                    switch (huntState)
                    {
                        case HuntState.StartHunt:
                            if (Mission.Current != null && Mission.Current.IsLoadingFinished && Mission.Current.Time > 2f && Mission.Current.Agents != null)
                            {
                                foreach (var agent2 in from agent in Mission.Current.Agents
                                                       where agent.IsHuman && agent.IsEnemyOf(Agent.Main)
                                                       select agent) ForceAgentDropEquipment(agent2);
                                missionState.CurrentMission.ClearCorpses();

                                InformationManager.AddQuickInformation(new TextObject("{=CEEVENTS1069}Let's give them a headstart."), 100, CharacterObject.PlayerCharacter);
                                huntState = HuntState.HeadStart;
                            }

                            break;

                        case HuntState.HeadStart:
                            if (Mission.Current != null && Mission.Current.Time > CESettings.Instance.HuntBegins && Mission.Current.Agents != null)
                            {
                                foreach (var agent2 in from agent in Mission.Current.Agents
                                                       where agent.IsHuman && agent.IsEnemyOf(Agent.Main)
                                                       select agent)
                                {
                                    var component = agent2.GetComponent<MoraleAgentComponent>();
                                    component?.Panic();
                                    agent2.DestinationSpeed = 0.5f;
                                }

                                InformationManager.AddQuickInformation(new TextObject("{=CEEVENTS1068}Hunt them down!"), 100, CharacterObject.PlayerCharacter, CharacterObject.PlayerCharacter.IsFemale
                                                                           ? "event:/voice/combat/female/01/victory"
                                                                           : "event:/voice/combat/male/01/victory");
                                huntState = HuntState.Hunting;
                            }

                            break;
                        case HuntState.Normal:
                            break;
                        case HuntState.Hunting:
                            break;
                        case HuntState.AfterBattle:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    CECustomHandler.LogMessage("Failed on hunting mission: " + e);
                    huntState = HuntState.Hunting;
                }
            }
            else if ((huntState == HuntState.HeadStart || huntState == HuntState.Hunting) && Game.Current.GameStateManager.ActiveState is MapState mapstate)
            {
                huntState = HuntState.AfterBattle;
                PlayerEncounter.SetPlayerVictorious();
                if (CESettings.Instance.HuntLetPrisonersEscape) PlayerEncounter.EnemySurrender = true;
                PlayerEncounter.Update();
            }
            else if (huntState == HuntState.AfterBattle && Game.Current.GameStateManager.ActiveState is MapState mapstate2 && !mapstate2.IsMenuState)
            {
                if (PlayerEncounter.Current == null)
                {
                    LoadingWindow.DisableGlobalLoadingWindow();
                    huntState = HuntState.Normal;
                }
                else
                {
                    PlayerEncounter.Update();
                }
            }
        }

        private void ForceAgentDropEquipment(Agent agent)
        {
            try
            {
                agent.RemoveEquippedWeapon(EquipmentIndex.Weapon0);
                agent.RemoveEquippedWeapon(EquipmentIndex.Weapon1);
                agent.RemoveEquippedWeapon(EquipmentIndex.Weapon2);
                agent.RemoveEquippedWeapon(EquipmentIndex.Weapon3);
                agent.RemoveEquippedWeapon(EquipmentIndex.Weapon4);
                if (agent.HasMount) agent.MountAgent.Die(new Blow(), Agent.KillInfo.Musket);
            }
            catch (Exception) { }
        }
    }
}