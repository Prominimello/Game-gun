namespace Breachpoint.Core.Data;

/// <summary>Static game content: weapons, attachments, gear, operator options.</summary>
public static class GameData
{
    public static readonly Dictionary<string, Caliber> Calibers = new[]
    {
        new Caliber("5.56", "5.56×45mm", 34, 32, 1.0),
        new Caliber("5.45", "5.45×39mm", 32, 34, 0.9),
        new Caliber("7.62", "7.62×51mm", 47, 46, 1.8),
        new Caliber("9mm", "9×19mm", 25, 14, 0.5),
        new Caliber(".45", ".45 ACP", 31, 11, 0.7),
        new Caliber("4.6", "4.6×30mm", 22, 32, 1.0),
        new Caliber("5.7", "5.7×28mm", 22, 30, 1.0),
        new Caliber("12g", "12 Gauge", 13, 6, 1.2, 9),
        new Caliber(".338", ".338 Lapua", 78, 66, 4.0),
    }.ToDictionary(c => c.Id);

    public static readonly Dictionary<string, WeaponCategory> Categories = new[]
    {
        new WeaponCategory("ar", "Assault Rifles", 2.4, new[] { "optic", "muzzle", "under", "side", "stock", "grip", "hand", "barrel", "mag", "sling" }),
        new WeaponCategory("smg", "Submachine Guns", 2.1, new[] { "optic", "muzzle", "under", "side", "stock", "grip", "mag", "sling" }),
        new WeaponCategory("br", "Battle Rifles", 2.8, new[] { "optic", "muzzle", "under", "side", "stock", "grip", "hand", "barrel", "mag", "sling" }),
        new WeaponCategory("sg", "Shotguns", 0.5, new[] { "optic", "side", "stock", "barrel", "mag", "sling" }),
        new WeaponCategory("sr", "Sniper Rifles", 3.2, new[] { "optic", "muzzle", "under", "stock", "barrel", "mag", "sling" }),
        new WeaponCategory("hg", "Handguns", 1.6, new[] { "optic", "muzzle", "side", "mag" }),
    }.ToDictionary(c => c.Id);

    public static readonly string[] CategoryOrder = { "ar", "smg", "br", "sg", "sr", "hg" };

