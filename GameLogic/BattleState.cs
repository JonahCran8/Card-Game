using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic;

public sealed class BattleState
{
	public PlayerState Player { get; private set; } = null!;
	public PlayerState Enemy  { get; private set; } = null!;

	public int HandSize { get; private set; } = 5;
	public bool PlayerNeedsRecoveryTurn { get; private set; }
	public bool EnemyNeedsRecoveryTurn { get; private set; }
	public bool EnemyIsRecovering { get; private set; }
	public bool RoundCanEnd { get; set; } = false;

	private readonly Random _rng = new();

	public void Initialize(CardDatabase db)
	{
		Player = new PlayerState(MaxHp: 500, BaseAttack: 100, BaseDefense: 100);
		Enemy  = new PlayerState(MaxHp: 500, BaseAttack: 100,  BaseDefense: 100);
		
		var pool = db.Cards.Keys.ToList();
		Player.LoadDeck(RandomDeck(pool, 12));
		Enemy.LoadDeck(RandomDeck(pool, 12));
		
		Player.ShuffleDrawPile(_rng);
		Enemy.ShuffleDrawPile(_rng);
		
		StartRound();
	}
	
	private List<string> RandomDeck(List<string> pool, int size)
	{
		var deck = new List<string>();
		for (int i = 0; i < size; i++)
		{
			deck.Add(pool[_rng.Next(pool.Count)]);
		}
		return deck;
	}

	public void StartRound()
	{
		RoundCanEnd = false;
		
		RecoveryCheck();
		
		if (EnemyNeedsRecoveryTurn) EnemyRecoveryTurn();
		if (PlayerNeedsRecoveryTurn) PlayerRecoveryTurn();
		
		Player.DrawUpTo(HandSize);
		Enemy.DrawUpTo(HandSize);
	}
	
	//Player turn, play card if not recovering
	public string PlayerTurn()
	{
		var playerCardId = Player.Hand[0];
		Player.PlayCard(0);
		return playerCardId;
	}
	
	//Enemy turn, play card if not recovering
	public string EnemyTurn()
	{	
		var enemyCardId = Enemy.Hand[0];
		Enemy.PlayCard(0);
		return enemyCardId;
	}
	
	//If player presses recovery button
	public void PlayerRecoveryTurn()
	{
		Player.DrawPile.AddRange(Player.DiscardPile);
		Player.DiscardPile.Clear();
		Player.ShuffleDrawPile(_rng);
		PlayerNeedsRecoveryTurn = false;
	}
	public void EnemyRecoveryTurn()
	{
		Enemy.DrawPile.AddRange(Enemy.DiscardPile);
		Enemy.DiscardPile.Clear();
		Enemy.ShuffleDrawPile(_rng);
		EnemyIsRecovering = true;
		EnemyNeedsRecoveryTurn = false;
	}
	
	//Checks if player and enemy need to recover
	public void RecoveryCheck()
	{
		PlayerNeedsRecoveryTurn = (Player.DrawPile.Count + Player.Hand.Count < 2);
  		EnemyNeedsRecoveryTurn  = (Enemy.DrawPile.Count + Enemy.Hand.Count < 2);
	}
	
	//Checks if turn can be resolve properly
	public ResolveResult ResolveTurn()
	{
		if (Player.Hand.Count >= HandSize || Player.CardsPlayed.Count > 0)  return ResolveResult.RecoveryNotNeeded;
		return ResolveResult.Ok;
	}
	
	//Ends the round, cards played added to discard pile and cleared
	public void EndRound(CardDatabase db)
	{
		EnemyIsRecovering = false;
		RoundCanEnd = false;
		ResolveAllPlayedCards(db);
		Player.DiscardPile.AddRange(Player.CardsPlayed);
		Player.CardsPlayed.Clear();
		Enemy.DiscardPile.AddRange(Enemy.CardsPlayed);
		Enemy.CardsPlayed.Clear();
	}
	
	private void ApplyCardEffects(CardDefinition card, PlayerState source, PlayerState target,
								List<ActionData> actions, ActionData action)
	{
		foreach (var effect in card.effects)
		{
			switch (effect.type)
			{
				case EffectType.damage:
					ApplyDamage(effect, card, source, target, actions, action);
					break;
					
				case EffectType.bonusAttackPercent:
					source.GainBonusAttackPercentOfBase(effect.amount ?? 0);
					break;
					
				case EffectType.bonusDefensePercent:
					source.GainBonusDefensePercentOfBase(effect.amount ?? 0);
					break;
					
				case EffectType.resetBonusStats:
					switch (effect.stat)
					{
						case "attack":
							source.RemoveBonusAttack();
							break;
						case "defense":
							source.RemoveBonusDefense();
							break;
						case "all":
							source.RemoveAllBonusStats();
							break;
					}
				break;
			}
		}
	}

