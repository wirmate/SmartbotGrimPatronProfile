using System.Linq;
using System.Collections.Generic;

namespace SmartBot.Plugins.API
{    
	public class bProfile : RemoteProfile
	{
        private const int CARDS_IN_HAND_THRESHHOLD = 5; // defines what is the least amount of cards which is OK to have in hand - below is bad
        private const int MIN_VALUE_EXECUTE = 8; // a creature to be execute has to have at least 4/4 stats or 8 in sum
        private const int NEVER_PLAY = 10000;
        // switches
        private const bool DEBUG_ENABLED = false;
        private const bool DEBUG_BEST_ENABLED = false;

		private float MinionEnemyTauntAddedValue = 1;
        private float MinionEnemyWindfuryAddedValue = 1;
        private float MinionDivineShieldAddedValue = 1;
        private float HeroEnemyHealthMultiplier = 1;
        private float HeroFriendHealthMultiplier = 1;
        private float FriendCardDrawMultiplier = 1;
        private float EnemyCardDrawMultiplier = 1;
        private float MinionEnemyAttackMultiplier = 1;
        private float MinionEnemyHealthMultiplier = 1;
        private float MinionFriendAttackMultiplier = 1;
        private float MinionFriendHealthMultiplier = 1;

		//Spells cast cost
        private float SpellsCastGlobalCost = 0;
		//Spells cast value
		private float SpellsCastGlobalValue = 0;
		//Weapons cast cost
        private float WeaponCastGlobalCost = 0;
		//Weapons cast value
        private float WeaponCastGlobalValue = 0;
		//Minions cast cost
        private float MinionCastGlobalCost = 0;
		//Minions cast value
        private float MinionCastGlobalValue = 0;
		//HeroPowerCost
        private float HeroPowerGlobalCost = 0;
		//Weapons Attack cost
        private float WeaponAttackGlobalCost = 0;
		//GlobalValueModifier
        private float GlobalValueModifier = 0;
        //Secret Modifier
        private float SecretModifier = 0;

        private List<Card.Cards> _enragingSpells = new List<Card.Cards>(new Card.Cards[] { Card.Cards.EX1_607, Card.Cards.EX1_391, Card.Cards.EX1_400, Card.Cards.EX1_603 });   // inner rage, whirlwind, slam, cruel taskmaster         
        private List<Card.Cards> _enrageableMinions = new List<Card.Cards>(new Card.Cards[] { Card.Cards.EX1_007, Card.Cards.EX1_402, Card.Cards.EX1_604, Card.Cards.BRM_019, Card.Cards.EX1_414 }); // acolyte, armorsmith, frothing berserker, grim patron, grommash
        private List<Card.Cards> _bestEnrageableMinions = new List<Card.Cards>(new Card.Cards[] { Card.Cards.EX1_604, Card.Cards.BRM_019, Card.Cards.EX1_414 }); // acolyte, armorsmith, frothing berserker, grim patron, grommash
    
        // PRIVATE METHODS        

        private void DebugLog(string s)
        {
            if (DEBUG_ENABLED)
                Debug("[PROFILE DEBUG]: " + s);
        }

        private void DebugBestLog(string s)
        {
            if (DEBUG_BEST_ENABLED)
                DebugBestMove("[PROFILE DEBUG BEST] : " + s);
        }

        private void SetMainValues(Board b)
        {                        
            MinionEnemyTauntAddedValue = 5;
		    MinionEnemyWindfuryAddedValue = 2.5f;
		    MinionDivineShieldAddedValue = 2;   
     
            FriendCardDrawMultiplier = 3f;
    		EnemyCardDrawMultiplier = 1;

            if (BoardHelper.GetOwnHP(b) - BoardHelper.GetEnemyHP(b) > 15)
    		    HeroEnemyHealthMultiplier = 1;       
            else
                HeroEnemyHealthMultiplier = 1;       

            if (BoardHelper.GetOwnHP(b) < 15 && BoardHelper.IsAggroClass(b.EnemyClass))
		        HeroFriendHealthMultiplier = 2;		    
            else
                HeroFriendHealthMultiplier = 1;		   

		    MinionEnemyAttackMultiplier = 1;
		    MinionEnemyHealthMultiplier = 1;
		    MinionFriendAttackMultiplier = 1;
		    MinionFriendHealthMultiplier = 1;		
        }

        // MAIN METHODS

