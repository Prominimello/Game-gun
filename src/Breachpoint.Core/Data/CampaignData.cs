namespace Breachpoint.Core.Data;

/// <summary>Organizations, campaign operations, mission templates, difficulty and enemy archetypes.</summary>
public static class CampaignData
{
    public static readonly List<OrgDef> Orgs = new()
    {
        new("ldn", "Eastside Distribution Network", "EDN", 1, "#c8694b", "Small-time street-level operation moving product through stash houses and storage units on the east side of Marlow City.", "Darnell \"Kingfish\" Pryor"),
        new("bwl", "Blackwell Logistics", "BWL", 2, "#4b8fc8", "A trucking and freight company acting as the transportation backbone for regional trafficking.", "Gordon Blackwell"),
        new("syn", "The Syndicate", "SYN", 3, "#a45bc8", "A disciplined organized crime family with legitimate fronts, armed security and financial operations.", "Victor Vane"),
        new("mer", "The Meridian Network", "MER", 4, "#c8b44b", "International trafficking network with cells across multiple countries. Well-funded and extremely dangerous.", "\"The Architect\""),
    };
    public static readonly Dictionary<string, OrgDef> Org = Orgs.ToDictionary(o => o.Id);

    public static readonly Dictionary<string, MissionTypeDef> MissionTypes = new[]
    {
        new MissionTypeDef("warrant", "Residential Warrant", "house", new[] { "neutralize", "evidence" }, (3, 5), (0, 0), (1, 3), (2, 3), (0, 1), "A wanted suspect is believed to be inside a suburban home with associates. Locate the suspect, recover evidence, and protect any occupants."),
        new MissionTypeDef("hostage", "Hostage Rescue", "house", new[] { "rescue", "neutralize" }, (4, 6), (2, 3), (0, 0), (1, 2), (0, 0), "An armed group has barricaded itself inside a residence with hostages. Hostage-takers may execute captives if alerted for long."),
        new MissionTypeDef("druglab", "Drug Laboratory Raid", "house2", new[] { "neutralize", "contraband" }, (4, 6), (0, 0), (0, 2), (2, 3), (2, 4), "A residential property is being used to cook and package narcotics. Secure the lab, seize the product and arrest the cooks."),
        new MissionTypeDef("warehouse", "Warehouse Raid", "warehouse", new[] { "neutralize", "evidence", "contraband" }, (6, 9), (0, 1), (0, 2), (2, 3), (2, 4), "A suspected distribution warehouse. Secure the property, locate contraband and recover shipping documents."),
        new MissionTypeDef("compound", "Industrial Compound", "compound", new[] { "neutralize", "evidence" }, (8, 12), (0, 1), (0, 1), (3, 4), (1, 2), "A heavily guarded industrial facility linked to organized crime. Multiple structures. Locate leadership and records."),
        new MissionTypeDef("bank", "Bank Robbery Response", "bank", new[] { "rescue", "neutralize", "evidence" }, (4, 6), (3, 5), (0, 0), (1, 2), (0, 0), "Armed suspects are holding employees and customers inside a bank branch. Rescue civilians and secure the building."),
        new MissionTypeDef("airport", "Airport Cargo Interdiction", "hangar", new[] { "neutralize", "contraband", "evidence" }, (7, 10), (0, 0), (1, 3), (2, 3), (3, 4), "An illicit shipment is moving through an airport cargo facility. Identify the shipment and secure the area."),
        new MissionTypeDef("hotel", "Hotel Hostage Situation", "hotel", new[] { "rescue", "neutralize" }, (6, 9), (2, 4), (2, 4), (1, 2), (0, 0), "Armed suspects have taken hostages across multiple floors of a hotel. Clear floor by floor."),
        new MissionTypeDef("bunker", "Underground Bunker Raid", "bunker", new[] { "neutralize", "evidence" }, (7, 10), (0, 1), (0, 0), (3, 4), (1, 2), "A hidden underground facility beneath a forest cabin. Limited intelligence. Expect tight corridors."),
        new MissionTypeDef("shipping", "Shipping Container Inspection", "shipyard", new[] { "neutralize", "contraband" }, (6, 9), (0, 2), (0, 1), (2, 2), (2, 4), "Containers at the port are being used to move contraband. Clear the yard and the dock office."),
        new MissionTypeDef("rural", "Rural Compound Assault", "rural", new[] { "neutralize", "evidence" }, (6, 9), (0, 1), (0, 2), (2, 3), (1, 2), "A remote farm used as a hideout. Farmhouse, barn and outbuildings."),
        new MissionTypeDef("prison", "Detention Riot Intervention", "prison", new[] { "rescue", "neutralize" }, (7, 10), (2, 3), (0, 0), (0, 1), (1, 2), "Inmates have seized a detention block and are holding correctional officers. Restore order and rescue the officers."),
        new MissionTypeDef("store", "Convenience Store Robbery", "store", new[] { "rescue", "neutralize" }, (2, 3), (1, 3), (0, 1), (1, 1), (0, 0), "An armed robbery in progress at a corner store. Small space, civilians present."),
        new MissionTypeDef("mansion", "Mansion Raid", "mansion", new[] { "neutralize", "capture", "evidence" }, (8, 11), (0, 1), (1, 3), (3, 4), (1, 2), "A high-value target is residing in a guarded mansion. Capture alive if possible.", true),
    }.ToDictionary(m => m.Id);

