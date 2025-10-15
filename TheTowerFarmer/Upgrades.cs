using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheTowerFarmer
{
    internal enum Upgrades
    {
        Unknown,

        // Attack Upgrades
        Damage,
        AttackSpeed,
        CriticalChance,
        CriticalFactor,
        Range,
        DamageMeter,
        MultishotChange,
        MultishotTargets,
        RapidFireChance,
        RapidFireDuration,
        BounceShotChance,
        BounceShotTargets,
        BounceShotRange,

        // Defense Upgrades
        Health,
        HealthRegen,
        Defense,
        DefenseAbsolute,
        ThornDamage,
        Lifesteal,
        KnockbackChance,
        KnockbackForce,
        OrbSpeed,
        Orbs,
        ShockwaveSize,
        ShockwaveFrequency,

        // Utility Upgrades
        CashBonus,
        CashWave,
        CoinsKillBonus,
        CoinsWave,
        FreeAttackUpgrade,
        FreeDefenseUpgrade,
        FreeUtilityUpgrade,
        InterestWave
    }
}
