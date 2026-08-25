using HarmonyLib;
using UnityEngine;

namespace GalacticScale
{
    public class PatchOnDysonSwarm
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddBullet))]
        public static void AddBullet(ref SailBullet bullet, int orbitId)
        {
            VectorLF3 delta = bullet.uEnd - bullet.uBegin;
            float targetDist = (float)delta.magnitude;
            if (targetDist > 1000f)
            {
                // Scale bullet velocity: v = 5000 * sqrt(targetDist / 40000)
                // Keeps flight time within ~8s - 25s across systems of all sizes
                float speedMultiplier = Mathf.Max(1.0f, Mathf.Sqrt(targetDist / 40000.0f));
                float scaledSpeed = 5000.0f * speedMultiplier;
                bullet.maxt = targetDist / scaledSpeed;
            }
        }
    }
}