    private static CampaignMissionDef CM(string id, string org, int idx, string name, string type, int seed, int enemies, int reward, string intel,
        int hostages = 0, int civilians = 0, int evidence = 0, int contraband = 0, int? floors = null, bool small = false, bool hvt = false,
        bool lieutenant = false, bool night = false, bool rain = false, bool takedown = false, int intelReq = 0) => new()
        {
            Id = id, Org = org, Idx = idx, Name = name, Type = type, Seed = seed, Enemies = enemies, Reward = reward, Intel = intel, Hostages = hostages,
            Civilians = civilians, Evidence = evidence, Contraband = contraband, Floors = floors, Small = small, Hvt = hvt, Lieutenant = lieutenant,
            Night = night, Rain = rain, Takedown = takedown, IntelReq = intelReq,
        };

    public static readonly List<CampaignMissionDef> Campaign = new()
    {
        CM("c1_1", "ldn", 0, "Corner Stash", "warrant", 1101, 3, 9000, "A low-level dealer named in a traffic stop is storing product at his mother's house. Expect 2–4 associates, mostly handguns.", civilians: 2, evidence: 2, contraband: 1, lieutenant: true),
        CM("c1_2", "ldn", 1, "Stash House on Pine", "druglab", 1202, 5, 12000, "Ledger from Corner Stash points to a two-storey stash house on Pine Street, where product is cut and packaged.", civilians: 1, evidence: 3, contraband: 3, floors: 2),
        CM("c1_3", "ldn", 2, "Riverside Storage", "warehouse", 1303, 6, 15000, "The network rents a riverside storage warehouse. A courier who wanted out is being held there.", hostages: 1, evidence: 3, contraband: 3, small: true),
        CM("c1_4", "ldn", 3, "Kingfish", "hostage", 1404, 7, 22000, "Darnell \"Kingfish\" Pryor is holed up in his duplex with his crew and two hostages. Take him alive.", hostages: 2, evidence: 3, hvt: true, floors: 2, takedown: true, intelReq: 4),
        CM("c2_1", "bwl", 0, "Bay 9", "warehouse", 2101, 8, 18000, "Kingfish's phone links his supply to Blackwell Logistics freight. Bay 9 at their north terminal.", evidence: 3, contraband: 4),
        CM("c2_2", "bwl", 1, "Vehicle Depot", "compound", 2202, 9, 21000, "Trucks are fitted with hidden compartments at a fenced depot outside town.", evidence: 3, contraband: 2, lieutenant: true),
        CM("c2_3", "bwl", 2, "Night Shift", "shipping", 2303, 9, 24000, "Two port inspectors who refused bribes have gone missing near Blackwell's container stack.", hostages: 2, evidence: 2, contraband: 3, night: true),
        CM("c2_4", "bwl", 3, "Blackwell Compound", "compound", 2404, 12, 34000, "Gordon Blackwell is at the company's industrial compound under heavy security.", hostages: 1, evidence: 4, contraband: 2, hvt: true, takedown: true, intelReq: 5),
        CM("c3_1", "syn", 0, "First National", "bank", 3101, 5, 26000, "A robbery crew tied to Syndicate money-laundering has taken a bank branch.", hostages: 4, evidence: 2),
        CM("c3_2", "syn", 1, "The Grand Halden", "hotel", 3202, 8, 30000, "Syndicate enforcers have seized floors of a hotel to silence a witness.", hostages: 3, civilians: 3, evidence: 2, lieutenant: true),
        CM("c3_3", "syn", 2, "Safehouse", "warrant", 3303, 6, 28000, "A Syndicate bookkeeper uses a suburban safehouse. Raid at night, in the rain.", evidence: 4, contraband: 2, night: true, floors: 2, rain: true),
        CM("c3_4", "syn", 3, "Vane Estate", "mansion", 3404, 11, 45000, "Victor Vane at his estate, with professional security. Capture him.", civilians: 2, evidence: 4, contraband: 2, hvt: true, takedown: true, intelReq: 6),
        CM("c4_1", "mer", 0, "Cargo Terminal 4", "airport", 4101, 10, 40000, "Vane's ledgers point to Meridian shipments through the airport cargo terminal.", civilians: 2, evidence: 3, contraband: 4),
        CM("c4_2", "mer", 1, "Dry Creek Farm", "rural", 4202, 9, 42000, "A Meridian cell is using a remote farm as a waypoint.", hostages: 1, evidence: 3, contraband: 2, night: true, lieutenant: true),
        CM("c4_3", "mer", 2, "The Hole", "bunker", 4303, 10, 48000, "Beneath a hunting cabin lies a bunker used as Meridian's regional command post.", hostages: 1, evidence: 4, contraband: 2),
        CM("c4_4", "mer", 3, "Architect", "compound", 4404, 14, 80000, "The Architect is personally overseeing a handoff at a fortified facility. Final operation.", hostages: 2, evidence: 4, contraband: 3, hvt: true, takedown: true, intelReq: 6, night: true),
    };

