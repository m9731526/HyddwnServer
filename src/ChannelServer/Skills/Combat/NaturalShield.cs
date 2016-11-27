﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Skills.Base;
using Aura.Channel.World.Entities;
using Aura.Mabi;
using Aura.Mabi.Const;
using Aura.Shared.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Channel.Skills.Combat
{
	/// <summary>
	/// Skill handler for active Natural Shield. Also handles the effect of
	/// all Natural Shields.
	/// </summary>
	/// <remarks>
	/// Var1: Damage Reduction (%)
	/// Var2: Delay Reduction (%)
	/// Var3: Ping?
	/// 
	/// Reference: http://wiki.mabinogiworld.com/view/Natural_Shield_(Monster)
	/// 
	/// Use `Has(SkillFlags.InUse)` to check if skill is active.
	/// </remarks>
	[Skill(SkillId.NaturalShield)]
	public class NaturalShield : StartStopSkillHandler
	{
		private const SkillRank DefaultMsgRank = SkillRank.RF;
		private const float DefaultDamageReduction = 50;
		private const float DefaultDelayReduction = 50;

		private static readonly SkillId[] Skills = { SkillId.NaturalShield, SkillId.NaturalShieldPassive, SkillId.PaladinNaturalShield, SkillId.DarkNaturalShield, SkillId.ConnousNaturalShield, SkillId.PhysisNaturalShield };

		private static string[] Lv1Msgs =
		{
			Localization.Get("My attack is being defended by a skill!"),
			Localization.Get("My attack is very ineffective right now..."),
			Localization.Get("That didn't feel right..."),
			Localization.Get("My attack is a bit off the mark...")
		};

		private static string[] Lv2Msgs =
		{
			Localization.Get("This is not enough to stop the target..."),
			Localization.Get("Can't shake the target's balance!"),
			Localization.Get("That may have inflicted some damage, but the target still has its guard up.")
		};

		private static string[] Lv3Msgs =
		{
			Localization.Get("The target takes no damage at all!"),
			Localization.Get("This attack is completely useless!")
		};

		/// <summary>
		/// Starts Natural Shield.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="dict"></param>
		/// <returns></returns>
		public override StartStopResult Start(Creature creature, Skill skill, MabiDictionary dict)
		{
			// Give an indication of activation, official behavior unknown.
			Send.Notice(creature, Localization.Get("Natural Shield activated."));

			return StartStopResult.Okay;
		}

		/// <summary>
		/// Stops Natural Shield.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="skill"></param>
		/// <param name="dict"></param>
		/// <returns></returns>
		public override StartStopResult Stop(Creature creature, Skill skill, MabiDictionary dict)
		{
			Send.Notice(creature, Localization.Get("Natural Shield deactivated."));

			return StartStopResult.Okay;
		}

		/// <summary>
		/// Handles Natural Shield bonuses and auto-defense, reducing damage
		/// and setting the appropriate options on tAction. Returns the
		/// delay reduction and whether a ping occured.
		/// </summary>
		/// <remarks>
		/// All active and passive Natural Shields are checked in sequence,
		/// followed by the equipment. The first one found is the one that
		/// is used for the damage and delay reduction. It's unknown whether
		/// this is official behavior, but stacking them would be overkill.
		/// </remarks>
		/// <param name="attacker"></param>
		/// <param name="target"></param>
		/// <param name="damage"></param>
		/// <param name="tAction"></param>
		public static PassiveDefenseResult Handle(Creature attacker, Creature target, ref float damage, TargetAction tAction)
		{
			var pinged = false;
			var used = false;
			var damageReduction = 0f;
			var delayReduction = 0f;
			var rank = DefaultMsgRank;
			var rnd = RandomProvider.Get();

			// Check skills
			for (int i = 0; i < Skills.Length; ++i)
			{
				// Check if skill exists and it's either in use or passive
				var skill = target.Skills.Get(Skills[i]);
				if (skill != null && (skill.Info.Id == SkillId.NaturalShieldPassive || skill.Has(SkillFlags.InUse)))
				{
					damageReduction = skill.RankData.Var1;
					delayReduction = skill.RankData.Var2;
					pinged = (skill.RankData.Var3 == 1);
					rank = skill.Info.Rank;
					used = true;
					break;
				}
			}

			// Check equipment
			if (!used)
			{
				var equipment = target.Inventory.GetMainEquipment();
				foreach (var item in equipment)
				{
					var chance = item.Data.AutoDefenseRanged;

					// Add upgrades
					chance += item.MetaData1.GetFloat("IM_RNG") * 100;

					if (chance > 0)
					{
						if (used = pinged = (rnd.Next(100) < chance))
						{
							damageReduction = DefaultDamageReduction;
							delayReduction = DefaultDelayReduction;
							break;
						}
					}
				}
			}

			// Notice and flag
			if (pinged)
			{
				tAction.EffectFlags |= EffectFlags.NaturalShield;

				var msg = "";
				if (rank >= SkillRank.Novice && rank <= SkillRank.RA)
					msg = rnd.Rnd(Lv1Msgs);
				else if (rank >= SkillRank.R9 && rank <= SkillRank.R2)
					msg = rnd.Rnd(Lv2Msgs);
				else if (rank == SkillRank.R1)
					msg = rnd.Rnd(Lv3Msgs);

				Send.Notice(attacker, msg);
			}

			// Apply damage reduction and return delay reduction
			if (damageReduction > 0)
				damage = Math.Max(1, damage - (damage / 100 * damageReduction));

			return new PassiveDefenseResult(pinged, delayReduction);
		}
	}

	public struct PassiveDefenseResult
	{
		public readonly bool Pinged;
		public readonly float DelayReduction;

		public PassiveDefenseResult(bool pinged, float delayReduction)
		{
			this.Pinged = pinged;
			this.DelayReduction = delayReduction;
		}
	}
}