    public static readonly List<WeaponDef> Weapons = new()
    {
        new("m4a1", "M4A1", "ar", "5.56", "auto", 800, 30, 2.2, 42, 68, 3.1, 85, 1400, 1, new(30, 34, "collapsible", "stanag", 30, Rail: true)),
        new("hk416", "HK416", "ar", "5.56", "auto", 850, 30, 2.0, 38, 66, 3.5, 95, 2600, 2, new(32, 32, "collapsible", "stanag", 32, Rail: true)),
        new("scarl", "SCAR-L", "ar", "5.56", "auto", 625, 30, 2.0, 36, 64, 3.3, 92, 2400, 2, new(40, 30, "folding", "stanag", 26, Rail: true, Bulky: true)),
        new("mcx", "MCX", "ar", "5.56", "auto", 850, 30, 2.1, 35, 74, 2.9, 88, 3200, 3, new(30, 28, "folding", "stanag", 30, Rail: true)),
        new("ak105", "AK-105", "ar", "5.45", "auto", 600, 30, 2.6, 48, 62, 3.2, 97, 1100, 1, new(36, 28, "folding", "curved", 22, Ak: true)),
        new("g36c", "G36C", "ar", "5.56", "auto", 750, 30, 2.3, 34, 72, 2.8, 88, 1900, 2, new(36, 22, "folding", "translucent", 22, Rail: true, Bulky: true)),
        new("mp5", "MP5", "smg", "9mm", "auto", 800, 30, 2.6, 22, 82, 2.5, 88, 900, 1, new(32, 12, "collapsible", "curvedsmg", 18)),
        new("mp7", "MP7", "smg", "4.6", "auto", 950, 40, 2.4, 20, 88, 1.9, 90, 2200, 2, new(26, 10, "pdw", "grip", 12, Rail: true)),
        new("ump45", "UMP45", "smg", ".45", "auto", 600, 25, 2.8, 28, 80, 2.3, 90, 1200, 1, new(34, 12, "folding", "box", 16, Rail: true, Bulky: true)),
        new("p90", "P90", "smg", "5.7", "auto", 900, 50, 2.5, 18, 84, 2.6, 86, 2600, 3, new(46, 6, "bullpup", "top", 0, Rail: true, Bulky: true)),
        new("vector", "Vector", "smg", ".45", "auto", 1200, 25, 2.7, 15, 86, 2.7, 82, 3000, 3, new(34, 10, "folding", "grip", 14, Rail: true, Bulky: true)),
        new("scarh", "SCAR-H", "br", "7.62", "auto", 600, 20, 1.6, 60, 52, 3.9, 92, 3600, 2, new(42, 36, "folding", "box", 28, Rail: true, Bulky: true)),
        new("hk417", "HK417", "br", "7.62", "auto", 600, 20, 1.5, 58, 50, 4.4, 94, 4200, 3, new(34, 40, "collapsible", "box", 34, Rail: true)),
        new("sr25", "SR-25", "br", "7.62", "semi", 300, 20, 1.2, 55, 48, 4.9, 90, 4600, 3, new(34, 46, "fixed", "box", 36, Rail: true)),
        new("fal", "FAL", "br", "7.62", "auto", 650, 20, 1.9, 66, 46, 4.3, 86, 2200, 2, new(40, 40, "fixed", "box", 24, Wood: true)),
        new("m4super", "Benelli M4", "sg", "12g", "semi", 300, 7, 5.5, 70, 60, 3.8, 90, 1900, 2, new(36, 40, "collapsible", "tube", 26, Rail: true)),
        new("r870", "Remington 870", "sg", "12g", "pump", 70, 6, 6.5, 72, 58, 3.6, 96, 500, 1, new(34, 42, "fixed", "tube", 22, Wood: true)),
        new("m590", "Mossberg 590", "sg", "12g", "pump", 75, 8, 6.2, 70, 58, 3.4, 96, 700, 1, new(34, 44, "fixed", "tube", 22)),
        new("m24", "M24", "sr", "7.62", "bolt", 45, 5, 0.4, 80, 30, 5.4, 96, 3000, 2, new(40, 54, "fixed", "internal", 40, Bolt: true)),
        new("axmc", "AXMC", "sr", ".338", "bolt", 40, 5, 0.3, 85, 28, 6.8, 97, 7500, 4, new(44, 56, "chassis", "box", 40, Rail: true, Bolt: true)),
        new("m110", "M110", "sr", "7.62", "semi", 200, 20, 0.7, 60, 38, 5.3, 90, 5200, 3, new(34, 48, "fixed", "box", 38, Rail: true)),
        new("g17", "Glock 17", "hg", "9mm", "semi", 400, 17, 3.2, 25, 90, 0.9, 92, 500, 1, new(22, 0, "none", "pistol", 0, Pistol: true)),
        new("g19", "Glock 19", "hg", "9mm", "semi", 400, 15, 3.3, 24, 92, 0.85, 92, 550, 1, new(20, 0, "none", "pistol", 0, Pistol: true)),
        new("p320", "SIG P320", "hg", "9mm", "semi", 400, 17, 3.0, 24, 90, 0.95, 93, 650, 1, new(22, 0, "none", "pistol", 0, Pistol: true)),
        new("usp45", "USP45", "hg", ".45", "semi", 360, 12, 2.9, 30, 86, 1.0, 95, 700, 2, new(24, 0, "none", "pistol", 0, Pistol: true)),
        new("fn57", "Five-seveN", "hg", "5.7", "semi", 420, 20, 3.0, 20, 90, 0.75, 90, 900, 2, new(23, 0, "none", "pistol", 0, Pistol: true)),
    };
    public static readonly Dictionary<string, WeaponDef> Weapon = Weapons.ToDictionary(w => w.Id);

