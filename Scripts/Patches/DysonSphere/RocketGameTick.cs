using System;
using HarmonyLib;
using UnityEngine;

namespace GalacticScale
{
    public class PatchOnDysonSphereRocket
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RocketGameTick))]
        public static bool RocketGameTick(DysonSphere __instance)
        {
            try
            {
                if (__instance == null || __instance.starData == null) return true;

                AstroData[] astrosData = __instance.starData.galaxy.astrosData;
                double num = 1.0 / 60.0;

                // Scale rocket velocity and acceleration dynamically based on system max orbit radius
                float scaleRatio = Mathf.Max(1f, __instance.maxOrbitRadius / 40000f);
                float speedMultiplier = Mathf.Pow(scaleRatio, 0.6f);

                float num2 = speedMultiplier;
                float num3 = 7.5f * speedMultiplier;     // Acceleration scales with system size
                float num4 = 18f * num2;                 // Deceleration
                float num5 = 2800f * num2;               // Cruise speed
                VectorLF3 vectorLF = default(VectorLF3);

                for (int i = 1; i < __instance.rocketCursor; i++)
                {
                    if (__instance.rocketPool[i].id != i)
                    {
                        continue;
                    }
                    ref DysonRocket reference = ref __instance.rocketPool[i];
                    if (reference.node == null)
                    {
                        __instance.RemoveDysonRocket(i);
                        continue;
                    }

                    DysonSphereLayer dysonSphereLayer = __instance.layersIdBased[reference.node.layerId];
                    if (dysonSphereLayer == null)
                    {
                        __instance.RemoveDysonRocket(i);
                        continue;
                    }

                    ref AstroData reference2 = ref astrosData[reference.planetId];
                    vectorLF.x = reference2.uPos.x - reference.uPos.x;
                    vectorLF.y = reference2.uPos.y - reference.uPos.y;
                    vectorLF.z = reference2.uPos.z - reference.uPos.z;
                    double num6 = Math.Sqrt(vectorLF.x * vectorLF.x + vectorLF.y * vectorLF.y + vectorLF.z * vectorLF.z) - (double)reference2.uRadius;

                    if (reference.t <= 0f)
                    {
                        if (num6 < 200.0)
                        {
                            float num7 = (float)num6 / 200f;
                            if (num7 < 0f) num7 = 0f;
                            float num8 = num7 * num7 * 600f + 15f;
                            reference.uSpeed = reference.uSpeed * 0.9f + num8 * 0.1f;
                            reference.t = (num7 - 1f) * 1.2f;
                            if (reference.t < -1f) reference.t = -1f;
                        }
                        else
                        {
                            dysonSphereLayer.NodeEnterUPos(reference.node, out var result);
                            VectorLF3 vectorLF2 = result - reference.uPos;
                            double num9 = Math.Sqrt(vectorLF2.x * vectorLF2.x + vectorLF2.y * vectorLF2.y + vectorLF2.z * vectorLF2.z);
                            if (num9 < 50.0)
                            {
                                reference.t = 0.0001f;
                            }
                            else
                            {
                                reference.t = 0f;
                            }

                            double num10 = num9 / ((double)reference.uSpeed + 0.1) * 0.382;
                            double num11 = num9 / (double)num5;
                            float num12 = (float)((double)reference.uSpeed * num10) + 150f;
                            if (num12 > num5) num12 = num5;

                            if (reference.uSpeed < num12 - num3)
                            {
                                reference.uSpeed += num3;
                            }
                            else if (reference.uSpeed > num12 + num4)
                            {
                                reference.uSpeed -= num4;
                            }
                            else
                            {
                                reference.uSpeed = num12;
                            }

                            int num13 = -1;
                            double num14 = 0.0;
                            double num15 = 1E+40;
                            int num16 = reference.planetId / 100 * 100;
                            for (int j = num16; j < num16 + 10; j++)
                            {
                                float uRadius = astrosData[j].uRadius;
                                if (!(uRadius < 1f))
                                {
                                    VectorLF3 vectorLF3 = reference.uPos - astrosData[j].uPos;
                                    double num17 = vectorLF3.x * vectorLF3.x + vectorLF3.y * vectorLF3.y + vectorLF3.z * vectorLF3.z;
                                    double num18 = 0.0 - ((double)reference.uVel.x * vectorLF3.x + (double)reference.uVel.y * vectorLF3.y + (double)reference.uVel.z * vectorLF3.z);
                                    if ((num18 > 0.0 || num17 < (double)(uRadius * uRadius * 7f)) && num17 < num15)
                                    {
                                        num14 = ((num18 < 0.0) ? 0.0 : num18);
                                        num13 = j;
                                        num15 = num17;
                                    }
                                }
                            }

                            VectorLF3 vectorLF4 = VectorLF3.zero;
                            float num19 = 0f;
                            if (num13 > 0)
                            {
                                float num20 = astrosData[num13].uRadius;
                                bool flag = num13 % 100 == 0;
                                if (flag)
                                {
                                    num20 = dysonSphereLayer.orbitRadius - 400f;
                                }
                                double num21 = 1.25;
                                VectorLF3 vectorLF5 = reference.uPos + (VectorLF3)reference.uVel * num14 - astrosData[num13].uPos;
                                double num22 = vectorLF5.magnitude / (double)num20;
                                if (num22 < num21)
                                {
                                    double num23 = Math.Sqrt(num15) - (double)num20 * 0.82;
                                    if (num23 < 1.0) num23 = 1.0;
                                    double num24 = (num22 - 1.0) / (num21 - 1.0);
                                    if (num24 < 0.0) num24 = 0.0;
                                    num24 = 1.0 - num24 * num24;
                                    double num25 = 0.0;
                                    num25 = (double)(reference.uSpeed - 6f) / num23 * 2.5 - 0.01;
                                    if (num25 > 1.5) num25 = 1.5;
                                    else if (num25 < 0.0) num25 = 0.0;
                                    num25 = num25 * num25 * num24;
                                    num19 = (float)(flag ? 0.0 : (num25 * 0.5));
                                    vectorLF4 = vectorLF5.normalized * num25 * 2.0;
                                }
                            }

                            float num26 = 1f / (float)num11 - 0.05f;
                            num26 += num19;
                            float num27 = Mathf.Lerp(0.005f, 0.08f, num26);
                            Vector3 val = Vector3.Slerp(reference.uVel, (Vector3)(vectorLF2.normalized + vectorLF4), num27);
                            reference.uVel = val.normalized;

                            Quaternion val2;
                            if (num9 < 350.0)
                            {
                                float num28 = ((float)num9 - 50f) / 300f;
                                val2 = Quaternion.Slerp(dysonSphereLayer.NodeURot(reference.node), Quaternion.LookRotation(reference.uVel), num28);
                            }
                            else
                            {
                                val2 = Quaternion.LookRotation(reference.uVel);
                            }
                            reference.uRot = Quaternion.Slerp(reference.uRot, val2, 0.2f);
                        }
                    }
                    else
                    {
                        dysonSphereLayer.NodeSlotUPos(reference.node, out var result2);
                        VectorLF3 vectorLF6 = result2 - reference.uPos;
                        double num29 = Math.Sqrt(vectorLF6.x * vectorLF6.x + vectorLF6.y * vectorLF6.y + vectorLF6.z * vectorLF6.z);
                        if (num29 < 2.0)
                        {
                            __instance.ConstructSp(reference.node);
                            __instance.RemoveDysonRocket(i);
                            continue;
                        }

                        float num30 = (float)(num29 * 0.75 + 15.0);
                        if (num30 > num5) num30 = num5;

                        if (reference.uSpeed < num30 - num3)
                        {
                            reference.uSpeed += num3;
                        }
                        else if (reference.uSpeed > num30 + num4)
                        {
                            reference.uSpeed -= num4;
                        }
                        else
                        {
                            reference.uSpeed = num30;
                        }

                        reference.uVel = Vector3.Slerp(reference.uVel, (Vector3)vectorLF6.normalized, 0.1f);
                        reference.uRot = Quaternion.Slerp(reference.uRot, dysonSphereLayer.NodeURot(reference.node), 0.2f);
                        reference.t = (350f - (float)num29) / 330f;
                        if (reference.t > 1f) reference.t = 1f;
                        else if (reference.t < 0.0001f) reference.t = 0.0001f;
                    }

                    VectorLF3 vectorLF7 = Vector3.zero;
                    bool flag2 = false;
                    double num31 = 2f - (float)num6 / 200f;
                    if (num31 > 1.0) num31 = 1.0;
                    else if (num31 < 0.0) num31 = 0.0;

                    if (num31 > 0.0)
                    {
                        VectorLF3 v = reference.uPos - reference2.uPos;
                        VectorLF3 v2 = Maths.QInvRotateLF(reference2.uRot, v);
                        VectorLF3 vectorLF8 = Maths.QRotateLF(reference2.uRotNext, v2) + reference2.uPosNext;
                        Quaternion val3 = Quaternion.Inverse(reference2.uRot) * reference.uRot;
                        Quaternion val4 = reference2.uRotNext * val3;
                        num31 = (3.0 - num31 - num31) * num31 * num31;
                        vectorLF7 = (vectorLF8 - reference.uPos) * num31;
                        reference.uRot = Quaternion.Slerp(reference.uRot, val4, (float)num31);
                        flag2 = true;
                    }

                    if (!flag2)
                    {
                        VectorLF3 v3 = reference.uPos - __instance.starData.uPosition;
                        double num32 = Math.Abs(Math.Sqrt(v3.x * v3.x + v3.y * v3.y + v3.z * v3.z) - (double)dysonSphereLayer.orbitRadius);
                        double num33 = 1.5 - (double)((float)num32 / 1800f);
                        if (num33 > 1.0) num33 = 1.0;
                        else if (num33 < 0.0) num33 = 0.0;

                        if (num33 > 0.0)
                        {
                            VectorLF3 v4 = Maths.QInvRotateLF(dysonSphereLayer.currentRotation, v3);
                            VectorLF3 vectorLF9 = Maths.QRotateLF(dysonSphereLayer.nextRotation, v4) + __instance.starData.uPosition;
                            Quaternion val5 = Quaternion.Inverse(dysonSphereLayer.currentRotation) * reference.uRot;
                            Quaternion val6 = dysonSphereLayer.nextRotation * val5;
                            num33 = (3.0 - num33 - num33) * num33 * num33;
                            vectorLF7 = (vectorLF9 - reference.uPos) * num33;
                            reference.uRot = Quaternion.Slerp(reference.uRot, val6, (float)num33);
                        }
                    }

                    double num34 = (double)reference.uSpeed * num;
                    reference.uPos.x = reference.uPos.x + (double)reference.uVel.x * num34 + vectorLF7.x;
                    reference.uPos.y = reference.uPos.y + (double)reference.uVel.y * num34 + vectorLF7.y;
                    reference.uPos.z = reference.uPos.z + (double)reference.uVel.z * num34 + vectorLF7.z;

                    vectorLF = reference2.uPos - reference.uPos;
                    num6 = Math.Sqrt(vectorLF.x * vectorLF.x + vectorLF.y * vectorLF.y + vectorLF.z * vectorLF.z) - (double)reference2.uRadius;
                    if (num6 < 180.0)
                    {
                        reference.uPos = reference2.uPos + Maths.QRotateLF(reference2.uRot, (VectorLF3)reference.launch * ((double)reference2.uRadius + num6));
                        reference.uRot = reference2.uRot * Quaternion.LookRotation(reference.launch);
                    }
                }

                return false; // Skip original unscaled method
            }
            catch (Exception ex)
            {
                GS2.Warn($"Error in PatchOnDysonSphereRocket.RocketGameTick: {ex.Message}\n{ex.StackTrace}");
                return true;
            }
        }
    }
}
