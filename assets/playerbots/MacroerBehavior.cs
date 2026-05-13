// MacroerBehavior.cs — drives BotRole.Macroer bots through one of several
// AFK skill-training loops. Macroers don't move, don't respond to speech,
// and run their chosen routine on a robotically regular cadence.

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Spells.First;

namespace Server.Mobiles
{
    public enum MacroRoutine
    {
        Hiding,         // toggle Hidden on/off, with the hide visual
        Magery,         // self-cast Night Sight with the visual
        Healing,        // bandage animation, self-damage to justify it
        Meditation,     // focus sound, mana regen
        Anatomy,        // looking-over-self animation
        Musicianship,   // play a random instrument note on a cadence
        Taming          // periodically "tame" a wild creature (visual)
    }

    public class MacroerBehavior
    {
        private readonly PlayerBot m_Owner;
        public MacroRoutine Routine { get; }
        private DateTime m_NextStep;
        private int m_StepCount;

        // Per-musicianship-macroer: which instrument they're "training" on.
        // Picked at construction so the same bot keeps playing the same one.
        private readonly int m_InstrumentSound;

        private static readonly string[] AfkChatter = {
            "test","asdf","...","afk","back","brb","*","??","lol","ok"
        };

        // Lute, harp, tambourine, drums — UO sound IDs.
        private static readonly int[] InstrumentSounds = { 0x4C, 0x4D, 0x506, 0x52 };

        public MacroerBehavior(PlayerBot owner)
        {
            m_Owner = owner;
            Routine = (MacroRoutine)Utility.Random(
                Enum.GetValues(typeof(MacroRoutine)).Length);
            m_InstrumentSound = InstrumentSounds[Utility.Random(InstrumentSounds.Length)];
            m_NextStep = DateTime.UtcNow.AddSeconds(Utility.RandomMinMax(2, 12));
        }

        public void Tick()
        {
            // Macroers are mostly stationary.
            m_Owner.ActiveSpeed = 1.0;
            m_Owner.PassiveSpeed = 1.5;
            if (m_Owner.Combatant != null) return;
            if (DateTime.UtcNow < m_NextStep) return;

            try
            {
                switch (Routine)
                {
                    case MacroRoutine.Hiding:       StepHiding();       break;
                    case MacroRoutine.Magery:       StepMagery();       break;
                    case MacroRoutine.Healing:      StepHealing();      break;
                    case MacroRoutine.Meditation:   StepMeditation();   break;
                    case MacroRoutine.Anatomy:      StepAnatomy();      break;
                    case MacroRoutine.Musicianship: StepMusicianship(); break;
                    case MacroRoutine.Taming:       StepTaming();       break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MacroerBehavior] {0} step failed: {1}",
                    Routine, ex.Message);
            }

            m_StepCount++;

            if (m_StepCount > 0 && m_StepCount % Utility.RandomMinMax(30, 50) == 0)
                m_Owner.Say(AfkChatter[Utility.Random(AfkChatter.Length)]);

            m_NextStep = DateTime.UtcNow.AddSeconds(CadenceSeconds());
        }

        private double CadenceSeconds()
        {
            switch (Routine)
            {
                case MacroRoutine.Hiding:       return 11;
                case MacroRoutine.Magery:       return 5;
                case MacroRoutine.Healing:      return 8;
                case MacroRoutine.Meditation:   return 10;
                case MacroRoutine.Anatomy:      return 4;
                case MacroRoutine.Musicianship: return Utility.RandomMinMax(8, 18);
                case MacroRoutine.Taming:       return Utility.RandomMinMax(6, 12);
                default:                        return 8;
            }
        }

        // ---------- Routines ----------

        private void StepHiding()
        {
            if (m_Owner.Hidden)
            {
                m_Owner.Hidden = false;
                Effects.SendLocationEffect(m_Owner.Location, m_Owner.Map, 0x3735, 6, 15);
                Effects.PlaySound(m_Owner.Location, m_Owner.Map, 0x1FA);
            }
            else
            {
                m_Owner.Hidden = true;
                Effects.SendLocationEffect(m_Owner.Location, m_Owner.Map, 0x3779, 10, 20);
                m_Owner.PlaySound(0x208);
            }
        }