    private static readonly string[] Long = { "ar", "br", "smg", "sg", "sr" };
    private static readonly string[] LongNoSg = { "ar", "br", "smg", "sr" };

    public static readonly List<AttachmentDef> Attachments = new()
    {
        new("rds", "optic", "Micro Red Dot", new[] { "ar", "smg", "br", "sg" }, 350, 1, new Mods { AimSpeed = 7, Accuracy = 4, Weight = 0.1 }, "Fast, unmagnified reflex sight."),
        new("holo", "optic", "Holographic Sight", new[] { "ar", "smg", "br", "sg" }, 650, 1, new Mods { AimSpeed = 9, Accuracy = 6, Weight = 0.3, Ergonomics = 2 }, "Large window, fast target transitions."),
        new("prism", "optic", "4× Prism Combat Optic", new[] { "ar", "br" }, 1200, 2, new Mods { Accuracy = 14, AimSpeed = -6, Weight = 0.4, Range = 3 }, "Fixed magnification. Extends engagement range."),
        new("lpvo", "optic", "1–6× Variable Optic", new[] { "ar", "br", "sr" }, 1800, 3, new Mods { Accuracy = 17, AimSpeed = -3, Weight = 0.6, Range = 4 }, "Close-quarters at 1×, precision at 6×."),
        new("scope", "optic", "5–25× Precision Scope", new[] { "sr", "br" }, 2200, 2, new Mods { Accuracy = 26, AimSpeed = -14, Handling = -6, Weight = 0.9, Range = 8 }, "Long-range magnified optic. Slow up close."),
        new("rmr", "optic", "Pistol Reflex Sight", new[] { "hg" }, 400, 1, new Mods { AimSpeed = 8, Accuracy = 6, Weight = 0.05 }, "Slide-mounted red dot."),
        new("brake", "muzzle", "Muzzle Brake", new[] { "ar", "br", "sr" }, 300, 1, new Mods { Recoil = -12, Visibility = 15, Suppression = -15 }, "Vents gas to reduce recoil. Loud."),
        new("comp", "muzzle", "Compensator", new[] { "ar", "br", "smg", "hg" }, 250, 1, new Mods { Recoil = -8, Accuracy = 2, Visibility = 6 }, "Reduces muzzle climb."),
        new("supp", "muzzle", "Rifle Suppressor", new[] { "ar", "br", "sr" }, 1100, 2, new Mods { Suppression = 70, Visibility = -40, Weight = 0.5, Handling = -6, Recoil = -4 }, "Greatly reduces report and flash."),
        new("psupp", "muzzle", "Pistol-Caliber Suppressor", new[] { "smg", "hg" }, 600, 1, new Mods { Suppression = 75, Visibility = -40, Weight = 0.25, Handling = -4, Recoil = -3 }, "Quiet, flashless fire with pistol calibers."),
        new("vfg", "under", "Vertical Foregrip", LongNoSg, 120, 1, new Mods { Recoil = -8, Handling = -2, Weight = 0.15 }, "Strong recoil control."),
        new("afg", "under", "Angled Foregrip", LongNoSg, 150, 1, new Mods { Recoil = -4, AimSpeed = 4, Ergonomics = 5, Weight = 0.1 }, "Natural wrist angle, faster on target."),
        new("stop", "under", "Hand Stop", LongNoSg, 60, 1, new Mods { Recoil = -3, Ergonomics = 6, Weight = 0.03 }, "Index point for a consistent grip."),
        new("bipod", "under", "Bipod", new[] { "br", "sr" }, 350, 2, new Mods { Accuracy = 7, Handling = -10, Weight = 0.4 }, "Stable when braced. Clumsy indoors."),
        new("light", "side", "Weapon Light", new[] { "ar", "smg", "br", "sg", "hg" }, 200, 1, new Mods { Light = 1, Visibility = 10, Weight = 0.15 }, "Illuminates dark rooms. Makes you visible."),
        new("laser", "side", "Laser Module", new[] { "ar", "smg", "br", "sg", "hg" }, 350, 1, new Mods { Accuracy = 5, AimSpeed = 5, Visibility = 5, Weight = 0.12 }, "Visible aiming laser."),
        new("combo", "side", "Light/Laser Combo", new[] { "ar", "smg", "br", "sg", "hg" }, 600, 2, new Mods { Light = 1, Accuracy = 5, AimSpeed = 5, Visibility = 12, Weight = 0.25 }, "Both in one housing."),
        new("ctr", "stock", "Adjustable Stock", new[] { "ar", "smg", "br", "sg" }, 180, 1, new Mods { Ergonomics = 5, Recoil = -4, Weight = 0.05 }, "Better cheek weld and shoulder fit."),
        new("pdw", "stock", "PDW Stock", new[] { "ar", "smg" }, 450, 2, new Mods { Handling = 9, AimSpeed = 6, Recoil = 6, Weight = -0.2 }, "Compact. Faster, kicks harder."),
        new("precstock", "stock", "Precision Stock", new[] { "ar", "br", "sr" }, 600, 2, new Mods { Accuracy = 6, Recoil = -8, Handling = -6, Weight = 0.4 }, "Adjustable comb and length of pull."),
        new("ergo", "grip", "Ergonomic Grip", new[] { "ar", "smg", "br" }, 90, 1, new Mods { Ergonomics = 6, AimSpeed = 2 }, "Comfortable texture and angle."),
        new("steep", "grip", "Steep-Angle Grip", new[] { "ar", "smg", "br" }, 110, 1, new Mods { Handling = 4, Ergonomics = 3 }, "Suits a compressed, square stance."),
        new("rubber", "grip", "Rubberized Grip", new[] { "ar", "smg", "br" }, 70, 1, new Mods { Recoil = -2, Ergonomics = 2 }, "Sure grip with wet gloves."),
        new("mlok", "hand", "Free-Float Handguard", new[] { "ar", "br" }, 400, 1, new Mods { Accuracy = 4, Weight = -0.1, Ergonomics = 2 }, "Barrel does not touch the handguard."),
        new("shorthg", "hand", "Short Handguard", new[] { "ar", "br" }, 300, 1, new Mods { Handling = 6, Accuracy = -2, Weight = -0.15 }, "Compact, CQB-friendly."),
        new("longhg", "hand", "Extended Handguard", new[] { "ar", "br" }, 380, 1, new Mods { Recoil = -3, Accuracy = 3, Handling = -4, Weight = 0.15 }, "More real estate, better leverage."),
        new("shortb", "barrel", "Short Barrel (CQB)", new[] { "ar", "br", "sg" }, 500, 1, new Mods { Handling = 10, Accuracy = -8, Damage = -5, Weight = -0.3, Suppression = -5 }, "Shorter and handier, lower velocity."),
        new("longb", "barrel", "Long Barrel", new[] { "ar", "br", "sr", "sg" }, 600, 2, new Mods { Accuracy = 10, Handling = -8, Damage = 5, Weight = 0.4 }, "Higher velocity and precision."),
        new("heavyb", "barrel", "Heavy Profile Barrel", new[] { "ar", "br", "sr" }, 700, 2, new Mods { Reliability = 5, Accuracy = 6, Handling = -6, Weight = 0.6 }, "Holds zero under sustained fire."),
        new("extmag", "mag", "Extended Magazine", new[] { "ar", "smg", "br", "hg", "sg", "sr" }, 150, 1, new Mods { MagMult = 0.5, Handling = -4, Reliability = -3, Weight = 0.2 }, "+50% capacity."),
        new("coupler", "mag", "Magazine Coupler", new[] { "ar", "smg", "br" }, 120, 1, new Mods { Ergonomics = 10, Weight = 0.3 }, "Two mags clamped side-by-side. Faster reloads."),
        new("drum", "mag", "Drum Magazine", new[] { "ar", "smg" }, 900, 3, new Mods { MagMult = 1.0, Handling = -10, Reliability = -8, Weight = 0.9 }, "Double capacity. Heavy and finicky."),
        new("sling1", "sling", "Single-Point Sling", Long, 60, 1, new Mods { Handling = 3, Ergonomics = 2 }, "Quick transitions."),
        new("sling2", "sling", "Two-Point Sling", Long, 70, 1, new Mods { Ergonomics = 4, Recoil = -1 }, "Stable carry, adjustable."),
        new("sling3", "sling", "Three-Point Sling", Long, 80, 1, new Mods { Ergonomics = 2, Handling = 1 }, "Old-school, secure."),
    };
    public static readonly Dictionary<string, AttachmentDef> Attachment = Attachments.ToDictionary(a => a.Id);