		public override float GetBoardValue(Board board)
		{
			float value = 0;

            SetMainValues(board);

			//Hero friend value
			value += board.HeroFriend.CurrentHealth * HeroFriendHealthMultiplier + board.HeroFriend.CurrentArmor * HeroFriendHealthMultiplier;

			//Hero enemy value
			value -= board.HeroEnemy.CurrentHealth * HeroEnemyHealthMultiplier + board.HeroEnemy.CurrentArmor * HeroEnemyHealthMultiplier;

			//enemy board
			foreach(Card c in board.MinionEnemy)
			{
				value -= GetCardValue(board, c);
			}

			//friend board
			foreach(Card c in board.MinionFriend)
			{
				value += GetCardValue(board, c);
			}

			//casting costs
			value -= MinionCastGlobalCost;
			value -= SpellsCastGlobalCost;
			value -= WeaponCastGlobalCost;

			//casting action value
			value += WeaponCastGlobalValue;
			value += SpellsCastGlobalValue;
			value += MinionCastGlobalValue;

			//heropower vost
			value -= HeroPowerGlobalCost;

			//Weapon attack cost
			value -= WeaponAttackGlobalCost;

			if (board.HeroEnemy.CurrentHealth <= 0)
				value += 10000;

			if (board.HeroFriend.CurrentHealth <= 0 && board.FriendCardDraw == 0)
				value -= 100000;

			value += GlobalValueModifier;

			value += board.FriendCardDraw * FriendCardDrawMultiplier;
			value -= board.EnemyCardDraw * EnemyCardDrawMultiplier;

            value += SecretModifier;

            DebugLog("Actions: " + BoardHelper.GetActionsAsString(board) + ", Value: " + value);
            DebugBestLog("Best calculated actions: " + BoardHelper.GetActionsAsString(board) + ", Value: " + value);

			return value;
		}


		public float GetCardValue(Board board, Card card)
		{
			float value = 0;            

			//divine shield value
			if(card.IsDivineShield)
				value += MinionDivineShieldAddedValue;

			if(card.IsFriend)
			{
                value += card.CurrentHealth * MinionFriendHealthMultiplier + card.CurrentAtk * MinionFriendAttackMultiplier;

                if (card.IsFrozen)
                    value -= 1;				 
			}
			else
			{
				value += GetThreatModifier(card);
				//Taunt value
				if(card.IsTaunt)
					value += BoardHelper.GetEnemyTauntValue(board);

                float windfury = 1;

				if(card.IsWindfury)
					windfury = 2;

                value += card.CurrentHealth * MinionEnemyHealthMultiplier + card.CurrentAtk * MinionEnemyAttackMultiplier * windfury;
			}
            
			return value;
		}

		public override void OnCastMinion(Board board, Card minion, Card target)
		{                        
			switch (minion.Template.Id)
			{                                                                                                                                
				case Card.Cards.EX1_007://Acolyte of Pain, ID: EX1_007
					if (BoardHelper.CanBeImmediatelyKilledByEnemy(board, minion) && board.Hand.Count < CARDS_IN_HAND_THRESHHOLD)
					    MinionCastGlobalCost += GetCardValue(board, minion) * 0.3f;                                        
				    break;

				case Card.Cards.EX1_402: //Armorsmith, ID: EX1_402
                    if (BoardHelper.CanBeImmediatelyKilledByEnemy(board, minion) && !BoardHelper.CanPlayCard(board, _enragingSpells, minion.CurrentCost)) 
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
					break;

				case Card.Cards.EX1_012://Bloodmage Thalnos, ID: EX1_012
                    if (BoardHelper.CanBeImmediatelyKilledByEnemy(board, minion) || board.Hand.Count > CARDS_IN_HAND_THRESHHOLD || !BoardHelper.CanPlayCard(board, Card.Cards.EX1_400, minion.CurrentCost)) // EX1_400 - whirlwind
					    MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
				    break;

				case Card.Cards.EX1_603: //Cruel Taskmaster, ID: EX1_603					
					if ((target.CurrentHealth == 1 && !BoardHelper.IsOwnMinion(board, target)) || BoardHelper.CanEnrageOrGetArmor(board, _enragingSpells, _enrageableMinions) || BoardHelper.CanExecute(board, MIN_VALUE_EXECUTE, minion.CurrentCost))
                        MinionCastGlobalValue += 0; // GetCardValue(board, minion) * 1.3f;
                    else
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
				    break;

				case Card.Cards.NEW1_022://Dread Corsair, ID: NEW1_022
                    if (BoardHelper.CanPlayDreadCorsair(board, minion))
                        MinionCastGlobalValue += GetCardValue(board, minion) * 1.3f;
                    else
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
				    break;								
				
				case Card.Cards.GVG_110://Dr. Boom
					break;

                case Card.Cards.EX1_604://Frothing Berserker
                    if (BoardHelper.CanBeImmediatelyKilledByEnemy(board, minion) && !BoardHelper.CanCharge(board, minion) && !BoardHelper.CanPlayTaunt(board, minion.CurrentCost))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
                    if (!BoardHelper.CanBePlayedAndEnraged(board, _enragingSpells, minion) || !BoardHelper.CanCharge(board, minion) || !BoardHelper.CanPlayTaunt(board, minion.CurrentCost))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.3f;
					break;

                case Card.Cards.CS2_147://Gnomish Inventor
					break;

                case Card.Cards.BRM_019://Grim Patron
                    if ((BoardHelper.CanBeImmediatelyKilledByEnemy(board, minion) && !BoardHelper.CanBePlayedAndEnraged(board, _enragingSpells, minion)) || !(BoardHelper.CanCharge(board, minion) && BoardHelper.AnyEnemyTargets(board, 2)))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 2f;                    
					break;

                case Card.Cards.EX1_084://Warsong Commander
                    if (!BoardHelper.CanPlayWarsongCommander(board, minion))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 2f;                    
					break;

                case Card.Cards.BRMA03_1://Emperor Thaurissan
                    if (board.Hand.Count < 3)
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
                    if (board.Hand.Count > 5)
                        MinionCastGlobalValue += GetCardValue(board, minion) * 1.2f;
					break;

                case Card.Cards.EX1_414://Grommash Hellscream, ID: EX1_414
                    if (!BoardHelper.CanBePlayedAndEnraged(board, _enragingSpells, minion))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
					break;

                case Card.Cards.FP1_024://Unstable Ghoul, ID: FP1_024                                         								
                    if (board.MinionEnemy.Count == 0 || !BoardHelper.CanEnrageOrGetArmor(board, _enragingSpells, _enrageableMinions))
                        MinionCastGlobalCost += GetCardValue(board, minion) * 0.5f;
					break;                                                                                                   
			}

            if (BoardHelper.IsFirstMove(board))
                OnFirstAction(board, minion, target, true, false, false);

            //DebugLog("MINION: " + minion.Template.Name + ", standard value: " + GetCardValue(board, minion) + ", added value: " + MinionCastGlobalValue + ", added cost: " + MinionCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));
            //DebugBestLog("BEST MINION: " + minion.Template.Name + ", standard value: " + GetCardValue(board, minion) + ", added value: " + MinionCastGlobalValue + ", added cost: " + MinionCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));            			
		}                

