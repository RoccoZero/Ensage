﻿using System;
using System.Linq;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;
using SharpDX;

namespace SpacebarToFarm.Interfaces
{
    class FarmUnitRanged : FarmUnit
    {
        private const float DamageMultiplier = 1.45f;

        public FarmUnitRanged(Unit controlledUnit) : base(controlledUnit)
        {
        }

        protected override float GetItemBonusDamage(Unit target)
        {
            float damageBonus = 0;
            // mana burn
            if (target.Mana > 0)
            {
                var diffusal = ControlledUnit.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_Diffusal_Blade || x.ClassID == ClassID.CDOTA_Item_Diffusal_Blade_Level2);
                if (diffusal != null)
                {
                    if (ControlledUnit.IsIllusion)
                    {
                        var abilitySpecialData = diffusal.AbilitySpecialData.FirstOrDefault(x => x.Name == "feedback_mana_burn_illusion_ranged");
                        if ( abilitySpecialData != null)
                            damageBonus += Math.Min(abilitySpecialData.Value, target.Mana);
                    }
                    else
                    {
                        var firstOrDefault = diffusal.AbilitySpecialData.FirstOrDefault(x => x.Name == "feedback_mana_burn");
                        if (firstOrDefault != null)
                            damageBonus += Math.Min(firstOrDefault.Value, target.Mana);
                    }
                }
            }
            return damageBonus;
        }

        protected override float GetBaseDamage(Unit target)
        {
            float baseDamage = ((float)ControlledUnit.MinimumDamage + ControlledUnit.MaximumDamage)/2;
            // base damage amplification
            if (target.Team != ControlledUnit.Team)
            {
                var quellingBlade =
              ControlledUnit.Inventory.Items.FirstOrDefault(
                  x => x.ClassID == ClassID.CDOTA_Item_QuellingBlade || x.ClassID == ClassID.CDOTA_Item_Iron_Talon);

                if (quellingBlade != null)
                {
                    // 140
                    var abilitySpecialData = quellingBlade.AbilitySpecialData.FirstOrDefault(x => x.Name == "damage_bonus_ranged");
                    if (abilitySpecialData != null)
                        baseDamage *= (abilitySpecialData.Value / 100.0f);
                }

                var battleFury = ControlledUnit.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_Battlefury);
                if (battleFury != null)
                {
                    // 160
                    var abilitySpecialData = battleFury.AbilitySpecialData.FirstOrDefault(x => x.Name == "quelling_bonus_ranged");
                    if (abilitySpecialData != null)
                        baseDamage *= (abilitySpecialData.Value / 100.0f);
                }
            }
            return baseDamage;
        }

        protected override float GetTimeTilAttack(Unit target)
        {
            float distance = target.Distance2D(ControlledUnit);

            float projectileTime = Math.Min(AttackRange, AttackRange - distance) / (float)ControlledUnit.ProjectileSpeed();  
            return ((Math.Max(0, distance - AttackRange)) / ControlledUnit.MovementSpeed) + (float)(ControlledUnit.AttackPoint()*10) + (float)ControlledUnit.GetTurnTime(target) + projectileTime;
        }

        public override void LastHit()
        {
            if (LastTarget != null && !IsLastTargetValid)
            {
                if (FarmMenu.IsAutoStopEnabled)
                    ControlledUnit.Stop();
                LastTarget = null;
            }

            if (!Utils.SleepCheck($"lasthit_{ControlledUnit.Handle}"))
                return;

            if (FarmMenu.IsLasthittingActive)
            {
                var couldKill = InfoCentral.EnemyCreeps.AsParallel()
                    .Where(x => x.Distance2D(ControlledUnit) < (AttackRange + FarmMenu.RangedBonusRange))
                    .Where(x => GetPseudoHealth(x) <= (GetAttackDamage(x)* DamageMultiplier))
                    .OrderBy(x => x.Distance2D(ControlledUnit))
                    .FirstOrDefault();


                if (couldKill == null)
                    return;

                LastTarget = couldKill;

                if (ControlledUnit.IsAttacking() && GetPseudoHealth(couldKill) > GetAttackDamage(couldKill))
                {
                    ControlledUnit.Stop();
                    Utils.Sleep((ControlledUnit.AttackPoint()*500), $"lasthit_{ControlledUnit.Handle}");
                    return;
                }

                ControlledUnit.Attack(couldKill);
                Utils.Sleep((ControlledUnit.AttackPoint()*500), $"lasthit_{ControlledUnit.Handle}");
                return;
            }

            if (!FarmMenu.IsDenyModeActive)
                return;

            var couldDeny = InfoCentral.AlliedCreeps.AsParallel().Where(x => x.Distance2D(ControlledUnit) < (AttackRange + FarmMenu.RangedBonusRange)
                                                                && GetPseudoHealth(x) <= (GetAttackDamage(x) * DamageMultiplier))
                .OrderBy(x => x.Distance2D(ControlledUnit))
                .FirstOrDefault();
            if (couldDeny != null)
            {
                if (ControlledUnit.IsAttacking() && GetPseudoHealth(couldDeny) > GetAttackDamage(couldDeny))
                {
                    ControlledUnit.Stop();
                    Utils.Sleep((ControlledUnit.AttackPoint() * 500), $"lasthit_{ControlledUnit.Handle}");
                    return;
                }

                ControlledUnit.Attack(couldDeny);
                Utils.Sleep((ControlledUnit.AttackPoint() * 500), $"lasthit_{ControlledUnit.Handle}");
                return;
            }
        }

        public override void LaneClear()
        {
            throw new NotImplementedException();
        }

        public override void Harras()
        {
            throw new NotImplementedException();
        }

        protected virtual float AttackRange => ControlledUnit.AttackRange;

        public override void AddRangeEffect()
        {
            if (RangeEffect != null || !FarmMenu.ShouldDrawLasthitRange)
                return;

            RangeEffect = ControlledUnit.AddParticleEffect("particles/ui_mouseactions/drag_selected_ring.vpcf");
            RangeEffect.SetControlPoint(1, new Vector3(FarmMenu.RedColor, FarmMenu.GreenColor, FarmMenu.BlueColor)); // R G B
            RangeEffect.SetControlPoint(2, new Vector3(AttackRange+FarmMenu.RangedBonusRange, 255, 0));
        }
    }
}