    public static readonly Dictionary<string, string> SlotNames = new()
    {
        ["optic"] = "Optic", ["muzzle"] = "Muzzle", ["under"] = "Underbarrel", ["side"] = "Rail (side)", ["stock"] = "Stock",
        ["grip"] = "Pistol Grip", ["hand"] = "Handguard", ["barrel"] = "Barrel", ["mag"] = "Magazine", ["sling"] = "Sling",
    };

    public static readonly List<FinishDef> Finishes = new()
    {
        new("black", "Black", "#1f2124"), new("fde", "Flat Dark Earth", "#9a8361"), new("od", "OD Green", "#4f5a3a"), new("grey", "Tactical Grey", "#62676c"),
        new("multi", "Multi-Terrain Camo", "#8a7b5b", new[] { "#5e5a3d", "#a8946b", "#3f3b2b", "#71704f", "#c3b28a" }),
        new("urban", "Urban Camo", "#6d7278", new[] { "#2c2f33", "#9ca1a6", "#4a4e53" }),
        new("wood", "Woodland Camo", "#5b6340", new[] { "#2c2a1c", "#7b6a43", "#3d4a2b" }),
        new("custom", "Custom Camo", "#555555", Array.Empty<string>()),
    };
    public static readonly Dictionary<string, FinishDef> Finish = Finishes.ToDictionary(f => f.Id);