        public override void OnCastSpell(Board board, Card spell, Card target)
		{
			switch (spell.Template.Id)
			{
				case Card.Cards.EX1_392://Battle Rage
					SpellsCastGlobalCost += 3;
                    SpellsCastGlobalValue += board.MinionFriend.FindAll(x => x.CurrentHealth < x.MaxHealth).Count * FriendCardDrawMultiplier + (board.HeroFriend.CurrentHealth < 30 ? 1 : 0) * FriendCardDrawMultiplier;
				    break;

				case Card.Cards.CS2_108://Execute
                    SpellsCastGlobalCost += MIN_VALUE_EXECUTE;				    
				    break;

				case Card.Cards.EX1_607://Inner Rage
                    if (board.MinionEnemy.Contains(target))
					    SpellsCastGlobalCost += 10;
                    if (board.MinionFriend.Contains(target))
                        SpellsCastGlobalCost += 8;
                    if (board.MinionFriend.Contains(target) && BoardHelper.CanBeEnraged(target, _enrageableMinions) && target.CanAttack)
                        SpellsCastGlobalValue += 4;
                    if (board.MinionFriend.Contains(target) && BoardHelper.CanBeEnraged(target, _bestEnrageableMinions) && target.CanAttack)
                        SpellsCastGlobalValue += 10;
				    break;

				case Card.Cards.EX1_391://Slam                    
                    if (board.MinionEnemy.Contains(target) && BoardHelper.CanImmediatelyKill(target, spell.CurrentAtk))
					    SpellsCastGlobalCost += 6;
                    if (board.MinionEnemy.Contains(target) && !BoardHelper.CanImmediatelyKill(target, spell.CurrentAtk))
                        SpellsCastGlobalCost += 4;

                    if (board.MinionFriend.Contains(target) && BoardHelper.CanImmediatelyKill(target, spell.CurrentAtk))
                        SpellsCastGlobalCost += NEVER_PLAY;
                    if (board.MinionFriend.Contains(target) && !BoardHelper.CanBeEnraged(target, _bestEnrageableMinions) && board.Hand.Count > 1)
                        SpellsCastGlobalCost += NEVER_PLAY;

                    if (board.MinionFriend.Contains(target) && BoardHelper.CanBeEnraged(target, _bestEnrageableMinions))
                        SpellsCastGlobalValue += 2;                    
				    break;

                case Card.Cards.EX1_400://Whirlwind
                    SpellsCastGlobalCost += 6;
                    SpellsCastGlobalValue += board.MinionEnemy.FindAll(x => x.CurrentHealth == 1).Count * 2 - board.MinionFriend.FindAll(x => x.CurrentHealth == 1).Count * 2;
                    SpellsCastGlobalValue += board.MinionFriend.FindAll(x => _enrageableMinions.Contains(x.Template.Id) && !_bestEnrageableMinions.Contains(x.Template.Id) && x.CurrentHealth > 1).Count * 2;
                    SpellsCastGlobalValue += board.MinionFriend.FindAll(x => _bestEnrageableMinions.Contains(x.Template.Id) && x.CurrentHealth > 1).Count * 3;
				    break;				
                    
				case Card.Cards.GAME_005://The Coin
					SpellsCastGlobalCost += GetCoinValue(board);
				    break;
		    }

            if(BoardHelper.IsFirstMove(board))
				OnFirstAction(board, spell, target, true, false, false);

            //DebugLog("SPELL: " + spell.Template.Name + ", standard value: " + GetCardValue(board, spell) + ", added value: " + SpellsCastGlobalValue + ", added cost: " + SpellsCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));
            //DebugBestLog("BEST SPELL: " + spell.Template.Name + ", standard value: " + GetCardValue(board, spell) + ", added value: " + SpellsCastGlobalValue + ", added cost: " + SpellsCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));            			
		}

