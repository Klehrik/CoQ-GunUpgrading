using System;
using System.Linq;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;
using XRL.UI;

namespace XRL.World.Parts
{
    [Serializable]
    public class Klehrik_GunUpgrade : IPart
    {
        public int Level = 0;
        public int Bonus = 0;   // Unused now

        private int basePenetration = -1;
        private int baseHighestBit = -1;

        // These do not gain the extra bonus <=3 bit guns get
        private string[] extraBonusBanlist = { "Desert Rifle", "Ruin of House Isner" };

        // These DO gain the extra bonus <=3 bit guns get
        private string[] extraBonusAllowlist = { "Combat Shotgun" };

        public override bool SameAs(IPart p)
        {
            return Level == (p as Klehrik_GunUpgrade).Level;
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == GetInventoryActionsEvent.ID
                || ID == InventoryActionEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == GetMissileWeaponPerformanceEvent.ID;
        }

        public override bool HandleEvent(GetInventoryActionsEvent E)
        {
            if (CanUpgrade(E))
            {
                E.AddAction("Upgrade", "upgrade", "Klehrik_GunUpgrade", null, 'u');
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(InventoryActionEvent E)
        {
            if (E.Command == "Klehrik_GunUpgrade")
            {
                Upgrade(E);
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (Level > 0 && E.Understood())
            {
                E.AddEpithet("{{C|+" + Level + "}}");
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetMissileWeaponPerformanceEvent E)
        {
            if (Level > 0 && E.Subject == ParentObject)
            {
                E.PenetrationBonus += GetBonusPenetration(Level);

                var bonusDamage = GetBonusDamage(Level);
                if (bonusDamage > 0)
                {
                    var currentModifier = 0;
                    var dmg = E.BaseDamage.Split("+");
                    if (dmg.Length > 1)
                    {
                        if (int.TryParse(dmg[1], out int value))
                        {
                            currentModifier = value;
                        }
                    }
                    var newDmg = dmg[0] + "+" + (currentModifier + bonusDamage).ToString();
                    E.BaseDamage = newDmg;
                }
            }
            return base.HandleEvent(E);
        }

        private bool CanUpgrade(GetInventoryActionsEvent E)
        {
            if (!E.Actor.HasPart<Skill.Tinkering_Tinker1>())
            {
                return false;
            }
            if (!ParentObject.HasPart<TinkerItem>())
            {
                return false;
            }
            var part = ParentObject.GetPart<TinkerItem>();
            if (!part.CanBuild && ParentObject.Blueprint != "Ruin of House Isner")
            {
                return false;
            }
            if (GetBasePenetration() <= 0)
            {
                return false;
            }
            if (Level >= GetMaxLevel())
            {
                return false;
            }
            return true;
        }

        private void Upgrade(InventoryActionEvent E)
        {
            var actor = E.Actor;
            if (actor.AreHostilesNearby() && actor.FireEvent("CombatPreventsTinkering"))
            {
                actor.Fail("You can't upgrade with hostiles nearby!");
                return;
            }

            var basePen = GetBasePenetration();
            var penValue = basePen + 4 + GetBonusPenetration(Level);
            var penNextValue = basePen + 4 + GetBonusPenetration(Level + 1);

            var dmgValue = GetBonusDamage(Level);
            var dmgNextValue = GetBonusDamage(Level + 1);

            var bitReq = GetRequiredBit();
            var bitReqValue = bitReq.GetHighestTier();
            if (bitReqValue <= 0)
            {
                actor.Fail("Error: Failed to retrieve next required bit.");
                return;
            }
            var tinkerReq = GetRequiredTinker(bitReqValue);

            var hasBit = false;
            var hasTinker = false;

            var bitLocker = (actor.GetPart<Skill.Tinkering>() == null) ? actor.GetPart<BitLocker>() : actor.RequirePart<BitLocker>();
            if (bitLocker != null && bitLocker.HasBits(bitReq))
			{
				hasBit = true;
			}
            if (tinkerReq == 1
             || tinkerReq == 2 && actor.HasPart<Skill.Tinkering_Tinker2>()
             || tinkerReq == 3 && actor.HasPart<Skill.Tinkering_Tinker3>())
            {
                hasTinker = true;
            }

            var currentLevel = (Level > 0) ? ("+" + Level) : "--";
            var newLevel     = "+" + (Level + 1) + (Level + 1 >= GetMaxLevel() ? " (max)" : "");
            var currentPen   = penValue.ToString().PadLeft(2);
            var newPen       = penNextValue.ToString().PadLeft(2);
            var currentDmg   = (dmgValue > 0) ? ("+" + dmgValue.ToString()) : "--";
            var newDmg       = (dmgNextValue > 0) ? ("+" + dmgNextValue.ToString()) : "--";
            var bit          = bitReq.ToString();
            var bitStatus    = hasBit ? "{{G|✓}}" : "{{R|X}}";
            var tinker       = string.Concat(Enumerable.Repeat("I", tinkerReq)).PadRight(3);
            var tinkerStatus = hasTinker ? "{{G|✓}}" : "{{R|X}}";
            var prompt       = (hasBit && hasTinker) ? "Proceed?" : "{{R|Requirements not met.}}";

            var text = "{{w|Upgrade}}\nLevel    " + currentLevel + " → {{W|" + newLevel + "}}\n{{c|→}}        " + currentPen + " → {{W|" + newPen + "}}\n{{r|♥}}        " + currentDmg + " → {{W|" + newDmg + "}}\n\n{{w|Required}}\nBit      " + bit + "    " + bitStatus + "\nTinker   {{C|" + tinker + "}}  " + tinkerStatus + "\n\n" + prompt;

            if (!hasBit || !hasTinker)
            {
                Popup.Show(text, LogMessage: false);
                return;
            }

            var result = Popup.ShowYesNo(text);
            if (result != DialogResult.Yes)
            {
                return;
            }

            ParentObject.SplitStack(1, actor);
            bitLocker.UseBits(bitReq);
            Level += 1;

            if (ParentObject.HasPart<Commerce>())
            {
                ParentObject.GetPart<Commerce>().Value += bitReqValue * 75;
            }
            if (ParentObject.HasPart<Examiner>())
            {
                var part = ParentObject.GetPart<Examiner>();
                part.Complexity += 1;
                if (bitReqValue >= 5)
                {
                    part.Difficulty += 1;
                }
            }

            SoundManager.PlayUISound("Sounds/Abilities/sfx_ability_tinkerModItem");
            var text2 = ParentObject.t(int.MaxValue, null, null, AsIfKnown: false, Single: false, NoConfusion: false, NoColor: false, Stripped: true);
            Popup.Show("You upgrade " + text2 + " to be more effective.");
        }

        private int GetBasePenetration()
        {
            if (basePenetration >= 0)
            {
                return basePenetration;
            }
            string proj = "";
            if (ParentObject.HasPart<MagazineAmmoLoader>())
            {
                var part = ParentObject.GetPart<MagazineAmmoLoader>();
                proj = part.ProjectileObject;
            }
            else if (ParentObject.HasPart<EnergyAmmoLoader>())
            {
                var part = ParentObject.GetPart<EnergyAmmoLoader>();
                proj = part.ProjectileObject;
            }
            if (proj.IsNullOrEmpty())
            {
                return -1;
            }
            var obj = GameObject.Create(proj);
            basePenetration = obj.GetPart<Projectile>().BasePenetration;
            obj.Obliterate();
            return basePenetration;
        }

        private int GetBonusPenetration(int level)
        {
            // Bit <=3 guns get an additional +1 for their last upgrade (with exceptions)
            var bonus = level;
            if (level >= GetMaxLevel() && IsPermittedForExtraBonus())
            {
                bonus += 1;
            }
            return bonus;
        }

        private int GetBonusDamage(int level)
        {
            // Bit <=5 guns get +1 for their last upgrade
            // Bit <=3 guns get an additional +1 for their second last upgrade (with exceptions)
            var bonus = 0;
            if (level >= GetMaxLevel() - 1 && IsPermittedForExtraBonus())
            {
                bonus += 1;
            }
            if (level >= GetMaxLevel() && GetBaseHighestBit() <= 5)
            {
                bonus += 1;
            }
            return bonus;
        }

        private bool IsPermittedForExtraBonus()
        {
            return (GetBaseHighestBit() <= 3 && !extraBonusBanlist.Contains(ParentObject.Blueprint))
                 || extraBonusAllowlist.Contains(ParentObject.Blueprint);
        }

        private int GetBaseHighestBit()
        {
            if (baseHighestBit >= 0)
            {
                return baseHighestBit;
            }
            var part = ParentObject.GetPart<TinkerItem>();
            var bitCost = new BitCost(part.Bits);
            baseHighestBit = bitCost.GetHighestTier();
            return baseHighestBit;
        }

        private BitCost GetRequiredBit()
        {
            var required = baseHighestBit + 1 + Level;
            return new BitCost(BitType.ReverseTranslateBit(required.ToString()[0]));
        }

        private int GetRequiredTinker(int bit)
        {
            if (bit <= 3) return 1;
            if (bit <= 6) return 2;
            return 3;
        }

        private int GetMaxLevel()
        {
            return 7 - GetBaseHighestBit();
        }
    }
}