    public static readonly List<AmmoDef> Ammo = new()
    {
        new("fmj", "Standard (FMJ)", 1.0, 1.0, 1.0, 0, "Balanced full metal jacket."),
        new("jhp", "Hollow Point (JHP)", 1.25, 0.6, 1.4, 2500, "High stopping power, poor vs armor."),
        new("ap", "Armor Piercing (AP)", 0.92, 1.6, 2.2, 6000, "Defeats body armor. Expensive."),
        new("sub", "Subsonic", 0.9, 0.85, 1.5, 3000, "Much quieter through a suppressor."),
    };
    public static readonly Dictionary<string, AmmoDef> AmmoById = Ammo.ToDictionary(a => a.Id);

    public static readonly List<ArmorDef> Armors = new()
    {
        new("none", "No Armor", 0, 1.04, 1.0, 0), new("light", "Lightweight Carrier", 30, 1.0, 1.0, 0), new("standard", "Standard Plates", 55, 0.93, 1.15, 6000),
        new("heavy", "Heavy Plates", 75, 0.85, 1.35, 14000), new("full", "Full Ballistic Kit", 90, 0.74, 1.6, 26000),
    };
    public static readonly Dictionary<string, ArmorDef> Armor = Armors.ToDictionary(a => a.Id);

    public static readonly List<HelmetDef> Helmets = new()
    {
        new("none", "None", 0, 0), new("bump", "Bump Helmet", 12, 0), new("ballistic", "Ballistic Helmet", 40, 4000), new("heavyhelm", "Heavy Helmet + Visor", 70, 11000, 0.97),
    };
    public static readonly Dictionary<string, HelmetDef> Helmet = Helmets.ToDictionary(a => a.Id);