        public override void OnCastWeapon(Board board, Card weapon, Card target)
        {                        
            switch (weapon.Template.Id)
            {
                case Card.Cards.FP1_021://Death's Bite, ID: FP1_021
                    if (board.WeaponFriend != null && GetThreatModifier(target) == 0)
                        WeaponCastGlobalCost += 10;

                    WeaponCastGlobalValue += board.MinionFriend.FindAll(x => _enrageableMinions.Contains(x.Template.Id) && x.CurrentHealth > 1).Count;
                    WeaponCastGlobalValue += board.Hand.FindAll(x => _enrageableMinions.Contains(x.Template.Id) && board.ManaAvailable + 1 >= x.CurrentCost).Count;
                    break;

                case Card.Cards.CS2_106://Fiery War Axe, ID: CS2_106
                    if (board.WeaponFriend != null)
                        WeaponCastGlobalCost += 10;
                    break;
            }

            if (BoardHelper.IsFirstMove(board))
                OnFirstAction(board, weapon, target, false, false, false);

            //DebugLog("WEAPON: " + weapon.Template.Name + ", standard value: " + GetCardValue(board, weapon) + ", added value: " + WeaponCastGlobalValue + ", added cost: " + WeaponCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));
            //DebugBestLog("BEST WEAPON: " + weapon.Template.Name + ", standard value: " + GetCardValue(board, weapon) + ", added value: " + WeaponCastGlobalValue + ", added cost: " + WeaponCastGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));            			
        }        

		public override void OnAttack(Board board, Card attacker, Card target)
		{
			bool IsAttackingWithHero = (attacker.Id == board.HeroFriend.Id);
			bool IsAttackingWithWeapon = (board.WeaponFriend != null && attacker.Id == board.WeaponFriend.Id);

			if ((IsAttackingWithHero || IsAttackingWithWeapon) && board.WeaponFriend != null)//If we attack with weapon equipped
			{
				switch (board.WeaponFriend.Template.Id)
				{
                }
			}

			if (!IsAttackingWithHero && !IsAttackingWithWeapon)
			{
				if (target != null && target.CurrentAtk >= attacker.CurrentHealth && !attacker.IsDivineShield)
					OnMinionDeath(board, attacker);
			}

            if (BoardHelper.IsFirstMove(board))
                OnFirstAction(board, attacker, target, false, true, false);

            //DebugLog("ATTACKER: " + attacker.Template.Name + ", standard value: " + GetCardValue(board, attacker) + ", added cost: " + WeaponAttackGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));
            //DebugBestLog("BEST ATTACKER: " + attacker.Template.Name + ", standard value: " + GetCardValue(board, attacker) + ", added cost: " + WeaponAttackGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));            			
		}

		public override void OnCastAbility(Board board, Card ability, Card target)
		{
			if(BoardHelper.GetOwnHP(board) <= 15) HeroPowerGlobalCost -= 10;
			if(board.TurnCount < 2) HeroPowerGlobalCost += 10;
			HeroPowerGlobalCost += 2;

            if (BoardHelper.IsFirstMove(board))
                OnFirstAction(board, ability, target, false, false, true);

            //DebugLog("ABILITY: " + ability.Template.Name + ", standard value: " + GetCardValue(board, ability) + ", added cost: " + HeroPowerGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));
            //DebugBestLog("BEST ABILITY: " + ability.Template.Name + ", standard value: " + GetCardValue(board, ability) + ", added cost: " + HeroPowerGlobalCost + "; TARGET: " + ", standard value: " + GetCardValue(board, target));            			
		}

