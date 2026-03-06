#nullable enable
using System;
using System.Collections.Generic;

namespace GameLogic;

public sealed class CardDefinition
{
	public string id { get; set;} = "";
	public string name { get; set;} = "";
	public string element { get; set;} = "";
	public string rarity { get; set; } = "";
	public int priority { get; set; } = 0;
	public List<CardEffect> effects { get; set; } =new();
}

public class CardEffect
{
	public string type { get; set;} = "";
	public string stat { get; set;} = "";
	
	public int? amount { get; set;}
	public string mode { get; set;} = "";
	
	public string? condition { get; set; } = "";
	
	public string? target { get; set; } = "";
	public string? card { get; set;} = "";
}