    public static readonly List<ItemDef> Items = new()
    {
        new("flash", "Flashbang", "util", 60, "Stuns everyone in line of sight.", "FB"),
        new("smoke", "Smoke Grenade", "util", 40, "Blocks line of sight for ~25s.", "SM"),
        new("gas", "Tear Gas", "util", 75, "Suspects choke, lose accuracy, surrender more.", "CS"),
        new("stinger", "Diversionary Device", "util", 50, "Loud distraction. Draws suspects toward it.", "DV"),
        new("charge", "Breaching Charge", "util", 150, "Destroys a door, stuns those behind it.", "C4"),
        new("medkit", "Medical Kit", "tool", 300, "Revive downed operators, treat wounds. 3 uses.", "MED"),
        new("lockpick", "Lockpick Kit", "tool", 400, "Silently open locked doors.", "PICK"),
        new("doorcam", "Door Inspection Camera", "tool", 900, "See under a door before entering.", "CAM"),
        new("shield", "Ballistic Shield", "tool", 2500, "Stops frontal fire. Pistol only. Slow.", "SHLD"),
        new("ram", "Battering Ram", "tool", 350, "Reliable manual breach of locked doors.", "RAM"),
    };
    public static readonly Dictionary<string, ItemDef> Item = Items.ToDictionary(i => i.Id);

    public static readonly List<VehicleDef> Vehicles = new()
    {
        new("suv", "Unmarked SUV", 4, 0, 4.6, 1.9, "#20252b", false),
        new("psuv", "Police Interceptor SUV", 5, 9000, 4.8, 2.0, "#e7e9ec", false),
        new("van", "Tactical Van", 6, 18000, 5.6, 2.1, "#2b2f35", false),
        new("truck", "Armored Tactical Truck", 8, 48000, 6.2, 2.5, "#1d2125", true),
        new("transport", "Personnel Transport", 8, 30000, 6.8, 2.4, "#3a3f2f", false),
    };
    public static readonly Dictionary<string, VehicleDef> Vehicle = Vehicles.ToDictionary(v => v.Id);

    public static readonly List<UniformDef> Uniforms = new()
    {
        new("swat", "SWAT", "#1c2430", "#1c2430", "#141a22", "#151a20", "#111111", "POLICE"),
        new("fed", "Federal Tactical", "#2a2d33", "#6d6a55", "#2f3e31", "#2a2d30", "#2a2a2a", "FEDERAL"),
        new("sof", "Special Operations", "#7d7356", "#7d7356", "#4b5640", "#6b6650", "#5e5440", "TF", true),
        new("ct", "Counterterrorism", "#131417", "#131417", "#0d0e10", "#0f1012", "#0a0a0a", "CT"),
        new("uc", "Undercover", "#5a3b3b", "#2d3a55", "#2a2a2a", null, "#333333", ""),
        new("plain", "Plainclothes", "#365a7a", "#a89a78", "#2b2b2b", null, "#333333", "POLICE"),
        new("heavy", "Heavy Armor", "#2c3036", "#2c3036", "#1f2226", "#22262a", "#111111", "TF"),
        new("recon", "Light Recon", "#4c5538", "#5a5a42", "#4a5236", "#4c5538", "#3d3a2c", "RECON"),
    };
    public static readonly Dictionary<string, UniformDef> Uniform = Uniforms.ToDictionary(u => u.Id);