        public void OnFirstAction(Board board, Card minion, Card target, bool castCard, bool attackCard, bool castAbility)
        {
            if (board.Hand.Any(x => x.Template.Id == Card.Cards.GVG_074))
            {
                if (minion.Template.Id == Card.Cards.GVG_074)
                    SecretModifier += 50;

                return;
            }

            Card.CClass enemyclass = board.EnemyClass;
            bool lowestValueActor = false;

            if (minion != null && board.GetWorstMinionCanAttack() != null && board.GetWorstMinionCanAttack().Id == minion.Id && castCard)
                lowestValueActor = true;
            else if (minion != null && board.GetWorstMinionFromHand() != null && board.GetWorstMinionFromHand().Id == minion.Id && castCard)
                lowestValueActor = true;

            switch (enemyclass)
            {
                case Card.CClass.HUNTER:

                    if (castAbility && minion.Template.Id == Card.Cards.CS1h_001 && target.Type == Card.CType.MINION && target.IsFriend && target.CurrentHealth <= 2 && target.MaxHealth >= 3)
                        SecretModifier += 20;

                    if (castCard && minion.Template.Id == Card.Cards.FP1_007)
                        SecretModifier += 50;

                    if (castCard && minion.Template.Id == Card.Cards.EX1_093)
                    {
                        if ((board.GetLeftMinion(minion) != null && board.GetLeftMinion(minion).CurrentHealth == 2) && (board.GetRightMinion(minion) != null && board.GetRightMinion(minion).CurrentHealth == 2))
                        {
                            if (BoardHelper.Get2HpMinions(board) > 1)
                                SecretModifier += 50;
                        }
                        else if (board.GetLeftMinion(minion) != null && board.GetLeftMinion(minion).CurrentHealth == 2)
                        {
                            if (BoardHelper.Get2HpMinions(board) > 0)
                                SecretModifier += 25;
                        }
                        else if (board.GetRightMinion(minion) != null && board.GetRightMinion(minion).CurrentHealth == 2)
                        {
                            if (BoardHelper.Get2HpMinions(board) > 0)
                                SecretModifier += 25;
                        }
                    }

                    if (attackCard)
                    {
                        if (lowestValueActor && !board.TrapMgr.TriggeredHeroWithMinion)
                        {
                            if (BoardHelper.GetWeakMinions(board) > 1 && board.MinionEnemy.Count > 0)
                            {
                                if (target.Type == Card.CType.HERO)
                                {
                                    if (board.MinionEnemy.Count == 0 || BoardHelper.GetCanAttackMinions(board) == 1)
                                        SecretModifier += 5;
                                    else
                                        SecretModifier -= BoardHelper.GetWeakMinions(board) * 3;
                                }

                                if (target.Type == Card.CType.MINION)
                                    SecretModifier += 2.5f;
                            }
                        }

                        if (!lowestValueActor && (!board.TrapMgr.TriggeredHeroWithMinion || !board.TrapMgr.TriggeredMinionWithMinion))
                            SecretModifier -= 1.5f;
                    }
                    else
                        SecretModifier -= 5;

                    break;

                case Card.CClass.MAGE:
                    if (!board.TrapMgr.TriggeredCastMinion && lowestValueActor && castCard)
                    {
                        SecretModifier += 25;
                    }

                    break;

                case Card.CClass.PALADIN:
                    if (!board.TrapMgr.TriggeredHeroWithMinion && lowestValueActor
                        && minion != null && castCard
                        && target != null && target.Type == Card.CType.HERO
                        && attackCard)
                    {
                        SecretModifier += 5;
                    }

                    break;
            }

            if (castCard)
            {
                board.TrapMgr.TriggeredCastMinion = true;
            }
            else if (castAbility)
            {
            }
            else if (attackCard)
            {
                if (target != null && target.Type == Card.CType.HERO)
                    board.TrapMgr.TriggeredHeroWithMinion = true;

                if (target != null && target.Type == Card.CType.MINION)
                    board.TrapMgr.TriggeredMinionWithMinion = true;
            }
        }

