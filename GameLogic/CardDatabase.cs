#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace GameLogic;

public sealed partial class CardDatabase : Node
{
	public readonly Dictionary<string, CardDefinition> Cards = new();

	public override void _Ready()
	{
		LoadAllCards();
	}

	private void LoadAllCards()
	{
		DirAccess dir = DirAccess.Open("res://Data/Cards");
		if (dir == null)
		{
			GD.PushWarning("CardDatabase: Could not open res://Data/Cards");
			return;
		}
	dir.ListDirBegin();
		while (true)
		{
			string file = dir.GetNext();
			if (file == "") break;
			if (dir.CurrentIsDir()) continue;
			if (!file.EndsWith(".json")) continue;

			string path = $"res://Data/Cards/{file}";
			string json = FileAccess.GetFileAsString(path);

			  CardDefinition? card = JsonSerializer.Deserialize<CardDefinition>(json);

			  if (card == null || string.IsNullOrWhiteSpace(card.id))
			  {
				  GD.PushWarning($"CardDatabase: Invalid card data in {path}");
				  continue;
			  }

			  if (Cards.ContainsKey(card.id))
			  {
				  GD.PushWarning($"Duplicate card id: {card.id} in {path}");
			  }
			  else
			  {
				  Cards[card.id] = card;
			  }
		}
		dir.ListDirEnd();
		
		GD.Print($"CardDatabase: Loaded {Cards.Count} cards.");
	}
	public CardDefinition? GetCard(string id)
	{
		return Cards.TryGetValue(id, out var card) ? card : null;
	}
}