    public static readonly string[] Skin = { "#f2d0b5", "#e5b896", "#d29e78", "#b07a52", "#8d5a3a", "#6a4129", "#4a2c1c" };
    public static readonly string[] HairColors = { "#1a1310", "#3b2618", "#5e3b22", "#8a6038", "#c79a5a", "#d9c08a", "#6b6b6b", "#b8b8b8", "#7a2a1a" };
    public static readonly string[] Hair = { "Buzz", "Short", "Crew", "Side Part", "Slick Back", "Bun", "Ponytail", "Long", "Shaved", "Curly", "Braids" };
    public static readonly string[] Facial = { "None", "Stubble", "Mustache", "Goatee", "Short Beard", "Full Beard" };
    public static readonly string[] Faces = { "Angular", "Round", "Square", "Narrow", "Broad", "Weathered" };
    public static readonly string[] Builds = { "Lean", "Average", "Athletic", "Heavy" };
    public static readonly string[] EyePro = { "None", "Shooting Glasses", "Goggles", "Dark Lenses" };
    public static readonly string[] Headset = { "None", "Comms Earpiece", "Active Headset" };
    public static readonly string[] Gloves = { "Black", "Coyote", "Olive", "Grey" };
    public static readonly string[] Boots = { "Black", "Coyote", "Olive", "Grey" };
    public static readonly string[] Kneepads = { "None", "Black", "Coyote" };
    public static readonly string[] Watch = { "None", "Digital", "Analog" };
    public static readonly string[] Patches = { "None", "Flag", "Unit Crest", "Blood Type", "Callsign", "Skull", "Medic Cross" };

    public static readonly List<StatDef> Stats = new()
    {
        new("acc", "Accuracy", "Weapon handling and precision"), new("awa", "Awareness", "Detecting threats, reaction time"),
        new("mob", "Mobility", "Movement speed"), new("end", "Endurance", "Fatigue resistance, carrying heavy armor"),
        new("dis", "Discipline", "Following ROE, command response"), new("med", "Medical", "Treating injuries"),
        new("tec", "Technical", "Locks, charges and electronics"), new("lea", "Leadership", "Squad coordination and morale"),
    };

    public static readonly List<PersonalityDef> Personalities = new()
    {
        new("calm", "Calm Under Pressure", "Stress builds slowly.", 0.5, 0, 0, 1),
        new("aggr", "Aggressive", "Fast and decisive, sometimes reckless.", 0.9, -0.08, -2, 1.06, -10),
        new("meth", "Methodical", "Slow but very precise.", 0.8, 0.03, 6, 0.95),
        new("exp", "Experienced", "Seen it all before.", 0.7, -0.06, 3, 1),
        new("inexp", "Inexperienced", "Still learning the job.", 1.3, 0.1, -4, 1),
        new("stressed", "Easily Stressed", "Performance drops quickly under fire.", 1.7, 0.02, 0, 1),
        new("leader", "Natural Leader", "Squad morale and coordination boost.", 0.8, 0, 0, 1, 0, true),
    };
    public static readonly Dictionary<string, PersonalityDef> Personality = Personalities.ToDictionary(p => p.Id);

    public static readonly List<RoleDef> Roles = new()
    {
        new("leader", "Team Leader", "Commands the squad. Leadership aura reduces squad stress."),
        new("point", "Point", "Leads the stack. Faster threat reaction."),
        new("assault", "Assault", "Main combat personnel."),
        new("shield", "Shield", "Carries the ballistic shield."),
        new("marksman", "Marksman", "Overwatch. Extended vision when holding."),
        new("medic", "Medic", "Treats injured personnel faster."),
        new("tech", "Technical Specialist", "Locks, charges and electronics."),
    };
    public static readonly Dictionary<string, RoleDef> Role = Roles.ToDictionary(r => r.Id);