		public override RemoteProfile DeepClone()
		{
			bProfile ret = new bProfile();

			ret._logBestMove.AddRange(_logBestMove);
			ret._log = _log;

			ret.HeroEnemyHealthMultiplier = HeroEnemyHealthMultiplier;
			ret.HeroFriendHealthMultiplier = HeroFriendHealthMultiplier;
			ret.MinionEnemyAttackMultiplier = MinionEnemyAttackMultiplier;
			ret.MinionEnemyHealthMultiplier = MinionEnemyHealthMultiplier;
			ret.MinionFriendAttackMultiplier = MinionFriendAttackMultiplier;
			ret.MinionFriendHealthMultiplier = MinionFriendHealthMultiplier;

			ret.SpellsCastGlobalCost = SpellsCastGlobalCost;
			ret.SpellsCastGlobalValue = SpellsCastGlobalValue;
			ret.WeaponCastGlobalCost = WeaponCastGlobalCost;
			ret.WeaponCastGlobalValue = WeaponCastGlobalValue;
			ret.MinionCastGlobalCost = MinionCastGlobalCost;
			ret.MinionCastGlobalValue = MinionCastGlobalValue;

			ret.HeroPowerGlobalCost = HeroPowerGlobalCost;
			ret.WeaponAttackGlobalCost = WeaponAttackGlobalCost;

			ret.GlobalValueModifier = GlobalValueModifier;
            ret.SecretModifier = SecretModifier;

			return ret;
		}

        public void OnMinionDeath(Board board, Card minion)
		{
			switch (minion.Template.Id)
			{
			}
		}


        // THREATS
		public float GetThreatModifier(Card card)
		{
			switch (card.Template.Id)
			{
                //Imp Gang Boss, ID: BRM_006
                //Nerubian Egg, ID: FP1_007
                // snegochuch

                case Card.Cards.GVG_018://Mistress of Pain
                    return 3f;

                case Card.Cards.EX1_402: // Armorsmith
                    return 4f;

                case Card.Cards.GVG_006://Mechwarper
                    return 3.5f;

                case Card.Cards.FP1_013://Kel'Thuzad
                    return 3f;

                case Card.Cards.EX1_016://Sylvanas Windrunner
                    return 2.5f;

                case Card.Cards.GVG_105://Piloted Sky Golem
                    return 1.5f;

                case Card.Cards.BRM_031://Chromaggus
                    return 2.5f;

                case Card.Cards.EX1_559://Archmage Antonidas
                    return 4f;

                case Card.Cards.GVG_021://Mal'Ganis
                    return 3f;

                case Card.Cards.EX1_608://Sorcerer's Apprentice
                    return 4f;

                case Card.Cards.NEW1_012://Mana Wyrm
                    return 2.5f;

                case Card.Cards.BRM_002://Flamewaker
                    return 4.5f;

                case Card.Cards.EX1_595://Cult Master
                    return 1.2f;

                case Card.Cards.NEW1_021://Doomsayer
                    return 1.1f;

                case Card.Cards.EX1_243://Dust Devil
                    return 1.2f;

                case Card.Cards.EX1_170://Emperor Cobra
                    return 2f;

                case Card.Cards.BRM_028://Emperor Thaurissan
                    return 3f;

                case Card.Cards.EX1_565://Flametongue Totem
                    return 4f;

                case Card.Cards.GVG_100://Floating Watcher
                    return 2f;

                case Card.Cards.GVG_113://Foe Reaper 4000
                    return 1.1f;

                case Card.Cards.tt_004://Flesheating Ghoul
                    return 1.2f;

                case Card.Cards.EX1_604://Frothing Berserker
                    return 2f;

                case Card.Cards.BRM_019://Grim Patron
                    return 3.5f;

                case Card.Cards.EX1_084://Warsong Commander
                    return 3.5f;

                case Card.Cards.EX1_095://Gadgetzan Auctioneer
                    return 2f;

                case Card.Cards.NEW1_040://Hogger
                    return 1.5f;

                case Card.Cards.GVG_104://Hobgoblin
                    return 2.5f;

                case Card.Cards.EX1_614://Illidan Stormrage
                    return 2f;

                case Card.Cards.GVG_027://Iron Sensei
                    return 2.5f;

                case Card.Cards.GVG_094://Jeeves
                    return 2.5f;

                case Card.Cards.NEW1_019://Knife Juggler
                    return 3.5f;

                case Card.Cards.EX1_001://Lightwarden
                    return 2f;

                case Card.Cards.EX1_563://Malygos
                    return 3f;

                case Card.Cards.GVG_103://Micro Machine
                    return 2.5f;

                case Card.Cards.EX1_044://Questing Adventurer
                    return 3f;

                case Card.Cards.EX1_298://Ragnaros the Firelord
                    return 3.5f;

                case Card.Cards.GVG_037://Whirling Zap-o-matic
                    return 3f;

                case Card.Cards.NEW1_020://Wild Pyromancer
                    return 3f;

                case Card.Cards.GVG_013://Cogmaster
                    return 4f;
			}

			return 0;
		}        

