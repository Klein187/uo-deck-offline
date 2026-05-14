// PlayerBotRoles.cs — per-role configuration that scales with the bot's
// BotTier. All bots roll a tier at construction (see BotTier.cs) and
// these functions consult it.
//
// Common patterns:
//   - Primary skill range comes from BotTierUtil.PrimarySkillRange(tier)
//   - Secondary skills use BotTierUtil.SecondarySkillRange(tier)
//   - Pack quantity scales linearly with tier index (0..4)
//   - Special loot probability uses BotTierUtil.SpecialLootMultiplier(tier)

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles
{
    public static class PlayerBotRoles
    {
        // ----- Ambient chatter (unchanged from before) -----
        private static readonly string[] CrafterChatter = {
            "Hmm, this steel needs more heat.","*hammers steadily*",
            "A fine blade takes time.","Need a repair? I'm your smith.",
            "*wipes brow*"
        };
        private static readonly string[] MinerChatter = {
            "*swings pickaxe*","Veins of iron, deep and rich.",
            "My back's not what it was.","Found copper today.",
            "*coughs from the dust*"
        };
        private static readonly string[] FishermanChatter = {
            "*casts a line*","A bite! ... no, just weeds.",
            "Fish are biting today.","The sea provides.",
            "*hums an old shanty*"
        };
        private static readonly string[] LumberjackChatter = {
            "*swings axe*","Timberrr!","Good yew this season.",
            "Twenty boards this morn.","*spits, wipes brow*"
        };

        // ----- AI / FightMode -----

        public static AIType AIForRole(BotRole r)
        {
            switch (r)
            {
                case BotRole.Mage:      return AIType.AI_Mage;
                case BotRole.Archer:    return AIType.AI_Archer;
                case BotRole.Healer:    return AIType.AI_Healer;
                case BotRole.Warrior:   return AIType.AI_Melee;
                case BotRole.PK:        return AIType.AI_Melee;
                case BotRole.Macroer:   return AIType.AI_Mage;
                case BotRole.TamerCombat: return AIType.AI_Melee;
                default:                return AIType.AI_Animal;
            }
        }

        public static FightMode FightModeForRole(BotRole r)
        {
            switch (r)
            {
                case BotRole.PK:
                case BotRole.Warrior:
                case BotRole.Mage:
                case BotRole.Archer:
                case BotRole.Healer:
                case BotRole.TamerCombat:
                    return FightMode.Closest;
                default:
                    return FightMode.Aggressor;
            }
        }

        public static void ConfigureRole(PlayerBot bot, BotRole role)
        {
            switch (role)
            {
                case BotRole.Warrior:       Warrior(bot);       break;
                case BotRole.Mage:          Mage(bot);          break;
                case BotRole.Archer:        Archer(bot);        break;
                case BotRole.Healer:        Healer(bot);        break;
                case BotRole.Crafter:       Crafter(bot);       break;
                case BotRole.Miner:         Miner(bot);         break;
                case BotRole.Fisherman:     Fisherman(bot);     break;
                case BotRole.Lumberjack:    Lumberjack(bot);    break;
                case BotRole.Macroer:       Macroer(bot);       break;
                case BotRole.PK:            PK(bot);            break;
                case BotRole.TamerCivilian: TamerCivilian(bot); break;
                case BotRole.TamerCombat:   TamerCombat(bot);   break;
                case BotRole.Bard:          Warrior(bot);       break; // legacy fallthrough
            }
        }

        // ===== Combat =====

        private static void Warrior(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Swords, pmin, pmax);
            b.SetSkill(SkillName.Tactics, smin, smax);
            b.SetSkill(SkillName.Anatomy, smin, smax);
            b.SetSkill(SkillName.MagicResist, smin, smax);
            b.SetSkill(SkillName.Parry, smin - 10, smax - 10);

            // Weapon scales with tier
            switch (b.Tier)
            {
                case BotTier.Novice:      b.AddItem(new Cutlass()); break;
                case BotTier.Apprentice:  b.AddItem(new Longsword());  break;
                case BotTier.Adept:       b.AddItem(new Katana());     break;
                case BotTier.Expert:      b.AddItem(new Broadsword()); break;
                case BotTier.Grandmaster: b.AddItem(new VikingSword());break;
            }

            // Armor scales with tier
            if (b.Tier >= BotTier.Adept)
            {
                b.AddItem(new ChainChest());
                b.AddItem(new ChainLegs());
                b.AddItem(new RingmailArms());
            }
            else
            {
                b.AddItem(new StuddedChest());
                b.AddItem(new StuddedLegs());
                b.AddItem(new StuddedArms());
            }
            if (b.Tier == BotTier.Grandmaster) b.AddItem(new PlateHelm());

            b.AddItem(new Boots(Utility.RandomNeutralHue()));

            // Pack
            if (Utility.RandomDouble() < 0.5) b.PackItem(new LesserHealPotion());
            if (b.Tier >= BotTier.Expert)
                for (int i = 0; i < Utility.RandomMinMax(2, 5); i++) b.PackItem(new GreaterHealPotion());

            MaybeAddSpecialLoot(b, 0.05);
        }

        private static void Mage(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Magery,     pmin, pmax);
            b.SetSkill(SkillName.EvalInt,    smin, smax);
            b.SetSkill(SkillName.Meditation, smin, smax);
            b.SetSkill(SkillName.MagicResist,smin, smax);

            b.AddItem(new Robe(Utility.RandomNondyedHue()));
            b.AddItem(new WizardsHat(Utility.RandomNondyedHue()));
            b.AddItem(new Sandals(Utility.RandomNeutralHue()));

            // Spellbook content scales with tier
            var book = new Spellbook();
            switch (b.Tier)
            {
                case BotTier.Novice:      book.Content = 0xFFFUL;                 break; // 1st-3rd circle
                case BotTier.Apprentice:  book.Content = 0xFFFFFFUL;              break; // 1st-6th
                case BotTier.Adept:       book.Content = 0xFFFFFFFFUL;            break; // 1st-8th, partial
                case BotTier.Expert:      book.Content = 0xFFFFFFFFFFFFUL;        break;
                case BotTier.Grandmaster: book.Content = ulong.MaxValue;          break; // all
            }
            b.AddItem(book);

            // Reagents scale linearly with tier
            int regsPer = 5 + 10 * (int)b.Tier;  // 5,15,25,35,45
            PackReagents(b, regsPer);

            // GM mages sometimes carry a magic wand (special loot)
            MaybeAddSpecialLoot(b, 0.05);
        }

        private static void Archer(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Archery, pmin, pmax);
            b.SetSkill(SkillName.Tactics, smin, smax);
            b.SetSkill(SkillName.Anatomy, smin, smax);

            // Bow type scales: novice has plain bow, GM has heavy crossbow
            switch (b.Tier)
            {
                case BotTier.Novice:
                case BotTier.Apprentice:  b.AddItem(new Bow()); break;
                case BotTier.Adept:       b.AddItem(Utility.RandomBool() ? (Item)new Bow() : new Crossbow()); break;
                case BotTier.Expert:      b.AddItem(new Crossbow()); break;
                case BotTier.Grandmaster: b.AddItem(new HeavyCrossbow()); break;
            }

            int ammoCount = 30 + 20 * (int)b.Tier;  // 30..110
            for (int i = 0; i < ammoCount; i++) b.PackItem(new Arrow());
            if (b.Tier >= BotTier.Expert)
                for (int i = 0; i < ammoCount / 2; i++) b.PackItem(new Bolt());

            b.AddItem(new LeatherChest());
            b.AddItem(new LeatherLegs());
            b.AddItem(new LeatherArms());
            b.AddItem(new Boots(Utility.RandomNeutralHue()));

            MaybeAddSpecialLoot(b, 0.05);
        }

        private static void Healer(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Healing, pmin, pmax);
            b.SetSkill(SkillName.Anatomy, pmin - 5, pmax - 5);
            b.SetSkill(SkillName.Magery,  smin, smax);
            b.AddItem(new Robe(0x47E));
            b.AddItem(new Sandals());
            int bandages = 10 + 10 * (int)b.Tier;
            for (int i = 0; i < bandages; i++) b.PackItem(new Bandage());
            if (b.Tier >= BotTier.Expert) PackReagents(b, 10);
            MaybeAddSpecialLoot(b, 0.05);
        }

        // ===== Civilian =====

        private static void Crafter(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Blacksmith, pmin, pmax);
            b.SetSkill(SkillName.Mining, smin, smax);
            b.SetSkill(SkillName.Tinkering, smin - 10, smax - 10);
            b.SetSkill(SkillName.ArmsLore, smin, smax);

            b.AddItem(new HalfApron(Utility.RandomNeutralHue()));
            b.AddItem(new ShortPants(Utility.RandomNeutralHue()));
            b.AddItem(new Boots());
            b.AddItem(new SmithHammer());

            int ingots = 10 + 30 * (int)b.Tier;  // 10,40,70,100,130
            b.PackItem(new IronIngot(ingots));
            if (b.Tier >= BotTier.Adept && Utility.RandomDouble() < 0.4)
                b.PackItem(new DullCopperIngot(Utility.RandomMinMax(5, 15)));
            if (b.Tier >= BotTier.Expert && Utility.RandomDouble() < 0.25)
                b.PackItem(new CopperIngot(Utility.RandomMinMax(3, 10)));
            if (b.Tier == BotTier.Grandmaster && Utility.RandomDouble() < 0.10)
                b.PackItem(new GoldIngot(Utility.RandomMinMax(2, 5)));

            // Tools
            if (Utility.RandomDouble() < 0.3 + 0.1 * (int)b.Tier) b.PackItem(new Tongs());
            if (b.Tier >= BotTier.Expert && Utility.RandomDouble() < 0.4) b.PackItem(new TinkerTools());

            // Finished weapon — GMs sometimes carry a magical one
            if (b.Tier >= BotTier.Adept && Utility.RandomDouble() < 0.25)
                b.PackItem(new Dagger());
            if (b.Tier >= BotTier.Expert && Utility.RandomDouble() < 0.15)
                b.PackItem(new Longsword());

            MaybeAddSpecialLoot(b, 0.02);
        }

        private static void Miner(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            b.SetSkill(SkillName.Mining, pmin, pmax);

            b.AddItem(new FullApron(Utility.RandomNeutralHue()));
            b.AddItem(new ShortPants(Utility.RandomNeutralHue()));
            b.AddItem(new Boots());
            b.AddItem(new Pickaxe());

            // Carry-on-self ore — small amount, scales with tier and uses
            // the same tier-aware ore table as the mule.
            int onSelf = 5 + 5 * (int)b.Tier;  // 5..25
            for (int i = 0; i < onSelf; i++)
                b.PackItem(PlayerBotPets.RollOreFor(b.Tier));

            if (Utility.RandomDouble() < 0.2 + 0.1 * (int)b.Tier) b.PackItem(new Shovel());
            if (b.Tier >= BotTier.Expert && Utility.RandomDouble() < 0.15)
                b.PackItem(new BagOfReagents(15));

            MaybeAddSpecialLoot(b, 0.02);

            // 80% of miners get a pack mule (any tier) — see PlayerBotSystem.SpawnAt
            b.WantsPackMule = (Utility.RandomDouble() < 0.80);
        }

        private static void Fisherman(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.Fishing, pmin, pmax);
            b.SetSkill(SkillName.Cartography, smin, smax);

            b.AddItem(new Shirt(Utility.RandomNondyedHue()));
            b.AddItem(new LongPants(Utility.RandomNondyedHue()));
            b.AddItem(new Sandals());
            b.AddItem(new FishingPole());

            int fish = 2 + 3 * (int)b.Tier;  // 2..14
            for (int i = 0; i < fish; i++) b.PackItem(new Fish());

            // Treasure maps and SOS — GMs much more likely
            double sosChance = 0.05 + 0.10 * (int)b.Tier;  // 5..45%
            if (Utility.RandomDouble() < sosChance) b.PackItem(new SOS());
            if (b.Tier >= BotTier.Expert && Utility.RandomDouble() < 0.20)
                b.PackItem(new TreasureMap(Utility.Random(1, Math.Min(5, (int)b.Tier)), Map.Felucca));

            MaybeAddSpecialLoot(b, 0.02);
        }

        private static void Lumberjack(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            b.SetSkill(SkillName.Lumberjacking, pmin, pmax);
            b.SetSkill(SkillName.Tactics, 30, 50);

            b.AddItem(new Shirt(Utility.RandomNondyedHue()));
            b.AddItem(new ShortPants(Utility.RandomNondyedHue()));
            b.AddItem(new Boots());
            b.AddItem(new Hatchet());

            int logs = 5 + 10 * (int)b.Tier;
            b.PackItem(new Log(logs));
            if (b.Tier >= BotTier.Adept && Utility.RandomDouble() < 0.4)
                b.PackItem(new Board(Utility.RandomMinMax(5, 20)));
            // Sometimes a GM lumberjack has rare wood (treat as logs for now)
            if (b.Tier == BotTier.Grandmaster && Utility.RandomDouble() < 0.15)
                b.PackItem(new Log(Utility.RandomMinMax(20, 50)));

            MaybeAddSpecialLoot(b, 0.02);
        }

        private static void TamerCivilian(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.AnimalTaming,  pmin, pmax);
            b.SetSkill(SkillName.AnimalLore,    pmin - 5, pmax - 5);
            b.SetSkill(SkillName.Veterinary,    smin, smax);
            b.SetSkill(SkillName.Herding,       smin - 15, smax - 15);
            b.AddItem(new Tunic(Utility.RandomNondyedHue()));
            b.AddItem(new LongPants(Utility.RandomNondyedHue()));
            b.AddItem(new Boots());
            b.AddItem(new ShepherdsCrook());
            b.WantsPeacefulPet = true;
            MaybeAddSpecialLoot(b, 0.02);
        }

        private static void TamerCombat(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);
            b.SetSkill(SkillName.AnimalTaming, pmin, pmax);
            b.SetSkill(SkillName.AnimalLore,   pmin - 5, pmax - 5);
            b.SetSkill(SkillName.Veterinary,   smin, smax);
            b.SetSkill(SkillName.Tactics,      smin, smax);
            b.SetSkill(SkillName.MagicResist,  smin, smax);
            b.SetSkill(SkillName.Magery,       smin - 10, smax - 10);
            b.AddItem(new LeatherChest());
            b.AddItem(new LeatherLegs());
            b.AddItem(new LeatherArms());
            b.AddItem(new Boots(Utility.RandomNeutralHue()));
            b.AddItem(new ShepherdsCrook());
            int bands = 10 + 5 * (int)b.Tier;
            for (int i = 0; i < bands; i++) b.PackItem(new Bandage());
            PackReagents(b, 5 + 5 * (int)b.Tier);
            b.WantsCombatPet = true;
            MaybeAddSpecialLoot(b, 0.05);
        }

        // ===== Macroer =====
        private static void Macroer(PlayerBot b)
        {
            // Macroers are mid-tier in their training skills regardless of
            // the bot's overall BotTier (the joke is they're macroing UP to
            // skill). We use Adept range for all the things they train.
            var (smin, smax) = BotTierUtil.SecondarySkillRange(BotTier.Adept);
            b.SetSkill(SkillName.Magery,        smin, smax);
            b.SetSkill(SkillName.EvalInt,       smin - 10, smax - 10);
            b.SetSkill(SkillName.Meditation,    smin - 10, smax - 10);
            b.SetSkill(SkillName.Hiding,        smin - 10, smax - 10);
            b.SetSkill(SkillName.Healing,       smin - 10, smax - 10);
            b.SetSkill(SkillName.Anatomy,       smin - 10, smax - 10);
            b.SetSkill(SkillName.MagicResist,   smin - 10, smax - 10);
            b.SetSkill(SkillName.Musicianship,  smin - 10, smax - 10);
            b.SetSkill(SkillName.AnimalTaming,  smin - 10, smax - 10);
            b.AddItem(new Robe(Utility.RandomNondyedHue()));
            b.AddItem(new Sandals());
            var book = new Spellbook { Content = 0xFFFFFFFFUL };
            b.AddItem(book);
            PackReagents(b, 30);
            for (int i = 0; i < 30; i++) b.PackItem(new Bandage());
            switch (Utility.Random(4))
            {
                case 0: b.PackItem(new Lute());       break;
                case 1: b.PackItem(new LapHarp());    break;
                case 2: b.PackItem(new Tambourine()); break;
                case 3: b.PackItem(new Drums());      break;
            }
        }

        // ===== PK — fully random gear + tier-scaled skill/loot =====
        private enum PKKind { Sword, Macer, Fencer, Archer, Mage }

        private static void PK(PlayerBot b)
        {
            var (pmin, pmax) = BotTierUtil.PrimarySkillRange(b.Tier);
            var (smin, smax) = BotTierUtil.SecondarySkillRange(b.Tier);

            b.SetHits(70 + 10 * (int)b.Tier, 90 + 10 * (int)b.Tier);
            b.SetDamage(10, 16);
            b.SetSkill(SkillName.Tactics,     pmin - 5, pmax);
            b.SetSkill(SkillName.Anatomy,     pmin - 5, pmax);
            b.SetSkill(SkillName.MagicResist, pmin - 5, pmax);
            b.SetSkill(SkillName.Healing,     smin, smax);
            b.SetSkill(SkillName.Hiding,      smin - 10, smax - 10);

            PKKind kind = (PKKind)Utility.Random(5);

            switch (kind)
            {
                case PKKind.Sword:
                    b.SetSkill(SkillName.Swords, pmin, pmax);
                    b.AddItem(Utility.RandomBool() ? (Item)new Katana() : new Longsword());
                    break;
                case PKKind.Macer:
                    b.SetSkill(SkillName.Macing, pmin, pmax);
                    b.AddItem(Utility.RandomBool() ? (Item)new WarHammer() : new Maul());
                    break;
                case PKKind.Fencer:
                    b.SetSkill(SkillName.Fencing, pmin, pmax);
                    b.AddItem(Utility.RandomList<Item>(
                        new Kryss(), new Dagger(), new Spear()));
                    break;
                case PKKind.Archer:
                    b.SetSkill(SkillName.Archery, pmin, pmax);
                    b.AddItem(Utility.RandomBool() ? (Item)new Bow() : new Crossbow());
                    int arrows = 30 + 20 * (int)b.Tier;
                    for (int i = 0; i < arrows; i++) b.PackItem(new Arrow());
                    for (int i = 0; i < arrows / 3; i++) b.PackItem(new Bolt());
                    break;
                case PKKind.Mage:
                    b.SetSkill(SkillName.Magery,     pmin, pmax);
                    b.SetSkill(SkillName.EvalInt,    smin, smax);
                    b.SetSkill(SkillName.Meditation, smin, smax);
                    var book = new Spellbook { Content = ulong.MaxValue };
                    b.AddItem(book);
                    PackReagents(b, 20 + 10 * (int)b.Tier);
                    break;
            }

            RandomizePKArmor(b, kind);

            int bands = 15 + 5 * (int)b.Tier;
            for (int i = 0; i < bands; i++) b.PackItem(new Bandage());
            int pots = 2 + (int)b.Tier;
            for (int i = 0; i < pots; i++)
                b.PackItem(Utility.RandomBool() ? (Item)new GreaterHealPotion() : new GreaterCurePotion());

            // PK gold + special loot — scales sharply with tier
            int gold = 50 + 100 * (int)b.Tier;  // 50..450
            b.PackItem(new Gold(Utility.RandomMinMax(gold / 2, gold)));
            MaybeAddSpecialLoot(b, 0.10);  // higher base than civilians
        }

        private static void RandomizePKArmor(PlayerBot b, PKKind kind)
        {
            if (Utility.RandomDouble() < 0.5)
                b.AddItem(new Cloak(Utility.RandomList(0x455, 0x497, 0x21, 0x0)));

            if (kind == PKKind.Mage)
            {
                if (Utility.RandomDouble() < 0.7)
                {
                    b.AddItem(new Robe(Utility.RandomList(0x455, 0x21, 0x497, 0x0)));
                    b.AddItem(new Sandals(Utility.RandomNeutralHue()));
                    if (Utility.RandomDouble() < 0.4)
                        b.AddItem(new WizardsHat(Utility.RandomList(0x455, 0x0)));
                    return;
                }
                b.AddItem(new LeatherChest());
                b.AddItem(new LeatherLegs());
                b.AddItem(new Boots(0x497));
                return;
            }

            int tier = Utility.Random(5);
            switch (tier)
            {
                case 0:
                    b.AddItem(new LeatherChest()); b.AddItem(new LeatherLegs());
                    b.AddItem(new LeatherArms()); b.AddItem(new LeatherGloves());
                    break;
                case 1:
                    b.AddItem(new StuddedChest()); b.AddItem(new StuddedLegs());
                    b.AddItem(new StuddedArms()); b.AddItem(new StuddedGloves());
                    break;
                case 2:
                    b.AddItem(new RingmailChest()); b.AddItem(new RingmailLegs());
                    b.AddItem(new RingmailArms()); b.AddItem(new RingmailGloves());
                    break;
                case 3:
                    b.AddItem(new ChainChest()); b.AddItem(new ChainLegs());
                    b.AddItem(new RingmailArms());
                    break;
                case 4:
                    b.AddItem(new PlateChest()); b.AddItem(new PlateLegs());
                    b.AddItem(new PlateArms()); b.AddItem(new PlateGloves());
                    if (Utility.RandomDouble() < 0.5) b.AddItem(new PlateHelm());
                    break;
            }
            b.AddItem(new Boots(Utility.RandomList(0x497, 0x21, 0x0)));
        }

        // ===== Shared helpers =====

        public static void PackReagents(PlayerBot b, int each)
        {
            b.PackItem(new BlackPearl(each));
            b.PackItem(new Bloodmoss(each));
            b.PackItem(new Garlic(each));
            b.PackItem(new Ginseng(each));
            b.PackItem(new MandrakeRoot(each));
            b.PackItem(new Nightshade(each));
            b.PackItem(new SulfurousAsh(each));
            b.PackItem(new SpidersSilk(each));
        }

        // Adjusts the base "special loot" probability by the tier multiplier
        // and rolls. GMs are ~4x more likely than mid-tier to carry valuables.
        private static void MaybeAddSpecialLoot(PlayerBot b, double baseChance)
        {
            double chance = baseChance * BotTierUtil.SpecialLootMultiplier(b.Tier);
            if (Utility.RandomDouble() < chance) AddSpecialLoot(b);
        }

        private static void AddSpecialLoot(PlayerBot b)
        {
            // Loot pool also scales with tier — GMs see better drops.
            int pool = Math.Min(8, 4 + (int)b.Tier);  // 4..8
            switch (Utility.Random(pool))
            {
                case 0: b.PackItem(new Gold(Utility.RandomMinMax(200, 800)));       break;
                case 1: b.PackItem(new Diamond(Utility.RandomMinMax(1, 3)));        break;
                case 2: b.PackItem(new Ruby(Utility.RandomMinMax(1, 3)));           break;
                case 3: b.PackItem(new Sapphire(Utility.RandomMinMax(1, 3)));       break;
                case 4: b.PackItem(new Emerald(Utility.RandomMinMax(1, 3)));        break;
                case 5: b.PackItem(new GreaterCurePotion());                        break;
                case 6: b.PackItem(new MagicWizardsHat());                          break;
                case 7: b.PackItem(new BagOfReagents(50));                          break;
            }
        }

        // ===== Civilian ambient activity =====

        public static void DoCivilianActivity(PlayerBot b)
        {
            switch (b.Role)
            {
                case BotRole.Crafter:
                    b.Animate(11, 5, 1, true, false, 0);
                    Effects.PlaySound(b.Location, b.Map, 0x2A);
                    if (Utility.RandomBool()) b.Say(CrafterChatter[Utility.Random(CrafterChatter.Length)]);
                    break;
                case BotRole.Miner:
                    b.Animate(11, 5, 1, true, false, 0);
                    Effects.PlaySound(b.Location, b.Map, 0x125);
                    if (Utility.RandomBool()) b.Say(MinerChatter[Utility.Random(MinerChatter.Length)]);
                    break;
                case BotRole.Fisherman:
                    b.Animate(12, 5, 1, true, false, 0);
                    Effects.PlaySound(b.Location, b.Map, 0x364);
                    if (Utility.RandomBool()) b.Say(FishermanChatter[Utility.Random(FishermanChatter.Length)]);
                    break;
                case BotRole.Lumberjack:
                    b.Animate(11, 5, 1, true, false, 0);
                    Effects.PlaySound(b.Location, b.Map, 0x13E);
                    if (Utility.RandomBool()) b.Say(LumberjackChatter[Utility.Random(LumberjackChatter.Length)]);
                    break;
            }
        }
    }
}
