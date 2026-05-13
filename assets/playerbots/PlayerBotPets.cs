// PlayerBotPets.cs — helpers for giving a PlayerBot a follower.
// Pet selection scales with the bot's BotTier:
//   - Peaceful tamer civilians pick from a tier-appropriate small-pet pool.
//   - Combat tamers pick from a tier-appropriate fighting pool, with GMs
//     usually having dragon-tier pets but sometimes a normal bear/wolf.
// Pack animals (miners) also scale: novices have iron-only mules, GMs
// have mules with rare colored ores mixed in.

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles
{
    public static class PlayerBotPets
    {
        // Peaceful pet for civilian tamers. Tier affects exoticness.
        public static BaseCreature CreatePeacefulPet(BotTier tier)
        {
            switch (tier)
            {
                case BotTier.Novice:
                    return Utility.RandomList<BaseCreature>(
                        new Chicken(), new Rabbit(), new Cat());
                case BotTier.Apprentice:
                    return Utility.RandomList<BaseCreature>(
                        new Cat(), new Dog(), new Pig(), new Sheep(), new Goat());
                case BotTier.Adept:
                    return Utility.RandomList<BaseCreature>(
                        new Dog(), new Sheep(), new Cow(), new Horse(), new Llama());
                case BotTier.Expert:
                    return Utility.RandomList<BaseCreature>(
                        new Horse(), new Llama(), new Cow(),
                        new Bull(), new GreatHart());
                case BotTier.Grandmaster:
                    // Civilian GMs still keep peaceful pets — rare and impressive
                    // but not aggressive. The dragon-tier stuff is for combat tamers.
                    return Utility.RandomList<BaseCreature>(
                        new Horse(), new Llama(), new Bull(),
                        new GreatHart(), new Eagle(), new SnowLeopard());
            }
            return new Dog();
        }

        // Combat pet for adventuring tamers. GMs *usually* have dragon-tier
        // mounts but sometimes a regular bear/wolf for variety.
        public static BaseCreature CreateCombatPet(BotTier tier)
        {
            switch (tier)
            {
                case BotTier.Novice:
                    return Utility.RandomList<BaseCreature>(
                        new TimberWolf(), new GreyWolf(), new BlackBear());
                case BotTier.Apprentice:
                    return Utility.RandomList<BaseCreature>(
                        new BrownBear(), new GreyWolf(), new TimberWolf(),
                        new Cougar(), new Panther());
                case BotTier.Adept:
                    return Utility.RandomList<BaseCreature>(
                        new GrizzlyBear(), new PolarBear(), new BrownBear(),
                        new HellHound(), new DireWolf(), new GiantSerpent());
                case BotTier.Expert:
                    return Utility.RandomList<BaseCreature>(
                        new GrizzlyBear(), new PolarBear(), new HellHound(),
                        new DireWolf(), new DesertOstard(), new FrenziedOstard(),
                        new SnowLeopard(), new PredatorHellCat());
                case BotTier.Grandmaster:
                    // 70% dragon-tier, 30% something else for variety.
                    if (Utility.Random(100) < 70)
                    {
                        return Utility.RandomList<BaseCreature>(
                            new Dragon(),
                            new Drake(),
                            new WhiteWyrm(),
                            new Nightmare(),
                            new Wyvern());
                    }
                    return Utility.RandomList<BaseCreature>(
                        new GrizzlyBear(), new HellHound(), new DireWolf(),
                        new FrenziedOstard());
            }
            return new GrizzlyBear();
        }

        // Pack mule for miners. Tier affects ore quantity and rarity.
        public static BaseCreature CreatePackAnimalWithOre(BotTier tier)
        {
            BaseCreature pack = Utility.RandomBool()
                ? (BaseCreature)new PackHorse()
                : new PackLlama();

            if (pack.Backpack == null)
                pack.AddItem(new Backpack());

            // Ore quantity scales with tier.
            int oreCount;
            switch (tier)
            {
                case BotTier.Novice:      oreCount = Utility.RandomMinMax(10, 25);  break;
                case BotTier.Apprentice:  oreCount = Utility.RandomMinMax(20, 40);  break;
                case BotTier.Adept:       oreCount = Utility.RandomMinMax(30, 60);  break;
                case BotTier.Expert:      oreCount = Utility.RandomMinMax(40, 80);  break;
                case BotTier.Grandmaster: oreCount = Utility.RandomMinMax(60, 120); break;
                default:                  oreCount = 30; break;
            }

            for (int i = 0; i < oreCount; i++)
            {
                pack.Backpack.DropItem(RollOreFor(tier));
            }
            return pack;
        }

        // Per-tier ore loot table. Novices: iron only. GMs: rare chance of
        // valorite. The colored ores between scale gracefully.
        public static Item RollOreFor(BotTier tier)
        {
            int roll = Utility.Random(1000);
            switch (tier)
            {
                case BotTier.Novice:
                    // 100% iron
                    return new IronOre();
                case BotTier.Apprentice:
                    // 95% iron, 5% dull copper
                    if (roll < 950) return new IronOre();
                    return new DullCopperOre();
                case BotTier.Adept:
                    // 80% iron, 12% dull copper, 5% shadow iron, 3% copper
                    if (roll < 800)  return new IronOre();
                    if (roll < 920)  return new DullCopperOre();
                    if (roll < 970)  return new ShadowIronOre();
                    return new CopperOre();
                case BotTier.Expert:
                    // 60% iron, 18% dull copper, 12% shadow iron, 6% copper,
                    // 3% bronze, 1% gold
                    if (roll < 600)  return new IronOre();
                    if (roll < 780)  return new DullCopperOre();
                    if (roll < 900)  return new ShadowIronOre();
                    if (roll < 960)  return new CopperOre();
                    if (roll < 990)  return new BronzeOre();
                    return new GoldOre();
                case BotTier.Grandmaster:
                    // 35% iron, 20% dull copper, 18% shadow iron, 12% copper,
                    // 8% bronze, 4% gold, 2% agapite, 0.7% verite, 0.3% valorite
                    if (roll < 350)  return new IronOre();
                    if (roll < 550)  return new DullCopperOre();
                    if (roll < 730)  return new ShadowIronOre();
                    if (roll < 850)  return new CopperOre();
                    if (roll < 930)  return new BronzeOre();
                    if (roll < 970)  return new GoldOre();
                    if (roll < 990)  return new AgapiteOre();
                    if (roll < 997)  return new VeriteOre();
                    return new ValoriteOre();
            }
            return new IronOre();
        }

        public static void AttachAsFollower(PlayerBot master, BaseCreature pet)
        {
            try
            {
                pet.SetControlMaster(master);
                pet.Controlled = true;
                pet.ControlOrder = OrderType.Follow;
                pet.ControlTarget = master;
                pet.IsBonded = true;
                pet.Loyalty = 100;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotPets] AttachAsFollower failed: {0}", ex.Message);
            }
        }

        public static BaseCreature SpawnPetFor(PlayerBot master, BaseCreature pet)
        {
            var loc = new Point3D(
                master.X + Utility.RandomMinMax(-1, 1),
                master.Y + Utility.RandomMinMax(-1, 1),
                master.Z);
            pet.MoveToWorld(loc, master.Map);
            AttachAsFollower(master, pet);
            return pet;
        }
    }
}