		public static int GetCoinValue(Board board)
		{
			return 2;
		}
	}

    // HELPER CLASS
	public static class BoardHelper
	{       
        public static bool IsFirstMove(Board board)
        {
            return (board.SecretEnemy && board.ActionsStack.Count == 0);
        }

        public static int Get2HpMinions(Board b)
        {
            int i = 0;

            foreach (Card card in b.MinionFriend)
            {
                if (card.CurrentHealth == 2 && card.IsDivineShield == false)
                    i++;
            }

            return i;
        }

        public static int GetWeakMinions(Board b)
        {
            int i = 0;

            foreach (Card card in b.MinionFriend)
            {
                if (card.CurrentHealth <= 2 && card.IsDivineShield == false)
                    i++;
            }

            return i;
        }

        public static int GetCanAttackMinions(Board b)
        {
            int i = 0;

            foreach (Card card in b.MinionFriend)
            {
                if (card.CanAttack)
                    i++;
            }

            return i;
        }

        // MY METHODS

        public static bool IsOwnMinion(Board board, Card minion)
        {
            return board.MinionFriend.Any(x => x.Id == minion.Id);
        }

        public static string GetActionsAsString(Board board)
        {
            string s = "";
            foreach (var action in board.ActionsStack)
            {
                s += action.ToString() + ", ";            
            }

            return s;
        }

        public static bool CanBeImmediatelyKilledByEnemy(Board board, Card minion)
        {            
            if (board.HeroEnemy.CanAttackWithWeapon && board.HeroEnemy.CanAttack && board.HeroEnemy.CurrentAtk >= minion.CurrentHealth)                            
                return true;

            return board.MinionEnemy.Any(x => x.CurrentAtk >= minion.CurrentHealth && x.CanAttack);
        }

        public static bool CanImmediatelyKillEnemy(Board board, int damage)
        {
            return board.MinionEnemy.Any(x => x.CurrentHealth <= damage);
        }

        public static bool CanImmediatelyKill(Card target, int damage)
        {
            return target.CurrentHealth - damage <= 0;
        }        

        public static bool AnyEnemyTargets(Board board, int maxAttack)
        {
            return board.MinionEnemy.Any(x => x.CurrentAtk <= maxAttack);
        }

        public static bool CanPlayCard(Board board, List<Card.Cards> cards, int minusMana = 0)
        {            
            foreach (Card.Cards c in cards)
            {
                if (board.Hand.Any(x => x.Template.Id == c && board.ManaAvailable - minusMana >= x.CurrentCost))
                    return true;
            }
            
            return false;
        }

        public static bool CanPlayCard(Board board, Card.Cards card, int minusMana = 0)
        {
            return board.Hand.Any(x => x.Template.Id == card && board.ManaAvailable - minusMana >= x.CurrentCost);
        }

        public static bool CanPlayTaunt(Board board, int minusMana = 0)
        {
            return board.Hand.Any(x => x.IsTaunt && board.ManaAvailable - minusMana >= x.CurrentCost);
        }

        public static bool CanExecute(Board board, int minValue, int minusMana)
        {
            // CS2_108 = Execute
            return board.Hand.Any(x => x.Template.Id == Card.Cards.CS2_108 && board.ManaAvailable - minusMana >= x.CurrentCost) && board.MinionEnemy.Any(x => x.CurrentHealth < x.MaxHealth && (x.CurrentAtk + x.CurrentHealth >= minValue));
        }

        public static bool CanPlayDreadCorsair(Board board, Card corsair)
        {
            return (board.HeroFriend.CanAttackWithWeapon && corsair.CurrentCost - board.HeroFriend.CurrentAtk >= board.ManaAvailable) || (board.Hand.Any(c => c.Type == Card.CType.WEAPON && c.CurrentCost <= board.ManaAvailable && (board.ManaAvailable - c.CurrentCost >= corsair.CurrentCost - c.CurrentAtk)));            
        }

        public static bool CanPlayWarsongCommander(Board board, Card warsong)
        {
            return board.Hand.Any(c => board.ManaAvailable - warsong.CurrentCost >= c.CurrentCost && c.CurrentAtk <= 3);
        }

        public static bool CanCharge(Board board, Card minion)
        {            
            // EX1_084 = Warsong Commander
            return board.MinionFriend.Any(x => x.Template.Id == Card.Cards.EX1_084) || board.Hand.Any(x => x.Template.Id == Card.Cards.EX1_084 && board.ManaAvailable - minion.CurrentCost >= x.CurrentCost);         
        }

        public static bool HasWeaponEquipedOrCanEquipWeapon(Board board, int minusMana = 0)
        {
            if (board.HeroFriend.CanAttackWithWeapon)
                return true;

            return HasPlayableWeaponInHand(board, minusMana);
        }