	private sealed class ActionData
	{
		public CardDefinition card = null!;
		public PlayerState source = null!;
		public PlayerState target = null!;
		public int slotIndex;
		public bool canceled;
	}
	
	private void ApplyDamage(CardEffect effect, CardDefinition card, PlayerState source, PlayerState target,
							List<ActionData> actions, ActionData action)
	{
		int bonus = 0;
		foreach (var e in card.effects)
		{
			if (e.type == EffectType.conditionalDamage)
			bonus += ConditionalDamageBonus(actions, action, e);
		}
		var (debuffPercent, subtract)  = damageDebuff(actions, action);
		int baseDamage = (effect.amount ?? 0) + bonus;
		baseDamage -= subtract;
		
		int attack = source.TotalAttack;
		int defense = Math.Max(1, target.TotalDefense);
		int damage = (int)MathF.Round(baseDamage * (attack / (2f * defense)));
		damage = (int)MathF.Round(damage * debuffPercent);
		target.TakeDamage(damage);
	}
	
	private void AddAction(CardDatabase db, List<ActionData> actions,
							PlayerState source, PlayerState target, int slot)
	{
		if (source.CardsPlayed.Count <= slot) return;
		
		var c = db.GetCard(source.CardsPlayed[slot]);
		
		if (c == null) return;

		actions.Add(new ActionData
		{
			card = c,
			source = source,
			target = target,
			slotIndex = slot,
			canceled = false
		});
	}
	
	private void DebuffCards(List<ActionData> actions, ActionData action)
	{
		foreach (var effect in action.card.effects)
		{
			if (effect.type != EffectType.debuffCard) continue;
			
			bool self = effect.target == "self";
			//if target is self make it true, otherwise false (enemy)
			int targetSlot = effect.card == "same_slot"
			//if target slot is the same slot select the card in it
				? action.slotIndex
				: (action.slotIndex == 0 ? 1 : 0);
				//otherwise select other card
			
			foreach (var a in actions)
			{
				bool sameSide = a.source == action.source;
				if (self && sameSide && a.slotIndex == targetSlot)
					a.canceled = true;
				
				if (!self && !sameSide && a.slotIndex == targetSlot)
					a.canceled = true;
			}
		}
	}
	
	private (float percent, int subtract) damageDebuff(List<ActionData> actions, ActionData action)
	{
		var debuffPercent = 1f;
		var debuffSubtract = 0;
		
		foreach (var a in actions)
		{
			
			//cycles through all 4 cards
			foreach (var effect in a.card.effects)
			{
				if (!Target(a, action, effect)) continue;
				if (effect.type != EffectType.damageDebuff) continue;
				
				if (effect.mode == "percent")
				debuffPercent *= (effect.amount ?? 100) / 100f;
				
				if (effect.mode == "subtract")
				debuffSubtract += effect.amount ?? 0;
			}
			
		}
		
		return (debuffPercent, debuffSubtract);
	}
	
	private int ConditionalDamageBonus(List<ActionData> actions, ActionData action, CardEffect effect)
	{
		var bonusAmount = 0;
		switch(effect.condition)
		{
			case "opponent_played_damage_same_slot":
				foreach (var a in actions)
				{
					if (a.source == action.source) continue;
					if (a.slotIndex != action.slotIndex) continue;
					if (a.card.effects.Any(e => e.type == EffectType.damage))
					bonusAmount += effect.amount ?? 0;
				}
			break;
		}
		
		return bonusAmount;
	}
	
	private bool Target(ActionData a, ActionData action, CardEffect effect)
	{
		if (effect.target == "all") return true;
		
		bool sameSide = a.source == action.source;
		if (effect.target == "self" && !sameSide) return false;
		if (effect.target == "enemy" && sameSide) return false;
		
		// slot selection
		switch (effect.card)
		{
		case "same_slot":   return a.slotIndex == action.slotIndex;
		case "other_slot":  return a.slotIndex != action.slotIndex;
		case "slot_1":      return a.slotIndex == 0;
		case "slot_2":      return a.slotIndex == 1;
		case "both_slots":  return true;
		default:            return true; // no slot specified
		}
	}
	
	public void ResolveAllPlayedCards(CardDatabase db)
	{
		
		var actions = new List<ActionData>();
		
		AddAction(db, actions, Player, Enemy, 0);
		AddAction(db, actions, Player, Enemy, 1);
		AddAction(db, actions, Enemy, Player, 0);
		AddAction(db, actions, Enemy, Player, 1);
		
		//Player cards
		foreach (var action in actions)
		{
			DebuffCards(actions, action);
		}
		
		foreach (var group in actions
			.Where(a => !a.canceled)
			.GroupBy(a => a.card.priority)
			.OrderByDescending(g => g.Key))
		{
		foreach (var action in group)
			ApplyCardEffects(action.card, action.source, action.target, actions, action);
		}
	}
}
