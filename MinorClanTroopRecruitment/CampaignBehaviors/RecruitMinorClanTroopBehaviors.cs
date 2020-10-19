﻿using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.SandBox;
using TaleWorlds.CampaignSystem.GameMenus;
using System.Collections.Generic;

namespace MinorClanTroopRecruitment
{
	internal class RecruitMinorClanTroopBehaviors : CampaignBehaviorBase
    {
		public override void SyncData(IDataStore dataStore) { }

		public MinorClanMercDataHolder mc_merc_data = null;

		public override void RegisterEvents()
		{
			CampaignEvents.OnNewGameCreatedEvent8.AddNonSerializedListener(this, new Action(this.OnAfterNewGameCreated));
			CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnSettlementEntered));
			CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
			if (Settings.Settings.Instance.UpdateTiming.SelectedValue == "Weekly")
			{
				CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, new Action(this.WeeklyTickTown));
			}
			else
			{
				CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, new Action<Town>(this.DailyTickTown));
			}
		}

		// Only triggers on loaded games
		private void OnGameLoaded(CampaignGameStarter campaignGameStarter)
		{
			if (mc_merc_data == null)
			{
				MinorClanMercDataHolder clanMercData = new MinorClanMercDataHolder();
				this.mc_merc_data = clanMercData;
				foreach (Town town in Town.AllTowns)
				{
					this.UpdateCurrentMercenaryTroopAndCount(town);
				}
			}
			// Add Character if inside of town
			if (Settlement.CurrentSettlement != null && !Hero.MainHero.IsPrisoner)
			{
				this.AddMinorClanMercenaryCharacterToTavern(Settlement.CurrentSettlement);
			}
		}

		// Only triggers on new campaigns created
		public void OnAfterNewGameCreated()
		{
			if (mc_merc_data == null)
			{
				MinorClanMercDataHolder clanMercData = new MinorClanMercDataHolder();
				this.mc_merc_data = clanMercData;
				foreach (Town town in Town.AllTowns)
				{
					this.UpdateCurrentMercenaryTroopAndCount(town);
				}
			}
		}

		public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
		{
			if (mobileParty != MobileParty.MainParty)
			{
				return;
			}
			this.AddMinorClanMercenaryCharacterToTavern(settlement);
		}

		// Adding Character to the Tavern
		private void AddMinorClanMercenaryCharacterToTavern(Settlement settlement)
		{

			if (settlement.LocationComplex != null && settlement.IsTown && mc_merc_data.dictionaryOfMercAtTownData[settlement.Town].HasAvailableMercenary(Occupation.NotAssigned))
			{
				
				Location locationWithId = Settlement.CurrentSettlement.LocationComplex.GetLocationWithId("tavern");
				if (locationWithId != null)
				{
					locationWithId.AddLocationCharacters(new CreateLocationCharacterDelegate(this.CreateMinorClanMercenary), settlement.Culture, LocationCharacter.CharacterRelations.Neutral, 1);
				}
			}
		}

		private LocationCharacter CreateMinorClanMercenary(CultureObject culture, LocationCharacter.CharacterRelations relation)
		{
			Settlement currentSettlement = MobileParty.MainParty.CurrentSettlement;
			// return new LocationCharacter(new AgentData(new SimpleAgentOrigin(mc_merc_data.dictionaryOfMercAtTownData[currentSettlement.Town].TroopInfoCharObject(), -1, null, default(UniqueTroopDescriptor))).Monster(Campaign.Current.HumanMonsterSettlement).NoHorses(true), new LocationCharacter.AddBehaviorsDelegate(SandBoxManager.Instance.AgentBehaviorManager.AddOutdoorWandererBehaviors), "spawnpoint_mercenary", true, relation, null, false, false, null, false, false, true);
			return new LocationCharacter(new AgentData(new SimpleAgentOrigin(mc_merc_data.dictionaryOfMercAtTownData[currentSettlement.Town].TroopInfoCharObject(), -1, null, default(UniqueTroopDescriptor))).Monster(Campaign.Current.HumanMonsterSettlement).NoHorses(true), new LocationCharacter.AddBehaviorsDelegate(SandBoxManager.Instance.AgentBehaviorManager.AddOutdoorWandererBehaviors), "npc_common", true, relation, null, false, false, null, false, false, true);
		}

		private void CheckIfMinorClanMercenaryCharacterNeedsToRefresh(Settlement settlement, CharacterObject oldTroopType)
		{
			if (settlement.IsTown && settlement == Settlement.CurrentSettlement && PlayerEncounter.LocationEncounter != null && settlement.LocationComplex != null && (CampaignMission.Current == null || GameStateManager.Current.ActiveState != CampaignMission.Current.State))
			{
				Settlement.CurrentSettlement.LocationComplex.GetLocationWithId("tavern").RemoveAllCharacters((LocationCharacter x) => (x.Character.Occupation == oldTroopType.Occupation && x.Character.Name == oldTroopType.Name));
				this.AddMinorClanMercenaryCharacterToTavern(settlement);
			}
		}

		// Update minorMerc troops
		private void DailyTickTown(Town town)
		{
			this.UpdateCurrentMercenaryTroopAndCount(town);
		}

		private void WeeklyTickTown()
		{
			foreach (Town town in Town.AllTowns)
			{
				this.UpdateCurrentMercenaryTroopAndCount(town);
			}
		}

		private static int FindNumberOfMercenariesWillBeAdded()
		{
			float troopMultipler = Settings.Settings.Instance.TroopMultiplier;
			int minNumberOfTroops = Settings.Settings.Instance.MinNumberOfTroops;
			int maxNumberOfTroops = Settings.Settings.Instance.MaxNumberOfTroops + 1; // if set at 15 will never get 15 need this + 1
			float numOfMercs = MBRandom.RandomInt(minNumberOfTroops, maxNumberOfTroops);
			numOfMercs *= troopMultipler;
			return MBRandom.RoundRandomized(numOfMercs);
		}

		private void UpdateCurrentMercenaryTroopAndCount(Town town)
		{
			CharacterObject oldTroopType = mc_merc_data.dictionaryOfMercAtTownData[town].TroopInfoCharObject();
			List<TroopInfoStruct> possibleMercTroops = mc_merc_data.dictionaryOfMercAtTownData[town].PossibleMercTroopInfo;
			if (possibleMercTroops.Count == 0)
			{
				return;
			}
			int r = MBRandom.Random.Next(possibleMercTroops.Count);
			TroopInfoStruct newTroopStruct = possibleMercTroops[r];
			int numbOfUnits = FindNumberOfMercenariesWillBeAdded();
			if (MBRandom.RandomFloat > Settings.Settings.Instance.PossibilityOfSpawn)
			{
				numbOfUnits = 0;
			}
			mc_merc_data.dictionaryOfMercAtTownData[town].ChangeMercenaryType(newTroopStruct, numbOfUnits);

			// Since we don't have access to MercenaryNUmberChangedInTown or MercenaryTroopChangedInTown
			// need way to trigger spawn of hire guy in tavern when inside of town on a daily update
			if (oldTroopType != null)
			{
				CheckIfMinorClanMercenaryCharacterNeedsToRefresh(town.Settlement, oldTroopType);
			}
		}

		// start of the dialog and game Menu code flows
		public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this.AddDialogs(campaignGameStarter);
			this.AddGameMenus(campaignGameStarter);
		}

		private MinorClanMercData getMinorMercDataOfPlayerEncounter()
		{
			return mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town];
		}

		private int troopRecruitmentCost(MinorClanMercData mercData)
		{
			float recruitCostMultiplier = Settings.Settings.Instance.RecruitCostMultiplier;
			int baseCost = mercData.GetRecruitmentCost();
			return MBRandom.RoundRandomized(baseCost * recruitCostMultiplier);
		}


		// TAVERN CODE
		protected void AddDialogs(CampaignGameStarter campaignGameStarter)
		{
			// priority = higher takes presidence if equal first one added if condition isn't met will pass to next priority
			// priority also makes it so that if two prompts are present on that the higher one is higher on the list
			// not that start is start token for all converstaions and it goes down the priority to see the first start that returns true for the ConversationSentence.OnConditionDelegate 
			campaignGameStarter.AddDialogLine("minor_clan_recruit_talk_start_plural", "start", "minor_clan_mercenary_tavern_talk", "Do you have a need for fighters, {?PLAYER.GENDER}madam{?}sir{\\?}? Me and {?MCMERCS_PLURAL}{MCMERCS_MERCENARY_COUNT} of my mates{?}one of my mates{\\?} looking for a master. You might call us mercenaries, like. We'll join you for {MCMERCS_GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_plural_start_on_condition), null, 150, null);
			campaignGameStarter.AddDialogLine("minor_clan_recruit_talk_start_singlular", "start", "minor_clan_mercenary_tavern_talk", "Do you have a need for fighters, {?PLAYER.GENDER}madam{?}sir{\\?}? I am looking for a master. I'll join you for {MCMERCS_GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_single_start_on_condition), null, 150, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_hire_one", "minor_clan_mercenary_tavern_talk", "minor_clan_mercenary_tavern_talk_hire_one", "All right. I would only like to hire one of you. Here is {MCMERCS_GOLD_AMOUNT_FOR_ONE}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_one), new ConversationSentence.OnConsequenceDelegate(this.conversation_minor_clan_mercenary_recruit_one_on_consequence), 110, null, null);
			campaignGameStarter.AddDialogLine("minor_clan_recruit_talk_hire_one_response", "minor_clan_mercenary_tavern_talk_hire_one", "minor_clan_mercenary_tavern_talk", "Deal, One of us will report to your party outside the gates after gathering their gear. Need anything else?", null, null, 100, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_hire_all", "minor_clan_mercenary_tavern_talk", "minor_clan_mercenary_tavern_talk_hire", "All right. I will hire {?MCMERCS_PLURAL}all of you{?}you{\\?}. Here is {MCMERCS_GOLD_AMOUNT_ALL}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_accept_all_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_minor_clan_mercenary_recruit_accept_all_on_consequence), 100, null, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_hire_all_past_limit", "minor_clan_mercenary_tavern_talk", "minor_clan_mercenary_tavern_talk_hire", "All right. I will hire {?MCMERCS_PLURAL}all of you{?}you{\\?}. Here is {MCMERCS_GOLD_AMOUNT_ALL}{GOLD_ICON} (Hires Past Party Limit)", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_accept_all_on_condition_past_limit), new ConversationSentence.OnConsequenceDelegate(this.conversation_minor_clan_mercenary_recruit_accept_all_on_consequence), 110, null, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_hire_some_past_limit", "minor_clan_mercenary_tavern_talk", "minor_clan_mercenary_tavern_talk_hire", "All right. But I can only hire {MCMERCS_MERCENARY_COUNT_SOME_AFFORD} of you. Here is {MCMERCS_GOLD_AMOUNT_SOME_AFFORD}{GOLD_ICON} (Hires Past Party Limit)", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_accept_some_on_condition_past_limit_afford), new ConversationSentence.OnConsequenceDelegate(this.conversation_minor_clan_mercenary_recruit_accept_some_past_limit_on_consequence), 110, null, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_hire_some", "minor_clan_mercenary_tavern_talk", "minor_clan_mercenary_tavern_talk_hire", "All right. But I can only hire {MCMERCS_MERCENARY_COUNT_SOME} of you. Here is {MCMERCS_GOLD_AMOUNT_SOME}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_accept_some_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_minor_clan_mercenary_recruit_accept_some_on_consequence), 100, null, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_reject_no_gold", "minor_clan_mercenary_tavern_talk", "close_window", "That sounds good. But I can't hire any more men right now.", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_reject_gold_or_party_size_on_condition), null, 100, null, null);
			campaignGameStarter.AddPlayerLine("minor_clan_recruit_talk_reject_party_full", "minor_clan_mercenary_tavern_talk", "close_window", "Sorry. I don't need any other men right now.", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_dont_need_men_on_condition), null, 100, null, null);
			campaignGameStarter.AddDialogLine("minor_clan_recruit_talk_hired_end", "minor_clan_mercenary_tavern_talk_hire", "close_window", "{RANDOM_HIRE_SENTENCE}", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruit_end_on_condition), null, 100, null);
			campaignGameStarter.AddDialogLine("minor_clan_recruit_talk_start_post_hire", "start", "close_window", "Don't worry, I'll be ready. Just having a last drink for the road.", new ConversationSentence.OnConditionDelegate(this.conversation_minor_clan_mercenary_recruited_on_condition), null, 150, null);
		}

		private bool minorClanMercGuardIsInTavern(MinorClanMercData minorMercData)
		{
			if (CampaignMission.Current == null || CampaignMission.Current.Location == null || minorMercData.TroopInfo == null || minorMercData.TroopInfoCharObject() == null)
			{
				return false;
			}
			return CampaignMission.Current.Location.StringId == "tavern" && minorMercData.TroopInfoCharObject().Name == CharacterObject.OneToOneConversationCharacter.Name && CharacterObject.OneToOneConversationCharacter.IsSoldier;
		}

		// Conditions for starting line dialog
		private bool conversation_minor_clan_mercenary_recruit_plural_start_on_condition()
		{
			if(MobileParty.MainParty.CurrentSettlement == null || !MobileParty.MainParty.CurrentSettlement.IsTown)
			{
				return false;
			}
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			bool flag = minorMercData.Number > 1 && minorClanMercGuardIsInTavern(minorMercData);
			if (flag)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				MBTextManager.SetTextVariable("MCMERCS_PLURAL", (minorMercData.Number > 1) ? 1 : 0);
				MBTextManager.SetTextVariable("MCMERCS_MERCENARY_COUNT", minorMercData.Number - 1);
				MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT", troopRecruitmentCost * minorMercData.Number);
			}
			return flag;
		}

		private bool conversation_minor_clan_mercenary_recruit_single_start_on_condition()
		{
			if (MobileParty.MainParty.CurrentSettlement == null || !MobileParty.MainParty.CurrentSettlement.IsTown)
			{
				return false;
			}
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			bool flag = minorMercData.Number == 1 && minorClanMercGuardIsInTavern(minorMercData);
			if (flag)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT", minorMercData.Number * troopRecruitmentCost);
			}
			return flag;
		}

		private bool conversation_minor_clan_mercenary_recruited_on_condition()
		{
			if (MobileParty.MainParty.CurrentSettlement == null || !MobileParty.MainParty.CurrentSettlement.IsTown)
			{
				return false;
			}
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			return minorClanMercGuardIsInTavern(minorMercData);
		}

		// Conditions for Hiring options and functions that follow
		private bool conversation_minor_clan_mercenary_recruit_one()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopPlayerCanBuy = Hero.MainHero.Gold / troopRecruitmentCost;
			MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT_FOR_ONE", troopRecruitmentCost);
			return 1 < minorMercData.Number && numOfTroopPlayerCanBuy > 1;
		}

		private void conversation_minor_clan_mercenary_recruit_one_on_consequence()
		{
			this.BuyMinorClanMercenariesInTavern(1);
		}

		private bool conversation_minor_clan_mercenary_recruit_accept_all_on_condition()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			MBTextManager.SetTextVariable("MCMERCS_PLURAL", (minorMercData.Number > 1) ? 1 : 0);
			MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT_ALL", troopRecruitmentCost * minorMercData.Number);
			return Hero.MainHero.Gold >= minorMercData.Number * troopRecruitmentCost && numOfTroopSlotsOpen >= minorMercData.Number;
		}

		private bool conversation_minor_clan_mercenary_recruit_accept_all_on_condition_past_limit()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopPlayerCanBuy = (troopRecruitmentCost==0) ? minorMercData.Number : Hero.MainHero.Gold / troopRecruitmentCost;
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			MBTextManager.SetTextVariable("MCMERCS_PLURAL", (minorMercData.Number > 1) ? 1 : 0);
			MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT_ALL", troopRecruitmentCost * numOfTroopPlayerCanBuy);
			return numOfTroopSlotsOpen < minorMercData.Number && numOfTroopPlayerCanBuy >= minorMercData.Number;
		}

		private void conversation_minor_clan_mercenary_recruit_accept_all_on_consequence()
		{
			this.BuyMinorClanMercenariesInTavern(getMinorMercDataOfPlayerEncounter().Number);
		}

		private bool conversation_minor_clan_mercenary_recruit_accept_some_on_condition()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			if (Hero.MainHero.Gold >= troopRecruitmentCost && numOfTroopSlotsOpen > 0 && (Hero.MainHero.Gold < minorMercData.Number * troopRecruitmentCost || numOfTroopSlotsOpen < minorMercData.Number))
			{
				int numberToHire = 0;
				while (Hero.MainHero.Gold > troopRecruitmentCost * (numberToHire + 1) && numOfTroopSlotsOpen > numberToHire)
				{
					numberToHire++;
				}
				MBTextManager.SetTextVariable("MCMERCS_MERCENARY_COUNT_SOME", numberToHire);
				MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT_SOME", troopRecruitmentCost * numberToHire);
				return true;
			}
			return false;
		}

		private void conversation_minor_clan_mercenary_recruit_accept_some_on_consequence()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			int numberToHire = 0;
			while (Hero.MainHero.Gold > troopRecruitmentCost * (numberToHire + 1) && numOfTroopSlotsOpen > numberToHire)
			{
				numberToHire++;
			}
			this.BuyMinorClanMercenariesInTavern(numberToHire);
		}

		private bool conversation_minor_clan_mercenary_recruit_accept_some_on_condition_past_limit_afford()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			if (Hero.MainHero.Gold >= troopRecruitmentCost && Hero.MainHero.Gold < minorMercData.Number * troopRecruitmentCost)
			{
				int numberToHire = 0;
				while (Hero.MainHero.Gold > troopRecruitmentCost * (numberToHire + 1) && minorMercData.Number > numberToHire)
				{
					numberToHire++;
				}
				if (numberToHire <= numOfTroopSlotsOpen)
				{
					return false;
				}
				MBTextManager.SetTextVariable("MCMERCS_MERCENARY_COUNT_SOME_AFFORD", numberToHire);
				MBTextManager.SetTextVariable("MCMERCS_GOLD_AMOUNT_SOME_AFFORD", troopRecruitmentCost * numberToHire);
				return true;
			}
			return false;
		}

		private void conversation_minor_clan_mercenary_recruit_accept_some_past_limit_on_consequence()
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			int numberToHire = 0;
			while (Hero.MainHero.Gold > troopRecruitmentCost * (numberToHire + 1) && minorMercData.Number > numberToHire)
			{
				numberToHire++;
			}
			this.BuyMinorClanMercenariesInTavern(numberToHire);
		}

		private void BuyMinorClanMercenariesInTavern(int numberOfMercsToHire)
		{
			MinorClanMercData minorMercData = getMinorMercDataOfPlayerEncounter();
			minorMercData.ChangeMercenaryCount(-numberOfMercsToHire);
			int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
			MobileParty.MainParty.AddElementToMemberRoster(minorMercData.TroopInfoCharObject(), numberOfMercsToHire, false);
			int amount = numberOfMercsToHire * troopRecruitmentCost;
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, false);
			CampaignEventDispatcher.Instance.OnUnitRecruited(minorMercData.TroopInfoCharObject(), numberOfMercsToHire);
		}

		// Conditions to trigger reject hiring options
		private bool conversation_minor_clan_mercenary_recruit_reject_gold_or_party_size_on_condition()
		{
			int troopRecruitmentCost = this.troopRecruitmentCost(getMinorMercDataOfPlayerEncounter());
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			return Hero.MainHero.Gold < troopRecruitmentCost || numOfTroopSlotsOpen <= 0;
		}

		private bool conversation_minor_clan_mercenary_recruit_dont_need_men_on_condition()
		{
			int troopRecruitmentCost = this.troopRecruitmentCost(getMinorMercDataOfPlayerEncounter());
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			return Hero.MainHero.Gold >= troopRecruitmentCost && numOfTroopSlotsOpen > 0;
		}

		// Successful hire npc phrase
		public bool conversation_minor_clan_mercenary_recruit_end_on_condition()
		{
			MBTextManager.SetTextVariable("RANDOM_HIRE_SENTENCE", GameTexts.FindText("str_mercenary_tavern_talk_hire", MBRandom.RandomInt(4).ToString()));
			return true;
		}

		// GAME MENU CODE
		//Interaction of the Tavern from the Game Menu tested to work on 1.4.1
		// these variables have these names otherwise if say MEN_COUNT would override the 1.4.1 Game Menu for normal mercs
		public void AddGameMenus(CampaignGameStarter campaignGameStarter)
		{
			// index is location in menu 0 being top, 1 next if other of same index exist this are placed on top of them
			campaignGameStarter.AddGameMenuOption("town_backstreet", "recruit_minor_clan_mercenaries_all", "{=*}Recruit {MC_MEN_COUNT} {MC_MERCENARY_NAME} ({MC_TOTAL_AMOUNT}{GOLD_ICON})", new GameMenuOption.OnConditionDelegate(this.BuyMinorClanMercsViaMenuCondition), delegate (MenuCallbackArgs x)
			{
				BuyMinorClanMercenariesViaGameMenu(mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town]);
			}, false, 1, false);
			campaignGameStarter.AddGameMenuOption("town_backstreet", "recruit_minor_clan_mercenaries_party_limit", "{=*}Recruit to Party Limit {MC_MEN_COUNT_PL} {MC_MERCENARY_NAME_PL} ({MC_TOTAL_AMOUNT_PL}{GOLD_ICON})", new GameMenuOption.OnConditionDelegate(this.BuyMinorClanMercsViaMenuConditionToPartyLimit), delegate (MenuCallbackArgs x)
			{
				BuyMinorClanMercenariesViaGameMenuToPartyLimit(mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town]);
			}, false, 1, false);
			campaignGameStarter.AddGameMenuOption("town_backstreet", "recruit_minor_clan_mercenaries_hire_one", "{=*}Recruit 1 {MC_MERCENARY_NAME_ONLY_ONE} ({MC_TOTAL_AMOUNT_ONLY_ONE}{GOLD_ICON})", new GameMenuOption.OnConditionDelegate(this.BuyMinorClanMercsViaMenuConditionHireOne), delegate (MenuCallbackArgs x)
			{
				BuyMinorClanMercenariesViaGameMenuHireOne(mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town]);
			}, false, 1, false);
		}

		private bool BuyMinorClanMercsViaMenuConditionHireOne(MenuCallbackArgs args)
		{
			MinorClanMercData minorMercData = mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town];
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 1)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				int numOfTroopPlayerCanBuy = Hero.MainHero.Gold / troopRecruitmentCost;
				if (numOfTroopPlayerCanBuy > 1)
				{
					MBTextManager.SetTextVariable("MC_MERCENARY_NAME_ONLY_ONE", minorMercData.TroopInfoCharObject().Name);
					MBTextManager.SetTextVariable("MC_TOTAL_AMOUNT_ONLY_ONE", troopRecruitmentCost);
					args.optionLeaveType = GameMenuOption.LeaveType.RansomAndBribe;
					return true;
				}
			}
			return false;
		}

		private void BuyMinorClanMercenariesViaGameMenuHireOne(MinorClanMercData minorMercData)
		{
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 1)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				if (Hero.MainHero.Gold >= troopRecruitmentCost)
				{
					int numOfMercs = 1;
					MobileParty.MainParty.MemberRoster.AddToCounts(minorMercData.TroopInfoCharObject(), numOfMercs, false, 0, 0, true, -1);
					GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, -(numOfMercs * troopRecruitmentCost), false);
					minorMercData.ChangeMercenaryCount(-numOfMercs);
					GameMenu.SwitchToMenu("town_backstreet");
				}
			}
		}

		private bool BuyMinorClanMercsViaMenuCondition(MenuCallbackArgs args)
		{
			MinorClanMercData minorMercData = mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town];
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 0)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				if (Hero.MainHero.Gold >= troopRecruitmentCost)
				{
					int numOfTroopPlayerCanBuy = (troopRecruitmentCost == 0) ? minorMercData.Number : Hero.MainHero.Gold / troopRecruitmentCost;
					int num = Math.Min(minorMercData.Number, numOfTroopPlayerCanBuy);
					MBTextManager.SetTextVariable("MC_MEN_COUNT", num);
					MBTextManager.SetTextVariable("MC_MERCENARY_NAME", minorMercData.TroopInfoCharObject().Name);
					MBTextManager.SetTextVariable("MC_TOTAL_AMOUNT", num * troopRecruitmentCost);
					args.optionLeaveType = GameMenuOption.LeaveType.RansomAndBribe;
					return true;
				}
			}
			return false;
		}

		private void BuyMinorClanMercenariesViaGameMenu(MinorClanMercData minorMercData)
		{
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 0)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				if (Hero.MainHero.Gold >= troopRecruitmentCost)
				{
					int numOfTroopPlayerCanBuy = (troopRecruitmentCost == 0) ? minorMercData.Number : Hero.MainHero.Gold / troopRecruitmentCost;
					int numOfMercs = Math.Min(minorMercData.Number, numOfTroopPlayerCanBuy);
					MobileParty.MainParty.MemberRoster.AddToCounts(minorMercData.TroopInfoCharObject(), numOfMercs, false, 0, 0, true, -1);
					GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, -(numOfMercs * troopRecruitmentCost), false);
					minorMercData.ChangeMercenaryCount(-numOfMercs);
					GameMenu.SwitchToMenu("town_backstreet");
				}
			}
		}
		private bool BuyMinorClanMercsViaMenuConditionToPartyLimit(MenuCallbackArgs args)
		{
			MinorClanMercData minorMercData = mc_merc_data.dictionaryOfMercAtTownData[MobileParty.MainParty.CurrentSettlement.Town];
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 0)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
				int numOfTroopPlayerCanBuy = (troopRecruitmentCost == 0) ? minorMercData.Number : Hero.MainHero.Gold / troopRecruitmentCost;
				if (numOfTroopSlotsOpen > 0 && Hero.MainHero.Gold >= troopRecruitmentCost && numOfTroopSlotsOpen < minorMercData.Number && numOfTroopSlotsOpen < numOfTroopPlayerCanBuy)
				{
					int numOfMercs = Math.Min(minorMercData.Number, numOfTroopPlayerCanBuy);
					numOfMercs = Math.Min(numOfTroopSlotsOpen, numOfMercs);
					MBTextManager.SetTextVariable("MC_MEN_COUNT_PL", numOfMercs);
					MBTextManager.SetTextVariable("MC_MERCENARY_NAME_PL", minorMercData.TroopInfoCharObject().Name);
					MBTextManager.SetTextVariable("MC_TOTAL_AMOUNT_PL", numOfMercs * troopRecruitmentCost);
					args.optionLeaveType = GameMenuOption.LeaveType.RansomAndBribe;
					return true;
				}
			}
			return false;
		}

		private void BuyMinorClanMercenariesViaGameMenuToPartyLimit(MinorClanMercData minorMercData)
		{
			int numOfTroopSlotsOpen = PartyBase.MainParty.PartySizeLimit - PartyBase.MainParty.NumberOfAllMembers;
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && minorMercData != null && minorMercData.Number > 0 && numOfTroopSlotsOpen > 0)
			{
				int troopRecruitmentCost = this.troopRecruitmentCost(minorMercData);
				if (Hero.MainHero.Gold >= troopRecruitmentCost)
				{
					int numOfTroopPlayerCanBuy = (troopRecruitmentCost == 0) ? minorMercData.Number : Hero.MainHero.Gold / troopRecruitmentCost;
					int numOfMercs = Math.Min(minorMercData.Number, numOfTroopPlayerCanBuy);
					numOfMercs = Math.Min(numOfTroopSlotsOpen, numOfMercs);
					MobileParty.MainParty.MemberRoster.AddToCounts(minorMercData.TroopInfoCharObject(), numOfMercs, false, 0, 0, true, -1);
					GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, -(numOfMercs * troopRecruitmentCost), false);
					minorMercData.ChangeMercenaryCount(-numOfMercs);
					GameMenu.SwitchToMenu("town_backstreet");
				}
			}
		}
	}
}