        public static bool HasWeaponInHand(Board board)
        {
            return board.Hand.Any(c => c.Type == Card.CType.WEAPON);
        }

        public static bool HasPlayableWeaponInHand(Board board, int minusMana = 0)
        {
            return board.Hand.Any(c => c.Type == Card.CType.WEAPON && board.ManaAvailable - minusMana >= c.CurrentCost);
        }

        public static bool CanEnrageOrGetArmor(Board board, List<Card.Cards> enragingSpells, List<Card.Cards> enrageableMinions)
        {                        
            foreach (Card.Cards c in enrageableMinions)
            {
                if (board.MinionFriend.Any(x => x.Template.Id == c && x.CurrentHealth > 1))
                    return true;

                if (board.Hand.Any(x => x.Template.Id == c && board.ManaAvailable >= x.CurrentCost && CanPlayCard(board, enragingSpells, x.CurrentCost)))
                    return true;
            }

            return false;            
        }
        
        public static bool CanBePlayedAndEnraged(Board board, List<Card.Cards> enragingSpells, Card minionToEnrage)
        {                            
            return board.Hand.Any(x => x == minionToEnrage && board.ManaAvailable >= x.CurrentCost && CanPlayCard(board, enragingSpells, x.CurrentCost));
        }

        public static bool CanBeEnraged(Card minionToEnrage, List<Card.Cards> enrageableMinions)
        {
            return enrageableMinions.Contains(minionToEnrage.Template.Id);
        }

		public static List<Card> GetPlayables(Card.CType card_type, int min_cost, int max_cost, Board board)
		{            
			return board.Hand.FindAll(x => x.Type == card_type && x.CurrentCost >= min_cost && x.CurrentCost <= max_cost).ToList();
		}

        public static bool HasCardsInHand(Board board, List<Card.Cards> whiteList)
        {
            foreach (Card.Cards c in whiteList)
            {
                if (board.Hand.Any(x => x.Template.Id == c))
                    return true;
            }
            
            return false;
        }
        
		public static List<Card> GetPlayables(int min_cost, int max_cost, Board board)
		{
			return board.Hand.FindAll(x => x.CurrentCost >= min_cost && x.CurrentCost <= max_cost).ToList();
		}        

		public static bool IsSilenceCard(Card c)
		{
			switch(c.Template.Id)
			{
				case Card.Cards.EX1_332://Silence
					return true;

				case Card.Cards.EX1_166://Keeper of the Grove
					return true;

				case Card.Cards.CS2_203://Ironbeak Owl
					return true;

				case Card.Cards.EX1_048://Spellbreaker
					return true;

				case Card.Cards.EX1_245://Earth Shock
					return true;
					
				case Card.Cards.EX1_626://Mass Dispel
					return true;

				default:
					return false;
			}			
		}

        public static bool IsAggroClass(Card.CClass heroClass)
        {
            if (heroClass == Card.CClass.HUNTER || heroClass == Card.CClass.MAGE || heroClass == Card.CClass.ROGUE)
                return true;
            else
                return false;
        }

        public static int GetEnemyTauntValue(Board b)
        {
            return (b.MinionEnemy.FindAll(x => !x.IsTaunt).Count);
        }        

        public static int GetOwnHP(Board b)
        {
            return b.HeroFriend.CurrentHealth + b.HeroFriend.CurrentArmor;
        }

        public static int GetEnemyHP(Board b)
        {
            return b.HeroEnemy.CurrentHealth + b.HeroEnemy.CurrentArmor;
        }

        public static float BoardControl(Board b)
        {
            float value = 0;

            foreach (Card c in b.MinionFriend)
            {
                float windfury = 1;

                if (c.IsWindfury)
                    windfury = 2;

                if (!c.IsFrozen)
                    value += c.CurrentAtk * windfury + c.CurrentHealth;
            }

            foreach (Card c in b.MinionEnemy)
            {
                float windfury = 1;

                if (c.IsWindfury)
                    windfury = 2;

                if (!c.IsFrozen)
                    value -= c.CurrentAtk * windfury + c.CurrentHealth;
            }

            return value;
        }

		public static bool IsSparePart(Card c)
		{
			switch(c.Template.Id)
			{
				case Card.Cards.PART_001:
					return true;
				case Card.Cards.PART_002:
					return true;
				case Card.Cards.PART_003:
					return true;
				case Card.Cards.PART_004:
					return true;
				case Card.Cards.PART_005:
					return true;
				case Card.Cards.PART_006:
					return true;
				case Card.Cards.PART_007:
					return true;

				default:
					return false;
			}			
		}
	}
}