    public static readonly List<PerkDef> Perks = new()
    {
        new("quickdraw", "Quick Draw", "Aim acquisition time -20%."), new("breacher", "Breacher", "Breaching 40% faster, kicks rarely fail."),
        new("medic", "Combat Medic", "Treatment twice as fast and more effective."), new("eagle", "Eagle Eye", "+3 tiles vision, spots hidden suspects at range."),
        new("steady", "Steady Hands", "Recoil -25%."), new("iron", "Iron Will", "Stress gain halved."), new("runner", "Fleet Foot", "Movement speed +10%."),
        new("negotiator", "Negotiator", "Suspects more likely to surrender (+25%)."), new("locksmith", "Locksmith", "Lockpicking twice as fast."),
        new("grenadier", "Grenadier", "Longer throws, bigger flashbang radius."), new("tough", "Tough as Nails", "Bleed-out time doubled, +15 max HP."),
    };
    public static readonly Dictionary<string, PerkDef> Perk = Perks.ToDictionary(p => p.Id);

    public static readonly string[] NamesM = { "James", "Marcus", "Daniel", "Ethan", "Luis", "Andre", "Tyler", "Kevin", "Ryan", "Omar", "Victor", "Sam", "Nathan", "Derek", "Caleb", "Mateo", "Isaac", "Jordan", "Hector", "Wes", "Malik", "Grant", "Felix", "Owen", "Rafael", "Trent", "Cole", "Dmitri", "Kenji", "Bo" };
    public static readonly string[] NamesF = { "Sarah", "Maya", "Elena", "Jess", "Nadia", "Rachel", "Ava", "Lena", "Carmen", "Tasha", "Grace", "Ivy", "Kara", "Rosa", "Dana", "Mei", "Priya", "Leah", "Nina", "Jo", "Alexis", "Sofia", "Erin", "Vera", "Quinn" };
    public static readonly string[] Surnames = { "Walker", "Reyes", "Novak", "Brennan", "Okafor", "Castillo", "Hughes", "Kowalski", "Tran", "Mercer", "Vasquez", "Holt", "Barnes", "Lindqvist", "Park", "Doyle", "Ramos", "Fischer", "Grady", "Sato", "Murphy", "Adeyemi", "Cruz", "Volkov", "Hayes", "Ortega", "Kane", "Bishop", "Shaw", "Ibarra", "Whitlock", "Nash", "Duarte", "Keller", "Rowe" };
    public static readonly string[] Callsigns = { "Hawk", "Ghost", "Rook", "Viper", "Tank", "Doc", "Sparks", "Echo", "Saint", "Wolf", "Brick", "Jinx", "Nomad", "Reaper", "Mav", "Kodiak", "Dutch", "Fox", "Blade", "Flint", "Sarge", "Lucky", "Hex", "Atlas", "Moose", "Ranger", "Patch", "Juno", "Cypher", "Bear", "Tex", "Pike", "Stitch", "Ace", "Shadow" };
    public static readonly string[] Hometowns = { "Cedar Falls", "Port Halden", "Redwater", "Marlow City", "Fort Ashby", "Lakeview", "Grayson", "North Bend", "Silver Creek", "Harbor Point" };
    public static readonly string[] Backgrounds = { "former Army infantry", "ex-Marine", "twelve years municipal SWAT", "former state trooper", "ex-Navy corpsman", "former border patrol tactical unit", "former military police", "ex-Air Force security forces", "former county deputy", "former private security contractor", "former fire department paramedic", "former military combat engineer" };
    public static readonly string[] SuspectFirst = { "Tony", "Rico", "Dale", "Vince", "Manny", "Lenny", "Sergei", "Dre", "Cody", "Hank", "Lou", "Mick", "Tre", "Boris", "Javi", "Nico", "Rusty", "Earl", "Gus", "Yuri", "Tasha", "Lana", "Roxy", "Kat" };
    public static readonly string[] SuspectNick = { "Spider", "Snake", "Two-Tone", "Big Lou", "Smiley", "Chopper", "Ice", "Moth", "Junior", "Tank", "Duke", "Ghost", "Lucky", "Razor", "Kingfish", "Boots" };

    public static readonly string[] OpColors = { "#4fc3f7", "#ffb74d", "#81c784", "#e57373", "#ba68c8", "#fff176", "#4db6ac", "#f06292", "#a1887f", "#90a4ae", "#dce775", "#7986cb" };
}
