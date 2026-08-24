using System;
using System.Collections.Generic;
using HarmonyLib;
using static System.Reflection.Emit.OpCodes;

namespace GalacticScale
{
    public class DetermineCraftAstroIdTranspiler
    {
        // #258 / #247: craft only join a star's astro frame within a hardcoded
        // 40.5 AU (and leave past 45 AU). SensorLogic_Space gates all fleet
        // auto-targeting on sharing the star frame, so on GS2 systems whose
        // hives orbit beyond 40.5 AU, fleets can never self-acquire - they see
        // nothing while the Dark Fog freely discovers them. Scale both
        // thresholds to the star's real envelope (Utils.CraftAstroEnvelope,
        // floored at vanilla, enter/eject ratio preserved).
        //
        // UnitComponent.DetermineCraftAstroId and FleetComponent.DetermineCraftAstroId
        // are byte-identical (0.10.34.28529); one transpiler serves both.
        //
        // ENTER site: `dist < 1619999.957...` where dist (loc.1) was written by
        // SpaceSector.GetNearestStar and loc.0 holds that StarData - replace
        // the constant with `ldloc.0; call GetCraftAstroEnterMeters`.
        // EJECT site: `craft.pos.sqrMagnitude <= 3240000000000.0` runs in the
        // star-local frame; loc.0 is NOT initialized on this path, so the star
        // is recovered from craft.astroId / 100 via GalaxyData.StarById -
        // replace the constant with that lookup + GetCraftAstroEjectMetersSqr.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(UnitComponent), nameof(UnitComponent.DetermineCraftAstroId))]
        [HarmonyPatch(typeof(FleetComponent), nameof(FleetComponent.DetermineCraftAstroId))]
        public static IEnumerable<CodeInstruction> FixCraftFrameGate(IEnumerable<CodeInstruction> instructions, System.Reflection.MethodBase __originalMethod)
        {
            var enterHelper = AccessTools.Method(typeof(Utils), nameof(Utils.GetCraftAstroEnterMeters));
            var ejectHelper = AccessTools.Method(typeof(Utils), nameof(Utils.GetCraftAstroEjectMetersSqr));
            var starById = AccessTools.Method(typeof(GalaxyData), nameof(GalaxyData.StarById));
            var galaxyField = AccessTools.Field(typeof(SpaceSector), nameof(SpaceSector.galaxy));
            var astroIdField = AccessTools.Field(typeof(CraftData), nameof(CraftData.astroId));

            var matcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == Ldc_R8 && i.operand is double d && d == 1619999.9570846558));
            if (matcher.IsInvalid)
            {
                GS2.Error($"DetermineCraftAstroId transpiler: 40.5 AU enter constant not found in {__originalMethod?.DeclaringType?.Name} (game update changed it?). Craft star-frame gate stays vanilla.");
                return instructions;
            }
            // replace the enter constant: loc.0 already holds the nearest StarData
            matcher.SetInstruction(new CodeInstruction(Ldloc_0));
            matcher.Advance(1).InsertAndAdvance(new CodeInstruction(Call, enterHelper));

            matcher.Start().MatchForward(false,
                new CodeMatch(i => i.opcode == Ldc_R8 && i.operand is double d && d == 3240000000000.0));
            if (matcher.IsInvalid)
            {
                GS2.Error($"DetermineCraftAstroId transpiler: 45 AU eject constant not found in {__originalMethod?.DeclaringType?.Name} (game update changed it?). Craft star-frame gate stays vanilla.");
                return instructions;
            }
            // replace the eject constant: star = sector.galaxy.StarById(craft.astroId / 100)
            matcher.SetInstruction(new CodeInstruction(Ldarg_1));
            matcher.Advance(1)
                .InsertAndAdvance(new CodeInstruction(Ldfld, galaxyField))
                .InsertAndAdvance(new CodeInstruction(Ldarg_2))
                .InsertAndAdvance(new CodeInstruction(Ldfld, astroIdField))
                .InsertAndAdvance(new CodeInstruction(Ldc_I4_S, (sbyte)100))
                .InsertAndAdvance(new CodeInstruction(Div))
                .InsertAndAdvance(new CodeInstruction(Callvirt, starById))
                .InsertAndAdvance(new CodeInstruction(Call, ejectHelper));

            return matcher.InstructionEnumeration();
        }
    }
}
