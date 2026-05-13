// BotTier.cs — Bell-curve skill tier system. Every PlayerBot rolls a tier
// at construction, and per-role configurations use the tier to scale
// skill ranges, equipment quality, and pack contents.
//
// Tier ranges (T2A-style nomenclature):
//   Novice       30-50
//   Apprentice   50-70
//   Adept        70-85
//   Expert       85-95
//   Grandmaster  95-110
//
// Bell-curve distribution at roll time:
//   Novice 10%, Apprentice 22%, Adept 36%, Expert 22%, Grandmaster 10%

using System;
using Server;

namespace Server.Mobiles
{
    public enum BotTier
    {
        Novice,
        Apprentice,
        Adept,
        Expert,
        Grandmaster
    }

    public static class BotTierUtil
    {
        // Bell curve as cumulative thresholds out of 100.
        // 0..9 = Novice, 10..31 = Apprentice, 32..67 = Adept,
        // 68..89 = Expert, 90..99 = Grandmaster.
        public static BotTier RollTier()
        {
            int r = Utility.Random(100);
            if (r < 10) return BotTier.Novice;
            if (r < 32) return BotTier.Apprentice;
            if (r < 68) return BotTier.Adept;
            if (r < 90) return BotTier.Expert;
            return BotTier.Grandmaster;
        }

        // Skill range floor/ceiling per tier. Used for the "primary" skill
        // of a role (mining for miners, swords for warriors, etc).
        public static (double, double) PrimarySkillRange(BotTier t)
        {
            switch (t)
            {
                case BotTier.Novice:      return (30, 50);
                case BotTier.Apprentice:  return (50, 70);
                case BotTier.Adept:       return (70, 85);
                case BotTier.Expert:      return (85, 95);
                case BotTier.Grandmaster: return (95, 110);
            }
            return (50, 70);
        }

        // Range for the role's secondary/supporting skills. These scale
        // along with the primary but on a slightly tighter range so a
        // GM warrior isn't also GM-magery-resistance, just very good at it.
        public static (double, double) SecondarySkillRange(BotTier t)
        {
            switch (t)
            {
                case BotTier.Novice:      return (20, 40);
                case BotTier.Apprentice:  return (35, 60);
                case BotTier.Adept:       return (55, 75);
                case BotTier.Expert:      return (70, 85);
                case BotTier.Grandmaster: return (80, 95);
            }
            return (40, 60);
        }

        // Stat caps scale a bit too — GMs are physically tougher.
        public static (int strMin, int strMax) StrRangeFor(BotTier t)
        {
            switch (t)
            {
                case BotTier.Novice:      return (50, 75);
                case BotTier.Apprentice:  return (60, 85);
                case BotTier.Adept:       return (70, 95);
                case BotTier.Expert:      return (85, 105);
                case BotTier.Grandmaster: return (95, 110);
            }
            return (60, 90);
        }

        // Pretty-print for chat messages and the [botstats command.
        public static string Label(BotTier t)
        {
            switch (t)
            {
                case BotTier.Novice:      return "Novice";
                case BotTier.Apprentice:  return "Apprentice";
                case BotTier.Adept:       return "Adept";
                case BotTier.Expert:      return "Expert";
                case BotTier.Grandmaster: return "Grandmaster";
            }
            return "Unknown";
        }

        // Used by special-loot rolls: returns multiplier on the base 2%
        // drop chance. GMs are much more likely to be carrying something
        // valuable than novices.
        public static double SpecialLootMultiplier(BotTier t)
        {
            switch (t)
            {
                case BotTier.Novice:      return 0.2;   // very rare
                case BotTier.Apprentice:  return 0.5;
                case BotTier.Adept:       return 1.0;   // baseline
                case BotTier.Expert:      return 2.0;
                case BotTier.Grandmaster: return 4.0;   // common, real loot
            }
            return 1.0;
        }
    }
}
