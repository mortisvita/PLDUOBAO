using System;

namespace PLDUOBAO.Util;

/// <summary>
/// 国际服风格随机名字生成器
/// </summary>
internal static class InternationalNameGenerator
{
    private static readonly Random _random = new();
    
    private static readonly string[] Names = 
    {
        // 常见英文名
        "Alex", "Anna", "Ben", "Chris", "Dan", "Emma", "Finn", "Grace", "Jack", "Jake",
        "John", "Kate", "Leo", "Lily", "Luke", "Max", "Mia", "Nick", "Noah", "Ryan",
        "Sam", "Tom", "Zoe", "Adam", "Amy", "Bob", "Cody", "Dave", "Ella", "Eva",
        "Fred", "Gus", "Hank", "Ivy", "Jay", "Ken", "Liam", "Luna", "Mark", "Nate",
        "Owen", "Paul", "Quinn", "Rex", "Sean", "Theo", "Uma", "Vic", "Will", "Xander",
        "Yuki", "Zack", "Kyle", "Matt", "Mike", "Steve", "James", "Tyler", "Eric", "Kevin",
        "Brian", "Jason", "Justin", "Aaron", "Dylan", "Cole", "Ethan", "Logan", "Mason", "Lucas",
        
        "Ace", "Ash", "Bear", "Blaze", "Blade", "Bolt", "Buzz", "Claw", "Doom", "Duke",
        "Edge", "Fang", "Fury", "Ghost", "Hawk", "Ice", "Jet", "King", "Knox", "Lynx",
        "Mage", "Nero", "Nova", "Onyx", "Puma", "Rage", "Raven", "Reaper", "Riot", "Rock",
        "Rush", "Scar", "Shadow", "Skull", "Slash", "Snake", "Spark", "Steel", "Storm", "Tank",
        "Thor", "Titan", "Venom", "Viper", "Wolf", "Zero", "Zion", "Zoom", "Frost", "Flame",
        "Pixel", "Craft", "Block", "Mine", "Ender", "Nether", "Creep", "Zombie", "Skele", "Wither",
        "Dragon", "Slime", "Golem", "Blaze", "Ghast", "Piglin", "Shulk", "Raid", "Loot", "Ore",
        "Diamond", "Gold", "Iron", "Emerald", "Redstone", "Obsidian", "Netherite", "Bedrock",
        "Sword", "Axe", "Pick", "Bow", "Arrow", "Shield", "Armor", "Helm", "Elytra", "Trident",
        "Ninja", "Samurai", "Knight", "Wizard", "Archer", "Hunter", "Warrior", "Rogue", "Mage", "Healer",
        "Sniper", "Bomber", "Gamer", "Player", "Noob", "Pro", "Legend", "Master", "Chief", "Boss",
        "Toxic", "Salty", "Clutch", "Sweat", "Tryhard", "Camper", "Rusher", "Builder", "Pvp", "Pve",
        "Dream", "Techno", "Sapnap", "George", "Tommy", "Tubbo", "Ranboo", "Wilbur", "Phil", "Fundy",
        "Skeppy", "Bad", "Quackity", "Karl", "Punz", "Purpled", "Foolish", "Antfrost", "Awesom", "Hbomb",
        "Cyber", "Neon", "Glitch", "Pixel", "Byte", "Data", "Code", "Hack", "Virus", "Bot",
        "Cosmic", "Astro", "Solar", "Lunar", "Star", "Comet", "Meteor", "Galaxy", "Nebula", "Void",
        "Alpha", "Beta", "Omega", "Delta", "Sigma", "Gamma", "Theta", "Zeta", "Epsilon", "Lambda"
    };
    
    // 前缀
    private static readonly string[] Prefixes = 
    {
        "Pro", "xX", "Mr", "Dr", "Sir", "The", "Dark", "Epic", "God", "King",
        "Lord", "Master", "Mega", "Neo", "Ninja", "Super", "Ultra", "xx", "Xx", "OP",
        "Ez", "Im", "Its", "Just", "Not", "Real", "True", "Big", "Lil", "OG",
        "Mc", "Mine", "Craft", "Block", "Pixel", "iAm", "itz", "Da", "El", "La",
        "No", "So", "My", "Ur", "Hi", "Hey", "Yo", "Sup", "Bruh", "Bro",
        "Cool", "Hot", "Cold", "Fast", "Slow", "Old", "New", "Best", "Top", "Low",
        "High", "Mad", "Sad", "Bad", "Rad", "Sick", "Lit", "Dope", "Fire", "Ice"
    };
    
    // 后缀
    private static readonly string[] Suffixes = 
    {
        "Xx", "xX", "HD", "TV", "YT", "TTV", "BTW", "GG", "OP", "Pro",
        "God", "King", "Boss", "Chief", "Dude", "Guy", "Man", "Boy", "Kid", "Jr",
        "MC", "PvP", "HQ", "VIP", "MVP", "OG", "EU", "NA", "JP", "KR",
        "Plays", "Gaming", "Live", "Stream", "Clips", "Wins", "Frags", "Kills", "Shots", "Hits",
        "OwO", "UwU", "QwQ", "TwT", "XD", "Lol", "Lmao", "Gg", "Ez", "Wp",
        "_", "__", "x", "z", "v", "ii", "iii", "IV", "V", "X"
    };

    public static string Generate()
    {
        int style = _random.Next(6);
        
        return style switch
        {
            // 风格1: Name + Name + 数字 (AnnaJonny717)
            0 => $"{Names[_random.Next(Names.Length)]}{Names[_random.Next(Names.Length)]}{_random.Next(10, 999)}",
            
            // 风格2: 前缀 + Name + 数字 (ProMagun997)
            1 => $"{Prefixes[_random.Next(Prefixes.Length)]}{Names[_random.Next(Names.Length)]}{_random.Next(10, 999)}",
            
            // 风格3: xX + Name + Xx (xXImGayXx 风格)
            2 => $"xX{Names[_random.Next(Names.Length)]}{Names[_random.Next(Names.Length)]}Xx",
            
            // 风格4: Name + 数字 (Alex2024)
            3 => $"{Names[_random.Next(Names.Length)]}{_random.Next(1, 9999)}",
            
            // 风格5: 前缀 + Name + 后缀 (ProAlexGG)
            4 => $"{Prefixes[_random.Next(Prefixes.Length)]}{Names[_random.Next(Names.Length)]}{Suffixes[_random.Next(Suffixes.Length)]}",
            
            // 风格6: Name + Name (JackStorm)
            _ => $"{Names[_random.Next(Names.Length)]}{Names[_random.Next(Names.Length)]}"
        };
    }
}
