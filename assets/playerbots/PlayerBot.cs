// PlayerBot.cs — base class for all bot kinds.
// Role-specific code lives in PlayerBotRoles.cs, MacroerBehavior.cs, PKBehavior.cs.

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles
{
    public enum BotRole
    {
        // Combat (0-3)
        Warrior,
        Mage,
        Archer,
        Healer,

        // Civilian (4-7) — Bard reserved but no longer spawned as civilian.
        Crafter,
        Miner,
        Fisherman,
        Bard,         // DEPRECATED as civilian role; kept for save compat.

        // Specialty civilian (8)
        Lumberjack,

        // AFK macroers (9)
        Macroer,

        // PK (10)
        PK,

        // Tamer civilian — wanders peacefully with a tame pet (11)
        TamerCivilian,

        // Tamer combat — adventurer with a combat pet (12)
        TamerCombat,
    }

    // Legacy enum kept for backward compat.
    public enum BotClass { Warrior = 0, Mage = 1, Archer = 2, Healer = 3 }

    public class PlayerBot : BaseCreature
    {
        private static readonly string[] MaleNames = {
            "Garrick","Hammond","Edric","Cyrus","Roland","Aldric",
            "Marcus","Thane","Brennan","Corwin","Dane","Erik",
            "Finn","Galen","Holden","Ian","Joren","Kael",
            "Magnus","Nolan","Orin","Percy","Quinn","Ruric"
        };
        private static readonly string[] FemaleNames = {
            "Aria","Briar","Cera","Dara","Elsa","Faye",
            "Gwen","Hilde","Isla","Jenna","Kyra","Lia",
            "Mira","Nora","Orla","Pia","Quinn","Rina",
            "Sela","Tessa","Una","Vesna","Wren","Yara"
        };
        private static readonly string[] PKNames = {
            "Reaper","Bloodfang","Dirgewind","Ashthorn",
            "Killian","Mortis","Sablefell","Greythorne",
            "Vex","Murk","Carrion","Strix"
        };
        private static readonly string[] PKEpithets = {
            "the Cruel","the Bloody","the Black","the Vile","the Grim",
            "the Wicked","the Pale","the Hooded","the Faceless"
        };

        private static readonly string[] AdventurerChatter = {
            "Stay thy blade, friend.","Hail.",
            "These lands are not as they once were.",
            "Mind the brigands on the road to Vesper.",
            "Well met.","A fine day for adventure.",
            "By Lord British's beard...",
            "Heard tell of treasure in the Lost Lands."
        };

        private static readonly string[] PKTaunts = {
            "Your purse, or your life.",
            "Easy pickings.",
            "*draws blade*",
            "You shouldn't be out here alone.",
            "No witnesses.",
            "Run while you can."
        };

        public BotRole Role { get; private set; }
        public BotTier Tier { get; private set; }

        public BotClass BotClass
        {
            get
            {
                switch (Role)
                {
                    case BotRole.Warrior: return BotClass.Warrior;
                    case BotRole.Mage:    return BotClass.Mage;
                    case BotRole.Archer:  return BotClass.Archer;
                    case BotRole.Healer:  return BotClass.Healer;
                    default:              return BotClass.Warrior;
                }
            }
        }

        public bool IsCombatRole =>
            (int)Role <= (int)BotRole.Healer ||
            Role == BotRole.TamerCombat;
        // Civilians no longer include Bard, but do include peaceful tamer.
        public bool IsCivilianRole =>
            Role == BotRole.Crafter   ||
            Role == BotRole.Miner     ||
            Role == BotRole.Fisherman ||
            Role == BotRole.Lumberjack ||
            Role == BotRole.TamerCivilian;
        public bool IsMacroer => Role == BotRole.Macroer;
        public bool IsPK => Role == BotRole.PK;

        public MacroerBehavior MacroerBrain;
        public PKBehavior PKBrain;

        // Set during Configure() to signal that PlayerBotSystem should
        // attach a follower (pack mule for miners, pet for tamers) after
        // MoveToWorld places the master.
        public bool WantsPackMule { get; set; }
        public bool WantsPeacefulPet { get; set; }
        public bool WantsCombatPet { get; set; }

        private DateTime m_NextActivity = DateTime.UtcNow;

        [Constructable]
        public PlayerBot() : this(BotRole.Warrior) { }

        [Constructable]
        public PlayerBot(BotClass cls) : this((BotRole)(int)cls) { }

        [Constructable]
        public PlayerBot(BotRole role)
            : base(PlayerBotRoles.AIForRole(role),
                   PlayerBotRoles.FightModeForRole(role),
                   10, 1, 0.2, 0.4)
        {
            this.Role = role;
            this.Tier = BotTierUtil.RollTier();
            this.RangePerception = IsPK ? 16 : 10;

            bool female = !IsPK && Utility.RandomBool();
            this.Body = female ? 401 : 400;
            this.Female = female;

            if (IsPK)
                this.Name = PKNames[Utility.Random(PKNames.Length)] + " " +
                            PKEpithets[Utility.Random(PKEpithets.Length)];
            else
                this.Name = female
                    ? FemaleNames[Utility.Random(FemaleNames.Length)]
                    : MaleNames[Utility.Random(MaleNames.Length)];

            this.Hue = Utility.RandomSkinHue();
            this.HairItemID = Utility.RandomList(
                0x203B,0x203C,0x203D,0x2044,0x2045,0x2047,0x2049,0x204A);
            this.HairHue = IsPK ? 0x455 : Utility.RandomHairHue();
            if (!female)
            {
                this.FacialHairItemID = Utility.RandomList(0,0x203E,0x203F,0x2040,0x2041);
                this.FacialHairHue = this.HairHue;
            }

            // Stats scale with tier — see BotTierUtil. Civilians and macroers
            // still get adjusted by their role configs after this baseline.
            var (sMin, sMax) = BotTierUtil.StrRangeFor(this.Tier);
            SetStr(sMin, sMax);
            SetDex(Math.Max(40, sMin - 10), Math.Max(60, sMax - 10));
            SetInt(40, 80);
            SetHits(40, 80); SetDamage(8, 14);
            SetDamageType(ResistanceType.Physical, 100);

            PlayerBotRoles.Configure(this, role);

            this.Fame = (IsCombatRole || IsPK) ? 1000 : 200;
            this.Karma = IsPK ? -5000 : Utility.RandomMinMax(-500, 1500);
            this.VirtualArmor = (IsCombatRole || IsPK) ? 10 : 2;

            if (IsPK)
            {
                this.Kills = 6;
                this.AlwaysMurderer = true;
                this.PKBrain = new PKBehavior(this);
            }
            else if (IsMacroer)
            {
                this.MacroerBrain = new MacroerBehavior(this);
            }
        }

        public override bool ClickTitle => false;
        public override bool ShowFameTitle => false;
        public override bool CanRummageCorpses => IsCombatRole || IsPK;
        public override bool AlwaysAttackable => IsPK;

        public override void OnThink()
        {
            base.OnThink();

            bool playerNearby = false;
            foreach (var m in this.GetMobilesInRange(32))
            {
                if (m is PlayerMobile pm && !pm.IsStaff() && pm.Alive)
                {
                    playerNearby = true;
                    break;
                }
            }

            // PKs don't sleep — they hunt regardless. Wandering wilderness
            // PKs also need to update waypoints on their own schedule.
            if (!playerNearby && !IsPK)
            {
                this.ActiveSpeed = 0.6;
                this.PassiveSpeed = 1.0;
                return;
            }

            this.ActiveSpeed = IsPK ? 0.15 : 0.2;
            this.PassiveSpeed = IsPK ? 0.3 : 0.4;

            if (IsPK) { PKBrain?.Tick(); return; }
            if (IsMacroer) { MacroerBrain?.Tick(); return; }

            if (DateTime.UtcNow < m_NextActivity) return;

            if (IsCombatRole)
            {
                if (Combatant == null && Utility.RandomDouble() < 0.0003)
                {
                    Say(AdventurerChatter[Utility.Random(AdventurerChatter.Length)]);
                    m_NextActivity = DateTime.UtcNow.AddSeconds(20);
                }
                return;
            }

            if (IsCivilianRole && Combatant == null && Utility.RandomDouble() < 0.02)
            {
                PlayerBotRoles.DoCivilianActivity(this);
                m_NextActivity = DateTime.UtcNow.AddSeconds(Utility.RandomMinMax(15, 40));
            }
        }

        public override void OnSpeech(SpeechEventArgs e)
        {
            base.OnSpeech(e);
            if (!IsCombatRole) return;
            if (!(e.Mobile is PlayerMobile pm)) return;
            if (!pm.InRange(this, 4)) return;
            if (Combatant != null) return;

            string s = e.Speech?.ToLowerInvariant() ?? "";
            string myName = Name.ToLowerInvariant();

            if (s.Contains("follow") && s.Contains(myName))
            {
                Say("Aye, I shall follow ye.");
                ControlMaster = pm; Controlled = true;
                ControlOrder = OrderType.Follow; ControlTarget = pm;
            }
            else if (s.Contains("stop") && Controlled && ControlMaster == pm)
            {
                Say("As ye wish."); ControlOrder = OrderType.Stop;
            }
            else if (s.Contains("dismiss") && Controlled && ControlMaster == pm)
            {
                Say("Fare thee well.");
                Controlled = false; ControlMaster = null;
                ControlOrder = OrderType.None;
            }
        }

        public void Taunt()
        {
            if (Utility.RandomBool())
                Say(PKTaunts[Utility.Random(PKTaunts.Length)]);
        }

        public PlayerBot(Serial serial) : base(serial) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(3);                  // version
            writer.Write((int)Role);
            writer.Write((int)Tier);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            switch (version)
            {
                case 3:
                    Role = (BotRole)reader.ReadInt();
                    Tier = (BotTier)reader.ReadInt();
                    break;
                case 2:
                case 1:
                case 0:
                    Role = (BotRole)reader.ReadInt();
                    Tier = BotTier.Adept;   // mid-tier default for old saves
                    break;
            }
            if (IsPK) PKBrain = new PKBehavior(this);
            else if (IsMacroer) MacroerBrain = new MacroerBehavior(this);
        }
    }
}
