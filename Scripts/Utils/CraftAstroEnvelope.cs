using System;

namespace GalacticScale
{
    public static partial class Utils
    {
        // Craft star-frame envelope for DetermineCraftAstroId (#258 / #247).
        //
        // Vanilla hardcodes the star-frame ENTER threshold at 40.5 AU
        // (ldc.r8 1619999.9570846558 meters) and the EJECT threshold at 45 AU
        // (ldc.r8 3240000000000.0 = (1.8e6 m)^2, compared against sqrMagnitude).
        // GS2 stars can have system radii and Dark Fog hive orbits far beyond
        // 40.5 AU, so craft near those hives never enter the star's astro frame
        // (astroId stays 0) and SensorLogic_Space's star-astroId gate rejects
        // every enemy: fleets can never auto-acquire (live-confirmed on the
        // issue-258 save: 0 gate passes in 7837 shadow evaluations).
        //
        // Envelope = max(systemRadius, hive orbit max, LIVE hive astro distance)
        // * 1.05, floored at the vanilla 40.5 AU so small systems never get a
        // TIGHTER gate than vanilla. The live-distance term matters because
        // GS2's SpaceSector.Import prefix clamps oversized hiveAstroOrbit radii
        // down to systemRadius, but does not move the already-placed hive astro
        // - the actual position is the truth the envelope must cover.
        // Enter and eject scale together, preserving the vanilla 45/40.5 ratio
        // so the hysteresis band behaves identically at any scale.

        const double VanillaEnterAu = 40.5;
        const double VanillaCraftEnterMeters = 1619999.9570846558; // the IL literal
        const double VanillaCraftEjectMetersSqr = 3240000000000.0; // (45 AU in m)^2
        const double CraftEjectOverEnter = 45.0 / 40.5;
        const double CraftEnvelopePad = 1.05;
        const double AuMeters = 40000.0;

        static double CraftAstroEnvelopeAu(StarData star)
        {
            if (star == null) return VanillaEnterAu;
            double maxHive = 0.0;
            var orbits = star.hiveAstroOrbits;
            if (orbits != null)
                for (var i = 0; i < orbits.Length; i++)
                    if (orbits[i] != null && orbits[i].orbitRadius > maxHive)
                        maxHive = orbits[i].orbitRadius;
            // live hive astro positions beat the (possibly import-clamped) orbit table.
            // astros[] is indexed by hiveAstroId - 1000000 (not hiveAstroId).
            // dfHives[star.index] is the list head; further hives are nextSibling.
            var sector = GameMain.spaceSector;
            var astros = sector?.astros;
            var heads = sector?.dfHives;
            if (astros != null && heads != null
                && star.index >= 0 && star.index < heads.Length)
            {
                for (var hive = heads[star.index]; hive != null; hive = hive.nextSibling)
                {
                    if (hive.starData != star) continue;
                    var hiveId = hive.hiveAstroId - 1000000;
                    if (hiveId < 0 || hiveId >= astros.Length) continue;
                    if (astros[hiveId].id <= 0) continue;
                    var d = (astros[hiveId].uPos - star.uPosition).magnitude / AuMeters;
                    if (d > maxHive) maxHive = d;
                }
            }
            var env = Math.Max(star.systemRadius, maxHive) * CraftEnvelopePad;
            return Math.Max(env, VanillaEnterAu);
        }

        public static double GetCraftAstroEnterMeters(StarData star)
        {
            try { return CraftAstroEnvelopeAu(star) * AuMeters; }
            catch (Exception) { return VanillaCraftEnterMeters; }
        }

        public static double GetCraftAstroEjectMetersSqr(StarData star)
        {
            try
            {
                var eject = CraftAstroEnvelopeAu(star) * AuMeters * CraftEjectOverEnter;
                return eject * eject;
            }
            catch (Exception) { return VanillaCraftEjectMetersSqr; }
        }
    }
}
