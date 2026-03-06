using Godot;
using GameLogic;
using System.Collections.Generic;

public partial class BattleController : Control
{
	private BattleState _battle = null!;
	private readonly List<string> _logLines = new();
	private string LastLogMessage = "";
	private RichTextLabel _Log;
	private int _duplicateCount = 1;
	private int selectedHand = -1;
	private CardDatabase _cardDb = null!;
	
	public override void _Ready()
	{
		_battle = new BattleState();
		_battle.Initialize();
		
		_Log = GetNode<RichTextLabel>("Log");
		_Log.ScrollActive = true;
		_Log.ScrollFollowing = true;
		
		//Initialise player hp bar
		var PlayerBar = GetNode<ProgressBar>("PlayerUI/PlayerHpBar");
		PlayerBar.MaxValue = _battle.Player.MaxHp;
		PlayerBar.Value = _battle.Player.Hp;
		
		//Initialise enemy hp bar
		var enemyBar = GetNode<ProgressBar>("PlayerUI/EnemyHpBar");
		enemyBar.MaxValue = _battle.Enemy.MaxHp;
		enemyBar.Value = _battle.Enemy.Hp;
		
		//Initialise buttons
		GetNode<Button>("PlayerUI/PlayCardButton").Pressed += OnPlayCard;
		GetNode<Button>("PlayerUI/EndRoundButton").Pressed += OnEndRound;
		GetNode<Button>("PlayerUI/RecoverDeckButton").Pressed += OnRecoverDeck;
		
		_cardDb = GetNode<CardDatabase>("/root/CardDb");
		
		Log("Initialized battle. Drew opening hand.");
		Render();
	}
		
	//When PlayCard button is pressed
	private void OnPlayCard()
	{	
		if (_battle.RoundCanEnd)
		{
			Log("Please end the round");
			return;
		}
		if (_battle.PlayerNeedsRecoveryTurn)
		{
			Log("Please recover deck");
			return;
		}
		if (selectedHand < 0)
		{
			Log("Select a card first.");
			return;
		}
		//Player plays card
		PlayerPlayCard();
		
		//Plays enemy card
		EnemyPlayCard();
		
		//Once two cards are played, round can be ended
		if (_battle.Player.CardsPlayed.Count == 2)
		{
			_battle.RoundCanEnd = true;
		}
		Render();
	}
	
	private void OnSelectCard(int index)
	{
		selectedHand = index;
		Log($"Selected Card {index + 1}");
	}

	//Makes the player play a card
	private void PlayerPlayCard()
	{
		var playerCardId = _battle.Player.Hand[selectedHand];
		_battle.Player.PlayCard(selectedHand);
		
		selectedHand = -1;
		Log($"Played card {_battle.Player.CardsPlayed.Count}/2: {playerCardId}");
	}
	
	private void EnemyPlayCard()
	{
		if(_battle.EnemyIsRecovering) return;
		
		var enemyCardId = _battle.EnemyTurn();
		Log($"Enemy played card {_battle.Enemy.CardsPlayed.Count}/2: {enemyCardId}");
		
	}

	//Function for when RecoverDeck button is pressed
	private void OnRecoverDeck()
	{
		if (_battle.RoundCanEnd)
		{
			Log("Please end the round");
			return;
		}
		//Check if recovery is possible
		var result = _battle.ResolveTurn();
		//if not possible
		if (result == ResolveResult.RecoveryNotNeeded) 
		{
			Log("Recovery not possible yet");
			return;
		}
		
		PlayerRecovery();
	}

	public void PlayerRecovery()
	{
		//shorthand for _battle
		var b = _battle;
		Log("Deck recovered");
		
		//Player recovers deck, enemy plays twice, turn ends and starts again.
		b.PlayerRecoveryTurn();
		var enemyCardId = b.EnemyTurn();
		if(!_battle.EnemyNeedsRecoveryTurn)
		{
			Log($"Enemy played card {_battle.Enemy.CardsPlayed.Count}/2: {enemyCardId}");
			enemyCardId = b.EnemyTurn();
			Log($"Enemy played card {_battle.Enemy.CardsPlayed.Count}/2: {enemyCardId}");
		}
		else 
		{
			b.EnemyRecoveryTurn();
		}
		b.RoundCanEnd = true;
		Render();
	}
	
	private void OnEndRound()
	{	
		if (!_battle.RoundCanEnd)
		{
			Log("Round can not be ended");
		}
		else 
		{
			_battle.EndRound(_cardDb);
			_battle.StartRound();
			Render();
		}
	}

	private void Render()
	{
		RenderHand();
		RenderPiles();
		RenderBars();
		RenderSlots();
		RenderStats();
	}