    public static readonly List<EvidenceType> EvidenceTypes = new()
    {
        new("docs", "Documents", "docs", 1), new("laptop", "Laptop", "laptop", 2), new("phone", "Burner Phone", "phone", 1),
        new("ledger", "Ledger", "docs", 2), new("drive", "Hard Drive", "laptop", 2), new("manifest", "Shipping Manifest", "docs", 1),
    };
    public static readonly List<ContrabandType> ContrabandTypes = new()
    {
        new("drugs", "Narcotics", "drugs", 2500), new("cash", "Cash Bundle", "cash", 4000), new("guns", "Weapons Cache", "guns", 3000),
    };

    public static readonly Dictionary<string, DifficultyDef> Difficulty = new[]
    {
        new DifficultyDef("casual", "Casual", 0.6, 0.7, 1.3, true, 0.8, 0.9, "never", 18, "Forgiving damage. Hidden suspects are hinted. Cheaper equipment."),
        new DifficultyDef("standard", "Standard", 1.0, 1.0, 1.0, false, 1.0, 1.0, "never", 12, "Realistic tactical combat. Balanced AI and costs."),
        new DifficultyDef("veteran", "Veteran", 1.15, 1.15, 0.88, false, 1.15, 1.15, "chance", 9, "Limited intel, deadlier suspects. Downed operators may die."),
        new DifficultyDef("milsim", "Milsim", 1.35, 1.25, 0.8, false, 1.3, 1.3, "always", 7, "Realistic damage, scarce resources. Operator death is permanent."),
    }.ToDictionary(d => d.Id);
    public static readonly string[] DifficultyOrder = { "casual", "standard", "veteran", "milsim" };

