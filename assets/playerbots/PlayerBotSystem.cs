// PlayerBotSystem.cs
// Seeds the world with PlayerBots and provides admin commands.
//
// Key behavior changes from earlier versions:
//   - Civilian roles get RANDOM counts at seed time, not even splits.
//   - Macroers spawn mostly at banks (with some at wilderness as PK bait).
//   - PKs at dungeons are not guaranteed: each dungeon spot rolls a chance
//     per reseed for a gang to be there.
//   - "Wandering" PKs roam the wilderness, picking new waypoints over time.
//   - PK gear/class fully randomized (see PlayerBotRoles.PK).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Commands;
using Server.Mobiles;

namespace Server.Custom
{
    public static class PlayerBotSystem
    {
        [Flags]
        private enum SpotKind
        {
            None     = 0,
            Town     = 1 << 0,
            Road     = 1 << 1,
            Forge    = 1 << 2,
            Mine     = 1 << 3,
            Dock     = 1 << 4,
            Bank     = 1 << 5,   // macroer-preferred
            Forest   = 1 << 6,
            Wild     = 1 << 7,
            Dungeon  = 1 << 8,
        }

        private class SpawnSpot
        {
            public string Region;
            public Point3D Loc;
            public int Weight;
            public int Jitter;
            public SpotKind Kinds;
            public SpawnSpot(string r, Point3D l, int w, int j, SpotKind k)
            { Region = r; Loc = l; Weight = w; Jitter = j; Kinds = k; }
        }