	private void RenderHand()
	{
		
		var p = _battle.Player;
		var container = GetNode<VBoxContainer>("PlayerUI/HandContainer");

		//Clear old buttons
		foreach (var child in container.GetChildren())
		child.QueueFree();

		//Create one button for each card in hand
		for (int i = 0; i < p.Hand.Count; i++)
		{
			int index = i; // capture for closure
			var btn = new Button();
			btn.Text = p.Hand[i];
			btn.Pressed += () => OnSelectCard(index);
			container.AddChild(btn);
		}
		
		var e = _battle.Enemy;
		string enemytext = "HAND:\n";
		for (int i = 0; i < e.Hand.Count; i++)
			enemytext += $"{i+1}: {e.Hand[i]}\n";

		GetNode<Label>("PlayerUI/EnemyHandLabel").Text = enemytext;
	}

	private void RenderPiles()
	{
		var p = _battle.Player;
		GetNode<Label>("PlayerUI/PileLabel").Text =
			$"Draw: {p.DrawPile.Count}  Hand: {p.Hand.Count}  Discard: {p.DiscardPile.Count}";
		var e = _battle.Enemy;
		GetNode<Label>("PlayerUI/EnemyPileLabel").Text =
			$"Draw: {e.DrawPile.Count}  Hand: {e.Hand.Count}  Discard: {e.DiscardPile.Count}";
	}
	private void RenderBars()
	{
		//Renders progress bar value to represent the players HP
		GetNode<ProgressBar>("PlayerUI/PlayerHpBar").Value = _battle.Player.Hp;
		GetNode<ProgressBar>("PlayerUI/EnemyHpBar").Value = _battle.Enemy.Hp;
		
		//Renders the labels on the progress bar to display the value
		GetNode<Label>("PlayerUI/PlayerHpBar/PlayerHpText").Text = $"{_battle.Player.Hp} HP";
		GetNode<Label>("PlayerUI/EnemyHpBar/EnemyHpText").Text = $"{_battle.Enemy.Hp} HP";
	}
	
	private void RenderSlots()
	{
		var p = _battle.Player;
		var e = _battle.Enemy;

		GetNode<Label>("PlayerUI/PlayerCardSlots/CardSlot1").Text = p.CardsPlayed.Count > 0 ? p.CardsPlayed[0] : "";
		GetNode<Label>("PlayerUI/PlayerCardSlots/CardSlot2").Text = p.CardsPlayed.Count > 1 ? p.CardsPlayed[1] : "";

		GetNode<Label>("PlayerUI/EnemyCardSlots/CardSlot1").Text = e.CardsPlayed.Count > 0 ? e.CardsPlayed[0] : "";
		GetNode<Label>("PlayerUI/EnemyCardSlots/CardSlot2").Text = e.CardsPlayed.Count > 1 ? e.CardsPlayed[1] : "";
	}
	
	private void RenderStats()
	{
		var p = _battle.Player;
		var e = _battle.Enemy;
		
		//display player stats
		GetNode<Label>("PlayerUI/PlayerStats/PlayerAttack").Text = $"Attack: {p.BaseAttack}";
		if (p.BonusAttack != 0)
		{
			GetNode<Label>("PlayerUI/PlayerStats/PlayerAttack").Text += $" + {p.BonusAttack} Bonus";
		}
		GetNode<Label>("PlayerUI/PlayerStats/PlayerDefense").Text = $" Defense: {p.BaseDefense}";
		if (p.BonusDefense != 0)
		{
			GetNode<Label>("PlayerUI/PlayerStats/PlayerDefense").Text += $" + {p.BonusDefense} Bonus";
		}
		
		//display enemy stats
		GetNode<Label>("PlayerUI/EnemyStats/EnemyAttack").Text = $"Attack: {e.BaseAttack}";
		if (e.BonusAttack != 0)
		{
			GetNode<Label>("PlayerUI/EnemyStats/EnemyAttack").Text += $" + {e.BonusAttack} Bonus";
		}
		GetNode<Label>("PlayerUI/EnemyStats/EnemyDefense").Text = $"Defense: {e.BaseDefense}";
		if (e.BonusDefense != 0)
		{
			GetNode<Label>("PlayerUI/EnemyStats/EnemyDefense").Text += $" + {e.BonusDefense} Bonus";
		}
	}

	private void Log(string msg)
	{
		if (msg == LastLogMessage)
		{
			_duplicateCount++;
			_logLines[_logLines.Count-1] = $"{msg} (x{_duplicateCount})";
			_Log.Text = string.Join("\n", _logLines);
			return;
		}
		else
		{
			_duplicateCount = 1;
			_logLines.Add(msg);
			_Log.Text = string.Join("\n", _logLines);
			LastLogMessage = msg;
		}
	}
}