    public static readonly Dictionary<string, EnemyDef> Enemies = new[]
    {
        new EnemyDef("associate", "Associate", 0.32, 0.95, 35, 0.3, false, 0.6, new[] { "g17", "g17", "r870", "none" }, 0),
        new EnemyDef("guard", "Armed Guard", 0.48, 0.75, 60, 0.55, true, 0.33, new[] { "m590", "ak105", "mp5", "g19" }, 0),
        new EnemyDef("crim", "Organized Criminal", 0.62, 0.6, 75, 0.75, true, 0.18, new[] { "m4a1", "ak105", "ump45", "g36c" }, 30),
        new EnemyDef("pro", "Professional Security", 0.72, 0.5, 85, 0.8, true, 0.1, new[] { "hk416", "mcx", "scarl", "mp7" }, 55),
        new EnemyDef("hvt", "High-Value Target", 0.4, 0.8, 30, 0.2, true, 0.45, new[] { "g19", "usp45" }, 0),
        new EnemyDef("lieutenant", "Lieutenant", 0.55, 0.65, 65, 0.6, true, 0.28, new[] { "m4a1", "ump45" }, 30),
        new EnemyDef("inmate", "Rioting Inmate", 0.3, 0.9, 45, 0.65, false, 0.5, new[] { "g17", "r870", "none" }, 0),
    }.ToDictionary(e => e.Id);

    public static readonly Dictionary<string, string[]> Voice = new()
    {
        ["contact"] = new[] { "Contact!", "Contact front!", "Tango!", "Got one!" },
        ["comply"] = new[] { "Police! Show me your hands!", "Police! Drop the weapon!", "Hands! Let me see your hands!", "Get down! Get on the ground!" },
        ["down"] = new[] { "Suspect down.", "Tango down.", "He's down." },
        ["clear"] = new[] { "Room clear.", "Clear!", "Clear." },
        ["reload"] = new[] { "Reloading!", "Changing mags!", "Cover me, reloading!" },
        ["hurt"] = new[] { "I'm hit!", "Taking fire!", "Hit! I'm hit!" },
        ["mandown"] = new[] { "Man down!", "Operator down!", "We got a man down!" },
        ["flash"] = new[] { "Flashbang out!", "Bang out!", "Flash!" },
        ["breach"] = new[] { "Breaching!", "Breach, breach!", "Door's coming down!" },
        ["arrest"] = new[] { "Suspect in custody.", "Cuffed.", "Restrained." },
        ["secure"] = new[] { "Civilian secured.", "Hostage secure.", "You're safe, stay down." },
        ["evidence"] = new[] { "Evidence bagged.", "Got something here.", "Securing evidence." },
        ["locked"] = new[] { "Door's locked.", "It's locked." },
        ["moving"] = new[] { "Moving.", "Copy, moving.", "On it.", "Roger." },
        ["jam"] = new[] { "Malfunction!", "Jammed!" },
        ["allclear"] = new[] { "Area secure. All threats neutralized." },
    };

    public static readonly string[] GenOrgA = { "Crimson", "Northside", "Iron", "Silver", "Black Lotus", "Harbor", "Red Line", "Sable", "Cobalt", "Grey Wolf", "Ashfall", "Saint" };
    public static readonly string[] GenOrgB = { "Cartel", "Brotherhood", "Syndicate", "Crew", "Outfit", "Network", "Collective", "Family", "Ring", "Company" };
}
