namespace GameLogic;

using System;
using System.Collections.Generic;


public sealed class PlayerState
{
	//base stats
	public int BaseAttack { get; }
	public int BaseDefense { get; }
	public int MaxHp { get; }
	
	public int BonusAttack { get; private set;}
	public int BonusDefense { get; private set;}
	
	public int Hp { get; private set;}
	
	//set base values
	public PlayerState(int MaxHp, int BaseAttack, int BaseDefense)
	{
		this.MaxHp = MaxHp;
		this.BaseAttack = BaseAttack;
		this.BaseDefense = BaseDefense;
		
		this.Hp = MaxHp;
		this.BonusAttack = 0;
		this.BonusDefense = 0;
	}
	
	// Card piles (store card IDs for now)
	public List<string> DrawPile { get; } = new();
	public List<string> Hand { get; } = new();
	public List<string> DiscardPile { get; } = new();
	public List<string> ExhaustPile { get; } = new();
	public List<string> CardsPlayed { get; } = new();

	
	//add bonus attack, percent of base attack
	public void GainBonusAttackPercentOfBase(int percent)
	{
		int add = (BaseAttack * percent) / 100;
		BonusAttack = BonusAttack + add;
		if ((BaseAttack + BonusAttack) <= 0)
		BonusAttack = 1 - BaseAttack;
	}
	
	//add bonus defense, percent of base defense
	public void GainBonusDefensePercentOfBase(int percent)
	{
		int add = (BaseDefense * percent) / 100;
		BonusDefense = BonusDefense + add;
		if ((BaseDefense + BonusDefense) <= 0)
		BonusDefense = 1 - BaseDefense;
	}
	
	//remove bonus attack
	public void RemoveBonusAttack()
	{
		BonusAttack = 0;
	}
	
	//remove bonus defense
	public void RemoveBonusDefense()
	{
		BonusDefense = 0;
	}
	
	//remove all bonuses
	public void RemoveAllBonusStats()
	{
		RemoveBonusAttack();
		RemoveBonusDefense();
	}
	
	public void TakeDamage(int damage)
	{
		//if damage is negative, do no damage
		if (damage < 0) 
		{
			damage = 0;
		}
		
		//decrease health based on damage
		Hp = Hp - damage;
		
		//if health goes below 0, keep it at 0
		if (Hp < 0)
		{
			Hp = 0;
		}
	}
	
	//set total attack and defense to the combined totals
	public int TotalAttack => BaseAttack + BonusAttack;
	public int TotalDefense => BaseDefense + BonusDefense;
	
	public void LoadDeck(IEnumerable<string> cardIds)
	{
		DrawPile.Clear();
		Hand.Clear();
		DiscardPile.Clear();
		ExhaustPile.Clear();
		CardsPlayed.Clear();

		DrawPile.AddRange(cardIds);
	}

	public void ShuffleDrawPile(Random rng)
	{
		// Fisher-Yates shuffle
		for (int i = DrawPile.Count - 1; i > 0; i--)
		{
			int j = rng.Next(i + 1);
			(DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
		}
	}

	public void DrawUpTo(int handSize)
	{
		while (Hand.Count < handSize && DrawPile.Count > 0)
		{
			string top = DrawPile[^1];
			DrawPile.RemoveAt(DrawPile.Count - 1);
			Hand.Add(top);
		}
	}

	public bool PlayCard(int handIndex)
	{
		if (handIndex < 0 || handIndex >= Hand.Count) return false;

		string cardId = Hand[handIndex];
		Hand.RemoveAt(handIndex);
		CardsPlayed.Add(cardId);
		return true;
	}
}