        private void StepMagery()
        {
            try
            {
                var spell = new NightSightSpell(m_Owner, null);
                spell.Cast();
                Effects.SendTargetParticles(m_Owner, 0x375A, 1, 17, 1153, 7, 9919, (EffectLayer)255, 0);
                m_Owner.PlaySound(0x1E3);
            }
            catch
            {
                Effects.SendTargetParticles(m_Owner, 0x375A, 1, 17, 1153, 7, 9919, (EffectLayer)255, 0);
                m_Owner.PlaySound(0x1E3);
            }
        }

        private void StepHealing()
        {
            m_Owner.Animate(33, 7, 1, true, false, 0);
            m_Owner.PlaySound(0x57);
            if (m_Owner.Hits > m_Owner.HitsMax / 2)
                m_Owner.Damage(2, m_Owner);
        }

        private void StepMeditation()
        {
            m_Owner.PlaySound(0xF9);
            if (m_Owner.Mana < m_Owner.ManaMax)
                m_Owner.Mana = Math.Min(m_Owner.Mana + 2, m_Owner.ManaMax);
        }

        private void StepAnatomy()
        {
            m_Owner.Animate(33, 5, 1, true, false, 0);
        }

        private void StepMusicianship()
        {
            // Play this macroer's chosen instrument. The slight delay before
            // the note makes it feel like a real player triggering a macro
            // rather than an automated metronome.
            Effects.PlaySound(m_Owner.Location, m_Owner.Map, m_InstrumentSound);
            // Tiny "playing" animation — a small bow gesture works for any
            // instrument and is the closest UO has to a musicianship anim.
            if (Utility.RandomBool())
                m_Owner.Animate(33, 4, 1, true, false, 0);
        }

        private void StepTaming()
        {
            // Find a wild tameable nearby. If none, spawn one. Then play the
            // "attempt to tame" visual — the bot says the iconic command
            // and a target effect plays on the creature.
            BaseCreature wild = FindNearbyTameable();
            if (wild == null)
            {
                wild = SpawnTameableNear();
            }
            if (wild == null) return;

            // The classic T2A taming script issued the verbal command —
            // either the creature's name or its species. We pick a fixed
            // phrase for visual consistency.
            switch (Utility.Random(3))
            {
                case 0: m_Owner.Say("All Follow Me"); break;
                case 1: m_Owner.Say(wild.Name ?? "*"); break;
                case 2: m_Owner.Say("Stop"); break;
            }
            // Target sparkles on the creature
            Effects.SendTargetParticles(wild, 0x375A, 1, 17, 1153, 7, 9919, (EffectLayer)255, 0);
            // Use the bot's "use skill" animation
            m_Owner.Animate(33, 4, 1, true, false, 0);
            m_Owner.PlaySound(0x55F);  // skill use sound
        }

        private BaseCreature FindNearbyTameable()
        {
            foreach (var m in m_Owner.GetMobilesInRange(6))
            {
                if (m is BaseCreature bc && !bc.Controlled && bc.Tamable && bc.Alive)
                    return bc;
            }
            return null;
        }

        private BaseCreature SpawnTameableNear()
        {
            // Don't keep spawning — limit ourselves to one creature at a time
            // nearby. If anything is in range, skip.
            foreach (var m in m_Owner.GetMobilesInRange(8))
                if (m is BaseCreature bc && bc.Tamable) return bc;

            BaseCreature wild;
            switch (Utility.Random(6))
            {
                case 0: wild = new Horse();    break;
                case 1: wild = new Llama();    break;
                case 2: wild = new Pig();      break;
                case 3: wild = new Sheep();    break;
                case 4: wild = new Rabbit();   break;
                default: wild = new Chicken(); break;
            }
            try
            {
                var loc = new Point3D(
                    m_Owner.X + Utility.RandomMinMax(-2, 2),
                    m_Owner.Y + Utility.RandomMinMax(-2, 2),
                    m_Owner.Z);
                wild.MoveToWorld(loc, m_Owner.Map);
            }
            catch
            {
                wild.Delete();
                return null;
            }
            return wild;
        }
    }
}
