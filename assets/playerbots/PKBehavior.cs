// PKBehavior.cs — drives BotRole.PK PlayerBots.
//
// PKs have two spawn flavors set by the spawner:
//   - "Camper": parked at a dungeon entrance with a small RangeHome.
//   - "Wanderer": roams the wilderness, picking new waypoints every few
//                  minutes so they don't orbit one spot.
//
// Gang members share a PKGang; when any sees a target, the others assist.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Server.Mobiles
{
    public enum PKTargetMode { PlayersOnly, BotsOnly, Both }

    public class PKGang
    {
        private static int s_NextId = 1;
        public int Id { get; }
        public List<PlayerBot> Members { get; } = new List<PlayerBot>();

        public PKGang() { Id = s_NextId++; }

        public void Add(PlayerBot pk)
        {
            if (!Members.Contains(pk)) Members.Add(pk);
        }

        public void Engage(Mobile target)
        {
            foreach (var m in Members.ToList())
            {
                if (m == null || m.Deleted || !m.Alive) { Members.Remove(m); continue; }
                if (m.Combatant == null && m.GetDistanceToSqrt(target) < 24)
                {
                    m.Combatant = target;
                    m.Warmode = true;
                }
            }
        }
    }

    public class PKBehavior
    {
        public static PKTargetMode TargetMode = PKTargetMode.Both;

        private readonly PlayerBot m_Owner;
        public PKGang Gang;
        public bool Wandering;     // set by spawner — true = roaming PK
        private DateTime m_NextScan;
        private DateTime m_NextTaunt;
        private DateTime m_NextWaypoint;
        private bool m_Fleeing;

        public PKBehavior(PlayerBot owner)
        {
            m_Owner = owner;
            m_NextWaypoint = DateTime.UtcNow.AddMinutes(Utility.RandomMinMax(2, 5));
        }

        public void Tick()
        {
            // Low HP → flee
            if (m_Owner.Hits < m_Owner.HitsMax * 0.3 && !m_Fleeing)
            {
                m_Fleeing = true;
                m_Owner.Say("*tries to escape*");
                m_Owner.Combatant = null;
                m_Owner.Warmode = false;
                m_Owner.Home = new Point3D(
                    m_Owner.X + Utility.RandomMinMax(-20, 20),
                    m_Owner.Y + Utility.RandomMinMax(-20, 20),
                    m_Owner.Z);
                m_Owner.RangeHome = 25;
                return;
            }
            if (m_Fleeing && m_Owner.Hits > m_Owner.HitsMax * 0.7)
                m_Fleeing = false;
            if (m_Fleeing) return;

            // Wandering: occasionally pick a new home waypoint so they roam.
            if (Wandering && DateTime.UtcNow >= m_NextWaypoint && m_Owner.Combatant == null)
            {
                MoveWaypoint();
                m_NextWaypoint = DateTime.UtcNow.AddMinutes(Utility.RandomMinMax(2, 6));
            }

            if (m_Owner.Combatant != null) { MaybeTaunt(); return; }
            if (DateTime.UtcNow < m_NextScan) return;
            m_NextScan = DateTime.UtcNow.AddSeconds(2);

            Mobile target = FindTarget();
            if (target == null) return;

            m_Owner.Combatant = target;
            m_Owner.Warmode = true;
            Gang?.Engage(target);
            MaybeTaunt();
        }

        private void MoveWaypoint()
        {
            // Pick a new wandering target ~30-80 tiles from current location.
            int dx = Utility.RandomMinMax(-80, 80);
            int dy = Utility.RandomMinMax(-80, 80);
            // Bias slightly toward continued direction by allowing big jumps.
            var next = new Point3D(
                Math.Max(1, m_Owner.X + dx),
                Math.Max(1, m_Owner.Y + dy),
                m_Owner.Z);
            m_Owner.Home = next;
            m_Owner.RangeHome = 30;
        }

        private Mobile FindTarget()
        {
            Mobile best = null;
            double bestD = double.MaxValue;

            foreach (var m in m_Owner.GetMobilesInRange(m_Owner.RangePerception))
            {
                if (m == m_Owner || m.Deleted || !m.Alive || m.IsStaff() || m.Hidden) continue;
                if (m is PlayerBot pk && pk.IsPK) continue;

                bool isPlayer = m is PlayerMobile pm && !pm.IsStaff();
                bool isOtherBot = m is PlayerBot otherBot && !otherBot.IsPK;

                bool valid = false;
                switch (TargetMode)
                {
                    case PKTargetMode.PlayersOnly: valid = isPlayer; break;
                    case PKTargetMode.BotsOnly:    valid = isOtherBot; break;
                    case PKTargetMode.Both:        valid = isPlayer || isOtherBot; break;
                }
                if (!valid) continue;

                // Skip if inside an active guard zone.
                if (m_Owner.Region is Server.Regions.GuardedRegion gr && !gr.IsDisabled())
                    continue;

                double d = m_Owner.GetDistanceToSqrt(m);
                if (d < bestD) { bestD = d; best = m; }
            }

            return best;
        }

        private void MaybeTaunt()
        {
            if (DateTime.UtcNow < m_NextTaunt) return;
            if (Utility.RandomDouble() < 0.3)
            {
                m_Owner.Taunt();
                m_NextTaunt = DateTime.UtcNow.AddSeconds(15);
            }
        }
    }
}
