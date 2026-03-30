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
		_cardDb = GetNode<CardDatabase>("/root/CardDb");
		_battle.Initialize(_cardDb);
		
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
	
	private Button CreateCardButton(string cardId)
	{
		var cardDef = _cardDb.GetCard(cardId);
		var btn = new Button();
		btn.CustomMinimumSize = new Vector2(100, 150);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.Text = cardDef != null ? cardDef.name : cardId;

		Texture2D cardTexture = null;
		string texturePath = $"res://Assets/Cards/{cardId}.png";
		if (ResourceLoader.Exists(texturePath))
			cardTexture = GD.Load<Texture2D>(texturePath);
		if (cardTexture != null)
		{
			btn.Icon = cardTexture;
			btn.ExpandIcon = true;
			btn.Text = "";
		}
		else if (cardDef != null)
		{
			var baseColor = GetElementColor(cardDef.element);
			var style = new StyleBoxFlat();
			style.BgColor = baseColor;
			btn.AddThemeStyleboxOverride("normal", style);
			btn.AddThemeStyleboxOverride("hover", style.BgColor != null ? new StyleBoxFlat { BgColor = baseColor.Darkened(0.15f) } : null);
			btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = baseColor.Darkened(0.3f) });
			btn.AddThemeStyleboxOverride("disabled", style);
		}

		return btn;
	}

	private void RenderHand()
	{
		
		var p = _battle.Player;
		var container = GetNode<HBoxContainer>("PlayerUI/HandContainer");

		//Clear old buttons
		foreach (var child in container.GetChildren())
		child.QueueFree();

		//Create one button for each card in hand
		for (int i = 0; i < p.Hand.Count; i++)
		{
			var index = i;
			var btn = CreateCardButton(p.Hand[i]);
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
		
		var playerSlots = GetNode<HBoxContainer>("PlayerUI/PlayerCardSlots");
		var enemySlots = GetNode<HBoxContainer>("PlayerUI/EnemyCardSlots");
		
		foreach (var child in playerSlots.GetChildren())
		child.QueueFree();
		foreach (var child in enemySlots.GetChildren())
		child.QueueFree();
		
		foreach (var cardId in p.CardsPlayed)
			playerSlots.AddChild(CreateCardButton(cardId));

		foreach (var cardId in e.CardsPlayed)
			enemySlots.AddChild(CreateCardButton(cardId));
	}
	
	private string FormatStat(string name, int base_, int bonus)
	{
		int total = base_ + bonus;
		if (bonus == 0) return $"{name}: {total}";;
		string modifier = bonus > 0 ? $"{base_} + {bonus} Bonus" : $"{base_} - {-bonus} Debuff";
		return $"{name}: {total} ({modifier})";
	}
	private void RenderStats()
	{
		var p = _battle.Player;
		var e = _battle.Enemy;
		
		//display player stats
		//attack
		GetNode<Label>("PlayerUI/PlayerStats/PlayerAttack").Text  = FormatStat("Attack",  p.BaseAttack,  p.BonusAttack);
		GetNode<Label>("PlayerUI/PlayerStats/PlayerDefense").Text = FormatStat("Defense", p.BaseDefense, p.BonusDefense);
		GetNode<Label>("PlayerUI/EnemyStats/EnemyAttack").Text    = FormatStat("Attack",  e.BaseAttack,  e.BonusAttack);
		GetNode<Label>("PlayerUI/EnemyStats/EnemyDefense").Text   = FormatStat("Defense", e.BaseDefense, e.BonusDefense);
	}
	
	// Sets colours of cards for each element
	private Color GetElementColor(string element)
	{
	switch (element)
		{
			case "Flame": return new Color(0.75f, 0f, 0f);
			case "Earth": return new Color(0.6f, 0.3f, 0);
			default:      return new Color(0.3f, 0.3f, 0.3f);
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