        private static readonly List<SpawnSpot> MainlandSpots = new List<SpawnSpot>
        {
            // Britain — the heart, with the most popular bank
            new SpawnSpot("Britain Bank",      new Point3D(1492,1628,10), 40, 8,  SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Britain Inn",       new Point3D(1495,1573,10), 15, 6,  SpotKind.Town),
            new SpawnSpot("Britain Smithy",    new Point3D(1455,1573,10), 10, 4,  SpotKind.Forge),
            new SpawnSpot("Britain West Gate", new Point3D(1417,1697,10), 20, 10, SpotKind.Town|SpotKind.Road),
            new SpawnSpot("Britain Castle",    new Point3D(1323,1624,20), 10, 6,  SpotKind.Town),
            new SpawnSpot("Britain Farms",     new Point3D(1620,1680,10), 12, 15, SpotKind.Town),
            new SpawnSpot("Britain Docks",     new Point3D(1499,1733, 0), 12, 8,  SpotKind.Dock|SpotKind.Town),

            new SpawnSpot("Trinsic Bank",      new Point3D(1823,2821, 0), 30, 12, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Trinsic Paladin HQ",new Point3D(1859,2789, 0),  8, 6,  SpotKind.Town),
            new SpawnSpot("Trinsic Smithy",    new Point3D(1837,2845, 0),  8, 4,  SpotKind.Forge),
            new SpawnSpot("Trinsic Docks",     new Point3D(1789,2885, 0),  8, 6,  SpotKind.Dock),

            new SpawnSpot("Vesper Bank",       new Point3D(2899, 676, 0), 28, 12, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Vesper Tavern",     new Point3D(2861, 696, 0),  8, 4,  SpotKind.Town),
            new SpawnSpot("Vesper Docks",      new Point3D(2799, 722, 0), 10, 8,  SpotKind.Dock),

            new SpawnSpot("Minoc Bank",        new Point3D(2477, 439,15), 22, 12, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Minoc Mines",       new Point3D(2531, 472,15), 18, 8,  SpotKind.Mine),
            new SpawnSpot("Minoc North Mines", new Point3D(2480, 360,20), 12, 8,  SpotKind.Mine),
            new SpawnSpot("Minoc Smithy",      new Point3D(2497, 540, 0),  8, 4,  SpotKind.Forge),

            new SpawnSpot("Yew Bank",          new Point3D(548, 979,  0), 22, 14, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Yew Abbey",         new Point3D(632, 858,  0),  8, 8,  SpotKind.Town),
            new SpawnSpot("Yew Forests North", new Point3D(750, 800,  0), 12, 25, SpotKind.Forest),
            new SpawnSpot("Yew Forests South", new Point3D(580,1100,  0), 12, 25, SpotKind.Forest),

            new SpawnSpot("Skara Brae Bank",   new Point3D(648,2235, 0), 22, 12, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Skara Brae Docks",  new Point3D(630,2192, 0), 14, 8,  SpotKind.Dock),
            new SpawnSpot("Skara Forests",     new Point3D(700,2380, 0),  8, 20, SpotKind.Forest),

            new SpawnSpot("Moonglow Bank",     new Point3D(4406,1045, 0), 24, 12, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Moonglow Docks",    new Point3D(4501,1158, 0),  8, 6,  SpotKind.Dock),

            new SpawnSpot("Magincia Bank",     new Point3D(3712,2220,20), 14, 10, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Magincia Docks",    new Point3D(3686,2272,20),  6, 6,  SpotKind.Dock),
            new SpawnSpot("Nujel'm Bank",      new Point3D(3766,1284, 0), 14, 10, SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Cove",              new Point3D(2237,1196, 0), 10, 8,  SpotKind.Town),
            new SpawnSpot("Serpent's Hold",    new Point3D(2902,3477,15), 10, 8,  SpotKind.Town|SpotKind.Dock),
            new SpawnSpot("Jhelom Bank",       new Point3D(1414,3821,10), 12, 8,  SpotKind.Town|SpotKind.Bank),
            new SpawnSpot("Jhelom Docks",      new Point3D(1394,3804,-3),  6, 6,  SpotKind.Dock),
            new SpawnSpot("Buccaneer's Den",   new Point3D(2729,2106, 0),  8, 6,  SpotKind.Town|SpotKind.Wild),

            // Roads
            new SpawnSpot("Road to Yew",       new Point3D(1058,1456, 0),  4, 20, SpotKind.Road),
            new SpawnSpot("Road to Trinsic",   new Point3D(1755,2467, 0),  4, 20, SpotKind.Road),
            new SpawnSpot("Road to Vesper",    new Point3D(2479, 840, 0),  4, 20, SpotKind.Road),

            // Dungeon entrances — PK candidates (chance per reseed)
            new SpawnSpot("Despise Entrance",  new Point3D(1300,1080, 0),  6, 8,  SpotKind.Dungeon),
            new SpawnSpot("Deceit Entrance",   new Point3D(4111, 432, 5),  6, 8,  SpotKind.Dungeon),
            new SpawnSpot("Destard Entrance",  new Point3D(1176,2640, 0),  6, 8,  SpotKind.Dungeon),
            new SpawnSpot("Shame Entrance",    new Point3D( 514,1561, 0),  6, 8,  SpotKind.Dungeon),
            new SpawnSpot("Wrong Entrance",    new Point3D(2042, 238, 0),  6, 8,  SpotKind.Dungeon),
            new SpawnSpot("Covetous Entrance", new Point3D(2499, 921, 0),  6, 8,  SpotKind.Dungeon),

            // Wilderness anchor spots — wandering PKs and bait macroers seed here
            new SpawnSpot("Wilderness north",  new Point3D(1800,1500, 0), 4, 30, SpotKind.Wild),
            new SpawnSpot("Wilderness east",   new Point3D(2700, 380, 0), 4, 30, SpotKind.Wild),
            new SpawnSpot("Wilderness south",  new Point3D(2100,2900, 0), 4, 30, SpotKind.Wild),
            new SpawnSpot("Wilderness west",   new Point3D( 800,1900, 0), 4, 30, SpotKind.Wild),
        };

        private static readonly List<SpawnSpot> LostLandsSpots = new List<SpawnSpot>
        {
            new SpawnSpot("Papua Bank",   new Point3D(5728,3197,-3), 15, 10, SpotKind.Town|SpotKind.Bank|SpotKind.Dock),
            new SpawnSpot("Delucia Bank", new Point3D(5273,4034,37), 15, 10, SpotKind.Town|SpotKind.Bank|SpotKind.Mine),
            new SpawnSpot("Lost Lands wilds", new Point3D(5520,2900, 0),  3, 30, SpotKind.Road|SpotKind.Wild),
        };

        // ===== Tunables =====
        private static int TargetBotCount = 300;
        private static double CivilianRatio = 0.30;
        private static double RoleAppropriateChance = 0.75;
        private static double PKDensity = 0.05;
        private static double MacroerRatio = 0.20;       // of civilians
        private static double MacroerBankBias = 1.00;    // 100% at banks (no wilderness bait)
        private static double DungeonOccupancyChance = 0.55;  // per dungeon, per reseed
        private static double PKWanderRatio = 0.50;      // of PKs that wander vs camp dungeons
        private static int PKGangMin = 1;
        private static int PKGangMax = 3;
        private static double CivilianRespawnSeconds = 600.0;
        private static bool CivilianRespawnEnabled = true;

        private const string SeededFlagFile = "Saves/playerbots_seeded.flag";
        private const string ExpansionFile  = "Scripts/Custom/PlayerBots/Expansion.txt";
        private const string SettingsFile   = "Scripts/Custom/PlayerBots/Settings.txt";

        private static string CurrentExpansion = "t2a";
        private static Timer m_RespawnTimer;
        private static Dictionary<string, int> m_TargetPerRegion;

        public static void Initialize()
        {
            LoadExpansion();
            LoadSettings();

            Console.WriteLine(
                "[PlayerBotSystem] expansion={0} target={1} civ={2:P0} pk={3:P0} mode={4}",
                CurrentExpansion, TargetBotCount, CivilianRatio, PKDensity,
                PKBehavior.TargetMode);

            CommandSystem.Register("addbot",     AccessLevel.GameMaster,    AddBot_OnCommand);
            CommandSystem.Register("removebot",  AccessLevel.GameMaster,    RemoveBot_OnCommand);
            CommandSystem.Register("botcount",   AccessLevel.GameMaster,    BotCount_OnCommand);
            CommandSystem.Register("botstats",   AccessLevel.GameMaster,    BotStats_OnCommand);
            CommandSystem.Register("botbudget",  AccessLevel.Administrator, BotBudget_OnCommand);
            CommandSystem.Register("pkmode",     AccessLevel.Administrator, PKMode_OnCommand);
            CommandSystem.Register("pkdensity",  AccessLevel.Administrator, PKDensity_OnCommand);
            CommandSystem.Register("clearbots",  AccessLevel.Administrator, ClearBots_OnCommand);
            CommandSystem.Register("reseedbots", AccessLevel.Administrator, ReseedBots_OnCommand);

            EventSink.WorldLoad += OnWorldLoad;
        }

        private static void LoadExpansion()
        {
            try
            {
                if (File.Exists(ExpansionFile))
                    CurrentExpansion = File.ReadAllText(ExpansionFile).Trim().ToLowerInvariant();
            }
            catch { }
        }

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) { SaveSettings(); return; }
                foreach (var line in File.ReadAllLines(SettingsFile))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    var parts = t.Split('=');
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim().ToLowerInvariant();
                    var val = parts[1].Trim();
                    if      (key == "targetbotcount" && int.TryParse(val, out int n))
                        TargetBotCount = Math.Max(0, Math.Min(2000, n));
                    else if (key == "civilianratio" && double.TryParse(val, out double cr))
                        CivilianRatio = Math.Max(0, Math.Min(1, cr));
                    else if (key == "roleappropriatechance" && double.TryParse(val, out double rac))
                        RoleAppropriateChance = Math.Max(0, Math.Min(1, rac));
                    else if (key == "pkdensity" && double.TryParse(val, out double pd))
                        PKDensity = Math.Max(0, Math.Min(0.5, pd));
                    else if (key == "macroerratio" && double.TryParse(val, out double mr))
                        MacroerRatio = Math.Max(0, Math.Min(1, mr));
                    else if (key == "macroerbankbias" && double.TryParse(val, out double mb))
                        MacroerBankBias = Math.Max(0, Math.Min(1, mb));
                    else if (key == "dungeonoccupancychance" && double.TryParse(val, out double doc))
                        DungeonOccupancyChance = Math.Max(0, Math.Min(1, doc));
                    else if (key == "pkwanderratio" && double.TryParse(val, out double pw))
                        PKWanderRatio = Math.Max(0, Math.Min(1, pw));
                    else if (key == "pkgangmin" && int.TryParse(val, out int gmin))
                        PKGangMin = Math.Max(1, Math.Min(5, gmin));
                    else if (key == "pkgangmax" && int.TryParse(val, out int gmax))
                        PKGangMax = Math.Max(1, Math.Min(8, gmax));
                    else if (key == "pktargetmode")
                    {
                        if (Enum.TryParse(val, true, out PKTargetMode tm))
                            PKBehavior.TargetMode = tm;
                    }
                    else if (key == "civilianrespawnseconds" && double.TryParse(val, out double rs))
                        CivilianRespawnSeconds = Math.Max(60, rs);
                    else if (key == "civilianrespawnenabled" && bool.TryParse(val, out bool re))
                        CivilianRespawnEnabled = re;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotSystem] Settings load failed: {0}", ex.Message);
            }
        }

        private static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllText(SettingsFile,
@"# PlayerBot system settings — edit and use [reseedbots to apply
TargetBotCount=" + TargetBotCount + @"
CivilianRatio=" + CivilianRatio.ToString("F2") + @"
RoleAppropriateChance=" + RoleAppropriateChance.ToString("F2") + @"

# PK settings
PKDensity=" + PKDensity.ToString("F2") + @"
PKTargetMode=" + PKBehavior.TargetMode + @"
PKGangMin=" + PKGangMin + @"
PKGangMax=" + PKGangMax + @"
# Probability a given dungeon entrance gets a PK gang on this reseed
DungeonOccupancyChance=" + DungeonOccupancyChance.ToString("F2") + @"
# Fraction of PKs that wander wilderness vs camp dungeons
PKWanderRatio=" + PKWanderRatio.ToString("F2") + @"

# Macroer settings (macroers replace bards — they appear as players
# leveling skills, often at banks)
MacroerRatio=" + MacroerRatio.ToString("F2") + @"
MacroerBankBias=" + MacroerBankBias.ToString("F2") + @"

# Civilian respawn (replace bots killed by PKs)
CivilianRespawnEnabled=" + CivilianRespawnEnabled + @"
CivilianRespawnSeconds=" + CivilianRespawnSeconds.ToString("F0") + "\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotSystem] Settings save failed: {0}", ex.Message);
            }
        }

        private static List<SpawnSpot> AllSpots()
        {
            var list = new List<SpawnSpot>(MainlandSpots);
            if (CurrentExpansion != "pretoa")
                list.AddRange(LostLandsSpots);
            return list;
        }

        private static SpotKind PreferredKindFor(BotRole role)
        {
            switch (role)
            {
                case BotRole.Crafter:       return SpotKind.Forge;
                case BotRole.Miner:         return SpotKind.Mine;
                case BotRole.Fisherman:     return SpotKind.Dock;
                case BotRole.Lumberjack:    return SpotKind.Forest;
                case BotRole.Macroer:       return SpotKind.Bank;
                case BotRole.PK:            return SpotKind.Dungeon | SpotKind.Wild;
                case BotRole.TamerCivilian: return SpotKind.Town;     // peaceful, town parks
                case BotRole.TamerCombat:   return SpotKind.Road | SpotKind.Forest | SpotKind.Wild;
                default:                    return SpotKind.Town | SpotKind.Road;
            }
        }

        private static SpawnSpot PickWeighted(List<SpawnSpot> spots)
        {
            if (spots.Count == 0) return null;
            int total = spots.Sum(s => s.Weight);
            if (total == 0) return spots[Utility.Random(spots.Count)];
            int roll = Utility.Random(total);
            int sum = 0;
            foreach (var s in spots)
            {
                sum += s.Weight;
                if (roll < sum) return s;
            }
            return spots[spots.Count - 1];
        }

        // Generic spot picker for non-PK roles.
        private static SpawnSpot PickSpotFor(BotRole role)
        {
            var all = AllSpots();
            var pref = PreferredKindFor(role);

            // Macroer special case: prefer banks. If macroer-bank-bias rolls
            // through, fall through to the general town spot picker — never
            // place them in the wilderness as PK bait.
            if (role == BotRole.Macroer)
            {
                if (Utility.RandomDouble() < MacroerBankBias)
                {
                    var banks = all.Where(s => (s.Kinds & SpotKind.Bank) != 0).ToList();
                    if (banks.Count > 0) return PickWeighted(banks);
                }
                // No wilderness fallback — fall through to general
            }

            if (Utility.RandomDouble() < RoleAppropriateChance)
            {
                var preferred = all.Where(s => (s.Kinds & pref) != 0).ToList();
                if (preferred.Count > 0) return PickWeighted(preferred);
            }

            var general = all.Where(s =>
                (s.Kinds & (SpotKind.Town | SpotKind.Road)) != 0).ToList();
            if (general.Count == 0) general = all;
            return PickWeighted(general);
        }

        private static BotRole PickCombatRole()
        {
            // ~10% chance for a combat tamer in the combat slot; rest are
            // the four classic classes.
            if (Utility.Random(10) == 0) return BotRole.TamerCombat;
            return (BotRole)Utility.Random(4);
        }

        private static PlayerBot SpawnAt(BotRole role, Point3D loc, Map map, int jitter, int rangeHome = 15)
        {
            var bot = new PlayerBot(role);
            var place = new Point3D(
                loc.X + Utility.RandomMinMax(-jitter, jitter),
                loc.Y + Utility.RandomMinMax(-jitter, jitter),
                loc.Z);
            bot.MoveToWorld(place, map);
            bot.Home = place;
            bot.RangeHome = role == BotRole.Macroer ? 2 : rangeHome;

            // Attach followers after the bot has a valid location.
            try
            {
                if (bot.WantsPackMule)
                {
                    var mule = PlayerBotPets.CreatePackAnimalWithOre(bot.Tier);
                    PlayerBotPets.SpawnPetFor(bot, mule);
                }
                if (bot.WantsPeacefulPet)
                {
                    var pet = PlayerBotPets.CreatePeacefulPet(bot.Tier);
                    PlayerBotPets.SpawnPetFor(bot, pet);
                }
                if (bot.WantsCombatPet)
                {
                    var pet = PlayerBotPets.CreateCombatPet(bot.Tier);
                    PlayerBotPets.SpawnPetFor(bot, pet);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotSystem] follower attach failed: {0}", ex.Message);
            }

            return bot;
        }

        // Picks how many of each civilian role we'll seed.
        // Returns (crafters, miners, fishermen, lumberjacks, tamers, macroers).
        private static (int, int, int, int, int, int) RollCivilianMix(int civTotal)
        {
            // Macroers carved out first.
            int macroers = (int)Math.Round(civTotal * MacroerRatio);
            int remaining = civTotal - macroers;
            if (remaining <= 0) return (0, 0, 0, 0, 0, macroers);

            // Five remaining roles share the rest with random weights so
            // each reseed has a different mix.
            int wCraft = Utility.RandomMinMax(1, 3);
            int wMine  = Utility.RandomMinMax(1, 3);
            int wFish  = Utility.RandomMinMax(1, 3);
            int wLumb  = Utility.RandomMinMax(1, 3);
            int wTame  = Utility.RandomMinMax(1, 3);
            int wTotal = wCraft + wMine + wFish + wLumb + wTame;

            int crafters = remaining * wCraft / wTotal;
            int miners   = remaining * wMine  / wTotal;
            int fishers  = remaining * wFish  / wTotal;
            int lumbs    = remaining * wLumb  / wTotal;
            int tamers   = remaining - crafters - miners - fishers - lumbs;

            return (crafters, miners, fishers, lumbs, tamers, macroers);
        }

        private static int SeedBots(int count)
        {
            int pkTarget = (int)Math.Round(count * PKDensity);
            int civilianTarget = (int)Math.Round((count - pkTarget) * CivilianRatio);
            int combatTarget = count - civilianTarget - pkTarget;
            int seeded = 0;

            // ---- Combat ----
            for (int i = 0; i < combatTarget; i++)
            {
                try
                {
                    var role = PickCombatRole();
                    var spot = PickSpotFor(role);
                    if (spot != null) { SpawnAt(role, spot.Loc, Map.Felucca, spot.Jitter); seeded++; }
                }
                catch (Exception ex) { Console.WriteLine("[PlayerBotSystem] combat: {0}", ex.Message); }
                LogProgress(seeded, count);
            }

            // ---- Civilians ----
            var (nCraft, nMine, nFish, nLumb, nTame, nMacro) = RollCivilianMix(civilianTarget);
            Console.WriteLine(
                "[PlayerBotSystem] civilian mix this reseed: crafter={0} miner={1} fisher={2} lumb={3} tamer={4} macroer={5}",
                nCraft, nMine, nFish, nLumb, nTame, nMacro);

            seeded += SeedRole(BotRole.Crafter,       nCraft, count, ref seeded);
            seeded += SeedRole(BotRole.Miner,         nMine,  count, ref seeded);
            seeded += SeedRole(BotRole.Fisherman,     nFish,  count, ref seeded);
            seeded += SeedRole(BotRole.Lumberjack,    nLumb,  count, ref seeded);
            seeded += SeedRole(BotRole.TamerCivilian, nTame,  count, ref seeded);
            seeded += SeedRole(BotRole.Macroer,       nMacro, count, ref seeded);

            // ---- PKs ----
            int pksSpawned = SeedPKs(pkTarget);

            Console.WriteLine(
                "[PlayerBotSystem] total seeded {0}: {1} combat / {2} civilian / {3} PK",
                seeded + pksSpawned, combatTarget, civilianTarget, pksSpawned);

            SnapshotCivilianTargets();
            return seeded + pksSpawned;
        }

        // Helper that returns the count successfully seeded (rather than mutating
        // a counter — keeps SeedBots more readable). The ref is for progress only.
        private static int SeedRole(BotRole role, int howMany, int totalForLog, ref int seededSoFar)
        {
            int n = 0;
            for (int i = 0; i < howMany; i++)
            {
                try
                {
                    var spot = PickSpotFor(role);
                    if (spot != null) { SpawnAt(role, spot.Loc, Map.Felucca, spot.Jitter); n++; }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PlayerBotSystem] {0}: {1}", role, ex.Message);
                }
                LogProgress(seededSoFar + n, totalForLog);
            }
            return n;
        }

        private static int SeedPKs(int pkTarget)
        {
            if (pkTarget <= 0) return 0;
            int spawned = 0;

            // Decide how many PKs camp dungeons vs wander wilderness.
            int wandererTarget = (int)Math.Round(pkTarget * PKWanderRatio);
            int camperTarget = pkTarget - wandererTarget;

            // ---- Dungeon campers ----
            var dungeonSpots = AllSpots().Where(s => (s.Kinds & SpotKind.Dungeon) != 0).ToList();
            // Per-dungeon roll: each entrance may or may not be occupied this reseed.
            var occupiedDungeons = dungeonSpots
                .Where(s => Utility.RandomDouble() < DungeonOccupancyChance)
                .ToList();

            if (occupiedDungeons.Count == 0 && dungeonSpots.Count > 0 && camperTarget > 0)
            {
                // If nothing rolled, guarantee at least one occupied dungeon
                // so a "campers only" config still produces some PKs.
                occupiedDungeons.Add(dungeonSpots[Utility.Random(dungeonSpots.Count)]);
            }

            Console.WriteLine(
                "[PlayerBotSystem] {0} of {1} dungeon entrances have PK gangs this reseed",
                occupiedDungeons.Count, dungeonSpots.Count);

            while (spawned < camperTarget && occupiedDungeons.Count > 0)
            {
                var spot = occupiedDungeons[Utility.Random(occupiedDungeons.Count)];
                int gangSize = Utility.RandomMinMax(PKGangMin, PKGangMax);
                if (spawned + gangSize > camperTarget) gangSize = camperTarget - spawned;
                spawned += SpawnPKGang(spot, gangSize, wandering: false);
            }

            // ---- Wandering wilderness PKs ----
            var wildSpots = AllSpots().Where(s => (s.Kinds & SpotKind.Wild) != 0).ToList();
            if (wildSpots.Count == 0) wildSpots = AllSpots().Where(s => (s.Kinds & SpotKind.Road) != 0).ToList();

            // Wanderers mostly solo or paired — they roam.
            while (spawned < pkTarget && wildSpots.Count > 0)
            {
                var spot = wildSpots[Utility.Random(wildSpots.Count)];
                int gangSize = Utility.RandomMinMax(1, 2);
                if (spawned + gangSize > pkTarget) gangSize = pkTarget - spawned;
                spawned += SpawnPKGang(spot, gangSize, wandering: true);
            }

            return spawned;
        }

        private static int SpawnPKGang(SpawnSpot spot, int size, bool wandering)
        {
            var gang = new PKGang();
            int spawned = 0;
            for (int g = 0; g < size; g++)
            {
                try
                {
                    var pk = SpawnAt(BotRole.PK, spot.Loc, Map.Felucca, spot.Jitter,
                        rangeHome: wandering ? 50 : 15);
                    if (pk.PKBrain != null)
                    {
                        pk.PKBrain.Gang = gang;
                        pk.PKBrain.Wandering = wandering;
                        gang.Add(pk);
                    }
                    spawned++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[PlayerBotSystem] PK gang spawn: {0}", ex.Message);
                    break;
                }
            }
            return spawned;
        }

        private static void LogProgress(int n, int total)
        {
            if (n > 0 && n % 50 == 0)
                Console.WriteLine("[PlayerBotSystem] ... {0}/{1} seeded", n, total);
        }

        private static void SnapshotCivilianTargets()
        {
            m_TargetPerRegion = new Dictionary<string, int>();
            var table = AllSpots();
            foreach (var bot in World.Mobiles.Values.OfType<PlayerBot>())
            {
                if (!bot.IsCivilianRole) continue;
                var region = NearestRegion(bot, table);
                if (!m_TargetPerRegion.ContainsKey(region)) m_TargetPerRegion[region] = 0;
                m_TargetPerRegion[region]++;
            }
        }

        private static string NearestRegion(IPoint2D p, List<SpawnSpot> table)
        {
            SpawnSpot best = null;
            double bestD = double.MaxValue;
            foreach (var s in table)
            {
                double d = Math.Sqrt(Math.Pow(p.X - s.Loc.X, 2) + Math.Pow(p.Y - s.Loc.Y, 2));
                if (d < bestD) { bestD = d; best = s; }
            }
            return best != null ? best.Region : "Wilderness";
        }

        private static void OnWorldLoad()
        {
            if (!File.Exists(SeededFlagFile))
            {
                Console.WriteLine("[PlayerBotSystem] First boot — seeding {0} bots...", TargetBotCount);
                var start = DateTime.UtcNow;
                SeedBots(TargetBotCount);
                Console.WriteLine("[PlayerBotSystem] Seed complete in {0:F1}s.",
                    (DateTime.UtcNow - start).TotalSeconds);
                try { File.WriteAllText(SeededFlagFile, DateTime.UtcNow.ToString("o")); } catch { }
            }
            else
            {
                SnapshotCivilianTargets();
            }
            StartRespawnTimer();
        }

        private static void StartRespawnTimer()
        {
            m_RespawnTimer?.Stop();
            if (!CivilianRespawnEnabled) return;
            m_RespawnTimer = Timer.DelayCall(
                TimeSpan.FromSeconds(CivilianRespawnSeconds),
                TimeSpan.FromSeconds(CivilianRespawnSeconds),
                RespawnTick);
        }

        private static void RespawnTick()
        {
            if (m_TargetPerRegion == null || m_TargetPerRegion.Count == 0) return;
            var table = AllSpots();

            var current = new Dictionary<string, int>();
            foreach (var bot in World.Mobiles.Values.OfType<PlayerBot>())
            {
                if (!bot.IsCivilianRole) continue;
                var region = NearestRegion(bot, table);
                if (!current.ContainsKey(region)) current[region] = 0;
                current[region]++;
            }

            int spawned = 0;
            foreach (var kv in m_TargetPerRegion)
            {
                int have = current.ContainsKey(kv.Key) ? current[kv.Key] : 0;
                int deficit = kv.Value - have;
                if (deficit <= 0) continue;

                var spots = table.Where(s => s.Region == kv.Key).ToList();
                if (spots.Count == 0) continue;

                // Roll fresh civilian roles per respawn — same random distribution.
                for (int i = 0; i < deficit && i < 5; i++)
                {
                    var roll = Utility.Random(5);  // crafter, miner, fisherman, lumberjack, tamer
                    BotRole role = BotRole.Crafter;
                    switch (roll)
                    {
                        case 0: role = BotRole.Crafter;       break;
                        case 1: role = BotRole.Miner;         break;
                        case 2: role = BotRole.Fisherman;     break;
                        case 3: role = BotRole.Lumberjack;    break;
                        case 4: role = BotRole.TamerCivilian; break;
                    }
                    var spot = spots[Utility.Random(spots.Count)];
                    SpawnAt(role, spot.Loc, Map.Felucca, spot.Jitter);
                    spawned++;
                }
            }
            if (spawned > 0)
                Console.WriteLine("[PlayerBotSystem] respawned {0} civilians", spawned);
        }

        // ---------- Commands ----------

        [Usage("addbot <role>")]
        [Description("Spawn a PlayerBot. Roles: warrior, mage, archer, healer, crafter, miner, fisherman, lumberjack, macroer, pk, tamercivilian, tamercombat.")]
        private static void AddBot_OnCommand(CommandEventArgs e)
        {
            BotRole role = BotRole.Warrior;
            if (e.Length >= 1 && !Enum.TryParse(e.GetString(0), true, out role))
            {
                e.Mobile.SendMessage("Unknown role.");
                return;
            }
            var bot = new PlayerBot(role);
            bot.MoveToWorld(e.Mobile.Location, e.Mobile.Map);
            e.Mobile.SendMessage("Spawned {0} the {1} {2}.",
                bot.Name, BotTierUtil.Label(bot.Tier), role);
        }

        [Usage("removebot")]
        [Description("Remove nearest PlayerBot.")]
        private static void RemoveBot_OnCommand(CommandEventArgs e)
        {
            var from = e.Mobile;
            PlayerBot nearest = null;
            int bestDist = int.MaxValue;
            foreach (var m in from.GetMobilesInRange(20))
                if (m is PlayerBot pb)
                {
                    int d = (int)from.GetDistanceToSqrt(m);
                    if (d < bestDist) { bestDist = d; nearest = pb; }
                }
            if (nearest == null) { from.SendMessage("No PlayerBot within 20 tiles."); return; }
            from.SendMessage("Removing: {0} ({1} {2})", nearest.Name,
                BotTierUtil.Label(nearest.Tier), nearest.Role);
            nearest.Delete();
        }

        [Usage("botcount")]
        [Description("Total PlayerBots in the world.")]
        private static void BotCount_OnCommand(CommandEventArgs e)
        {
            var bots = World.Mobiles.Values.OfType<PlayerBot>().ToList();
            int combat = bots.Count(b => b.IsCombatRole);
            int civ = bots.Count(b => b.IsCivilianRole);
            int macro = bots.Count(b => b.IsMacroer);
            int pk = bots.Count(b => b.IsPK);
            e.Mobile.SendMessage(
                "{0} bots (target {1}): {2} combat / {3} civilian / {4} macroer / {5} PK",
                bots.Count, TargetBotCount, combat, civ, macro, pk);
        }

        [Usage("botstats")]
        [Description("Distribution by role, tier, and region.")]
        private static void BotStats_OnCommand(CommandEventArgs e)
        {
            var bots = World.Mobiles.Values.OfType<PlayerBot>().ToList();
            if (bots.Count == 0) { e.Mobile.SendMessage("No bots."); return; }
            var perRole = bots.GroupBy(b => b.Role).ToDictionary(g => g.Key, g => g.Count());
            var perTier = bots.GroupBy(b => b.Tier).ToDictionary(g => g.Key, g => g.Count());
            var table = AllSpots();
            var perRegion = new Dictionary<string, int>();
            foreach (var b in bots)
            {
                var r = NearestRegion(b, table);
                if (!perRegion.ContainsKey(r)) perRegion[r] = 0;
                perRegion[r]++;
            }
            e.Mobile.SendMessage("{0} bots", bots.Count);
            e.Mobile.SendMessage("--- By tier ---");
            foreach (BotTier t in Enum.GetValues(typeof(BotTier)))
            {
                int n = perTier.ContainsKey(t) ? perTier[t] : 0;
                e.Mobile.SendMessage("  {0,-12} {1}", BotTierUtil.Label(t), n);
            }
            e.Mobile.SendMessage("--- By role ---");
            foreach (var kv in perRole.OrderByDescending(p => p.Value))
                e.Mobile.SendMessage("  {0,-14} {1}", kv.Key, kv.Value);
            e.Mobile.SendMessage("--- Top 10 regions ---");
            foreach (var kv in perRegion.OrderByDescending(p => p.Value).Take(10))
                e.Mobile.SendMessage("  {0,-26} {1}", kv.Key, kv.Value);
        }

        [Usage("botbudget <n>")]
        [Description("Set target PlayerBot population.")]
        private static void BotBudget_OnCommand(CommandEventArgs e)
        {
            if (e.Length < 1 || !int.TryParse(e.GetString(0), out int n))
            {
                e.Mobile.SendMessage("Usage: [botbudget <n>. Current: {0}", TargetBotCount);
                return;
            }
            TargetBotCount = Math.Max(0, Math.Min(2000, n));
            SaveSettings();
            e.Mobile.SendMessage("Target → {0}. Use [reseedbots to apply.", TargetBotCount);
        }

        [Usage("pkmode <players|bots|both>")]
        [Description("Set what PKs hunt.")]
        private static void PKMode_OnCommand(CommandEventArgs e)
        {
            if (e.Length < 1)
            {
                e.Mobile.SendMessage("Current: {0}", PKBehavior.TargetMode); return;
            }
            var s = e.GetString(0).ToLowerInvariant();
            PKTargetMode mode;
            switch (s)
            {
                case "players": mode = PKTargetMode.PlayersOnly; break;
                case "bots":    mode = PKTargetMode.BotsOnly;    break;
                case "both":    mode = PKTargetMode.Both;        break;
                default: e.Mobile.SendMessage("Unknown."); return;
            }
            PKBehavior.TargetMode = mode;
            SaveSettings();
            e.Mobile.SendMessage("PK target mode → {0}", mode);
        }

        [Usage("pkdensity <0..50>")]
        [Description("PKs as percent of population.")]
        private static void PKDensity_OnCommand(CommandEventArgs e)
        {
            if (e.Length < 1 || !int.TryParse(e.GetString(0), out int pct))
            {
                e.Mobile.SendMessage("Current: {0:P0}", PKDensity); return;
            }
            PKDensity = Math.Max(0, Math.Min(50, pct)) / 100.0;
            SaveSettings();
            e.Mobile.SendMessage("PK density → {0:P0}. Use [reseedbots to apply.", PKDensity);
        }

        [Usage("reseedbots")]
        [Description("Wipe and reseed.")]
        private static void ReseedBots_OnCommand(CommandEventArgs e)
        {
            var bots = World.Mobiles.Values.OfType<PlayerBot>().ToList();
            foreach (var b in bots) b.Delete();
            e.Mobile.SendMessage("Removed {0}. Reseeding {1}...", bots.Count, TargetBotCount);
            int n = SeedBots(TargetBotCount);
            e.Mobile.SendMessage("Seeded {0} bots.", n);
            StartRespawnTimer();
        }

        [Usage("clearbots")]
        [Description("Delete all bots without reseeding.")]
        private static void ClearBots_OnCommand(CommandEventArgs e)
        {
            var bots = World.Mobiles.Values.OfType<PlayerBot>().ToList();
            foreach (var b in bots) b.Delete();
            e.Mobile.SendMessage("Removed {0} bots.", bots.Count);
            m_RespawnTimer?.Stop();
        }
    }
}
