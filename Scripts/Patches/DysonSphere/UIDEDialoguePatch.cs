using System;
using HarmonyLib;
using UnityEngine;

namespace GalacticScale
{
    public static class DysonSphereUtils
    {
        public static void RefreshDysonOrbitBounds(DysonSphere dysonSphere)
        {
            if (dysonSphere == null || dysonSphere.starData == null) return;
            StarData starData = dysonSphere.starData;

            // 1. Min Orbit Radius = Star Physics Radius + 0.1 AU (4000m)
            dysonSphere.minOrbitRadius = starData.physicsRadius + 4000f;
            if (dysonSphere.minOrbitRadius < 4000f)
            {
                dysonSphere.minOrbitRadius = 4000f;
            }

            // 2. Max Orbit Radius = Farthest planet orbit + 1.0 AU (40000m)
            float maxPlanetOrbitMeters = 0f;
            if (starData.planets != null && starData.planets.Length > 0)
            {
                for (int i = 0; i < starData.planets.Length; i++)
                {
                    if (starData.planets[i] != null && starData.planets[i].orbitRadius > 0f)
                    {
                        float pOrbitAU = starData.planets[i].orbitAround != 0 && starData.planets[i].orbitAroundPlanet != null
                            ? starData.planets[i].orbitAroundPlanet.orbitRadius
                            : starData.planets[i].orbitRadius;
                        float pOrbit = (float)((double)pOrbitAU * 40000.0);
                        if (pOrbit > maxPlanetOrbitMeters)
                        {
                            maxPlanetOrbitMeters = pOrbit;
                        }
                    }
                }
            }

            if (maxPlanetOrbitMeters > 0f)
            {
                dysonSphere.maxOrbitRadius = maxPlanetOrbitMeters + 40000f; // Farthest planet + 1.0 AU
            }
            else
            {
                dysonSphere.maxOrbitRadius = dysonSphere.minOrbitRadius + 40000f; // Fallback: min + 1.0 AU
            }

            // Legacy saves: spheres built under older formulas may have layers exceeding new bounds
            if (dysonSphere.layersSorted != null)
            {
                for (int i = 0; i < dysonSphere.layersSorted.Length; i++)
                {
                    if (dysonSphere.layersSorted[i] != null && dysonSphere.layersSorted[i].orbitRadius + 4000f > dysonSphere.maxOrbitRadius)
                    {
                        dysonSphere.maxOrbitRadius = dysonSphere.layersSorted[i].orbitRadius + 4000f;
                    }
                }
            }

            if (dysonSphere.maxOrbitRadius < dysonSphere.minOrbitRadius + 4000f)
            {
                dysonSphere.maxOrbitRadius = dysonSphere.minOrbitRadius + 4000f;
            }

            // 3. Default Orbit Radius: comfortable 25% zone between min and max
            dysonSphere.defOrbitRadius = dysonSphere.minOrbitRadius + (dysonSphere.maxOrbitRadius - dysonSphere.minOrbitRadius) * 0.25f;
            dysonSphere.defOrbitRadius = Mathf.Round(dysonSphere.defOrbitRadius / 100f) * 100f;
            dysonSphere.minOrbitRadius = Mathf.Ceil(dysonSphere.minOrbitRadius / 100f) * 100f;
            dysonSphere.maxOrbitRadius = Mathf.Round(dysonSphere.maxOrbitRadius / 100f) * 100f;
        }
    }

    public class PatchOnUIDEDialogues
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIDysonEditor), nameof(UIDysonEditor.OnViewStarChange))]
        public static void UIDysonEditor_OnViewStarChange_Prefix(UIDysonEditor __instance)
        {
            if (__instance != null && __instance.selection != null && __instance.selection.viewDysonSphere != null)
            {
                DysonSphereUtils.RefreshDysonOrbitBounds(__instance.selection.viewDysonSphere);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDysonEditor), nameof(UIDysonEditor.OnViewStarChange))]
        public static void UIDysonEditor_OnViewStarChange_Postfix(UIDysonEditor __instance)
        {
            if (__instance == null || __instance.selection == null || __instance.selection.viewDysonSphere == null || __instance.cameraController == null) return;
            
            var dysonSphere = __instance.selection.viewDysonSphere;
            var star = __instance.selection.viewStar;
            if (star == null) return;

            // Recalibrate Camera distance & clipping planes based on actual maxOrbitRadius
            float maxOrbitUnity = dysonSphere.maxOrbitRadius * 0.00025f;
            float defOrbitUnity = dysonSphere.defOrbitRadius * 0.00025f;
            float starPhysicsUnity = (float)((double)star.physicsRadius * 0.00025 * 2.0);

            __instance.cameraController.minDist = Mathf.Max(5f, starPhysicsUnity * 0.8f);
            __instance.cameraController.maxDist = Mathf.Max(maxOrbitUnity * 3.5f, 300f);
            
            float targetDist = Mathf.Max(defOrbitUnity * 2.6f, __instance.cameraController.minDist * 1.5f);
            __instance.cameraController.dist = targetDist;
            __instance.cameraController.distWanted = targetDist;

            if (__instance.screenCamera != null)
            {
                __instance.screenCamera.nearClipPlane = Mathf.Min(0.5f, __instance.cameraController.minDist * 0.1f);
                __instance.screenCamera.farClipPlane = __instance.cameraController.maxDist * 2.5f;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIDEAddSwarmDialogue), nameof(UIDEAddSwarmDialogue.OnViewStarChange))]
        public static void SwarmOnViewStarChange_Prefix(UIDEAddSwarmDialogue __instance, DysonSphere dysonSphere)
        {
            if (dysonSphere != null)
            {
                DysonSphereUtils.RefreshDysonOrbitBounds(dysonSphere);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddSwarmDialogue), nameof(UIDEAddSwarmDialogue.OnViewStarChange))]
        public static void SwarmOnViewStarChange_Postfix(UIDEAddSwarmDialogue __instance, DysonSphere dysonSphere)
        {
            if (dysonSphere != null && __instance.slider0 != null)
            {
                __instance.slider0.minValue = dysonSphere.minOrbitRadius;
                __instance.slider0.maxValue = dysonSphere.maxOrbitRadius;
                __instance.slider0.value = Mathf.Clamp(dysonSphere.defOrbitRadius, __instance.slider0.minValue, __instance.slider0.maxValue);
                if (__instance.input0 != null)
                {
                    __instance.input0.text = __instance.slider0.value.ToString("0");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddSwarmDialogue), "_OnOpen")]
        public static void SwarmOnOpen_Postfix(UIDEAddSwarmDialogue __instance)
        {
            if (__instance != null && __instance.editor != null && __instance.editor.cameraController != null && __instance.slider0 != null)
            {
                float targetOrbitUnity = __instance.slider0.value * 0.00025f;
                __instance.editor.cameraController.distWanted = Mathf.Max(__instance.editor.cameraController.distWanted, targetOrbitUnity * 2.6f);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddSwarmDialogue), nameof(UIDEAddSwarmDialogue.OnSlider0Change))]
        public static void SwarmOnSlider0Change_Postfix(UIDEAddSwarmDialogue __instance, float val)
        {
            if (__instance != null && __instance.editor != null && __instance.editor.cameraController != null)
            {
                float targetOrbitUnity = val * 0.00025f;
                float neededDist = targetOrbitUnity * 2.6f;
                if (__instance.editor.cameraController.distWanted < neededDist)
                {
                    __instance.editor.cameraController.distWanted = neededDist;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIDEAddLayerDialogue), nameof(UIDEAddLayerDialogue.OnViewStarChange))]
        public static void LayerOnViewStarChange_Prefix(UIDEAddLayerDialogue __instance, DysonSphere dysonSphere)
        {
            if (dysonSphere != null)
            {
                DysonSphereUtils.RefreshDysonOrbitBounds(dysonSphere);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddLayerDialogue), nameof(UIDEAddLayerDialogue.OnViewStarChange))]
        public static void LayerOnViewStarChange_Postfix(UIDEAddLayerDialogue __instance, DysonSphere dysonSphere)
        {
            if (dysonSphere != null && __instance.slider0 != null)
            {
                __instance.slider0.minValue = dysonSphere.minOrbitRadius;
                __instance.slider0.maxValue = dysonSphere.maxOrbitRadius;
                __instance.slider0.value = Mathf.Clamp(dysonSphere.defOrbitRadius, __instance.slider0.minValue, __instance.slider0.maxValue);
                if (__instance.input0 != null)
                {
                    __instance.input0.text = __instance.slider0.value.ToString("0");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddLayerDialogue), "_OnOpen")]
        public static void LayerOnOpen_Postfix(UIDEAddLayerDialogue __instance)
        {
            if (__instance != null && __instance.editor != null && __instance.editor.cameraController != null && __instance.slider0 != null)
            {
                float targetOrbitUnity = __instance.slider0.value * 0.00025f;
                __instance.editor.cameraController.distWanted = Mathf.Max(__instance.editor.cameraController.distWanted, targetOrbitUnity * 2.6f);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIDEAddLayerDialogue), nameof(UIDEAddLayerDialogue.OnSlider0Change))]
        public static void LayerOnSlider0Change_Postfix(UIDEAddLayerDialogue __instance, float val)
        {
            if (__instance != null && __instance.editor != null && __instance.editor.cameraController != null)
            {
                float targetOrbitUnity = val * 0.00025f;
                float neededDist = targetOrbitUnity * 2.6f;
                if (__instance.editor.cameraController.distWanted < neededDist)
                {
                    __instance.editor.cameraController.distWanted = neededDist;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.CheckSwarmRadius))]
        public static bool CheckSwarmRadius(DysonSphere __instance, float swarmRadius, ref int __result)
        {
            DysonSphereUtils.RefreshDysonOrbitBounds(__instance);
            if (swarmRadius < __instance.minOrbitRadius || swarmRadius > __instance.maxOrbitRadius)
            {
                __result = -3;
                return false;
            }
            if (__instance.starData?.planets != null)
            {
                for (int i = 0; i < __instance.starData.planets.Length; i++)
                {
                    var p = __instance.starData.planets[i];
                    if (p != null && p.orbitRadius > 0f)
                    {
                        float pOrbitAU = p.orbitAround != 0 && p.orbitAroundPlanet != null ? p.orbitAroundPlanet.orbitRadius : p.orbitRadius;
                        float pOrbit = (float)((double)pOrbitAU * 40000.0);
                        if (Mathf.Abs(pOrbit - swarmRadius) < 2199.95f)
                        {
                            __result = -2;
                            return false;
                        }
                    }
                }
            }
            __result = 0;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.CheckLayerRadius))]
        public static bool CheckLayerRadius(DysonSphere __instance, float orbitRadius, ref int __result)
        {
            DysonSphereUtils.RefreshDysonOrbitBounds(__instance);
            if (orbitRadius < __instance.minOrbitRadius || orbitRadius > __instance.maxOrbitRadius)
            {
                __result = -3;
                return false;
            }
            if (__instance.starData?.planets != null)
            {
                for (int i = 0; i < __instance.starData.planets.Length; i++)
                {
                    var p = __instance.starData.planets[i];
                    if (p != null && p.orbitRadius > 0f)
                    {
                        float pOrbitAU = p.orbitAround != 0 && p.orbitAroundPlanet != null ? p.orbitAroundPlanet.orbitRadius : p.orbitRadius;
                        float pOrbit = (float)((double)pOrbitAU * 40000.0);
                        if (Mathf.Abs(pOrbit - orbitRadius) < 2199.95f)
                        {
                            __result = -2;
                            return false;
                        }
                    }
                }
            }
            if (__instance.layersSorted != null)
            {
                for (int j = 0; j < __instance.layersSorted.Length; j++)
                {
                    if (__instance.layersSorted[j] != null && Mathf.Abs(__instance.layersSorted[j].orbitRadius - orbitRadius) < 999.95f)
                    {
                        __result = -1;
                        return false;
                    }
                }
            }
            __result = 0;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.QuerySwarmRadius))]
        public static bool QuerySwarmRadius(DysonSphere __instance, ref float orbitRadius)
        {
            DysonSphereUtils.RefreshDysonOrbitBounds(__instance);
            if (orbitRadius < __instance.minOrbitRadius) orbitRadius = __instance.minOrbitRadius;
            if (orbitRadius > __instance.maxOrbitRadius) orbitRadius = __instance.maxOrbitRadius;

            float requestedRadius = orbitRadius;

            float cand1 = PushPlanetsCeil(__instance, requestedRadius);
            if (cand1 > __instance.maxOrbitRadius)
                cand1 = __instance.maxOrbitRadius;
            cand1 = PushPlanetsFloor(__instance, cand1);

            float cand2 = PushPlanetsFloor(__instance, requestedRadius);
            if (cand2 < __instance.minOrbitRadius)
                cand2 = __instance.minOrbitRadius;
            cand2 = PushPlanetsCeil(__instance, cand2);

            bool c1ok = cand1 >= __instance.minOrbitRadius && cand1 <= __instance.maxOrbitRadius;
            bool c2ok = cand2 >= __instance.minOrbitRadius && cand2 <= __instance.maxOrbitRadius;
            if (c1ok && c2ok)
                orbitRadius = Mathf.Abs(cand1 - requestedRadius) < Mathf.Abs(cand2 - requestedRadius) ? cand1 : cand2;
            else if (c1ok)
                orbitRadius = cand1;
            else if (c2ok)
                orbitRadius = cand2;
            // else: the whole [min,max] band sits inside a ring; leave the clamped request.
            return false;
        }

        private static float PushLayersCeil(DysonSphere dysonSphere, float radius)
        {
            if (dysonSphere.layersSorted != null)
            {
                for (int i = 0; i < dysonSphere.layersSorted.Length; i++)
                {
                    var layer = dysonSphere.layersSorted[i];
                    if (layer != null && Mathf.Abs(layer.orbitRadius - radius) < 999.95f)
                    {
                        radius = Mathf.Ceil(layer.orbitRadius + 1000f);
                    }
                }
            }
            return radius;
        }

        private static float PushLayersFloor(DysonSphere dysonSphere, float radius)
        {
            if (dysonSphere.layersSorted != null)
            {
                for (int i = dysonSphere.layersSorted.Length - 1; i >= 0; i--)
                {
                    var layer = dysonSphere.layersSorted[i];
                    if (layer != null && Mathf.Abs(layer.orbitRadius - radius) < 999.95f)
                    {
                        radius = Mathf.Floor(layer.orbitRadius - 1000f);
                    }
                }
            }
            return radius;
        }

        private static float PushPlanetsCeil(DysonSphere dysonSphere, float radius)
        {
            if (dysonSphere.starData?.planets != null)
            {
                for (int i = 0; i < dysonSphere.starData.planets.Length; i++)
                {
                    var p = dysonSphere.starData.planets[i];
                    if (p != null && p.orbitRadius > 0f)
                    {
                        float pOrbitAU = p.orbitAround != 0 && p.orbitAroundPlanet != null ? p.orbitAroundPlanet.orbitRadius : p.orbitRadius;
                        float pOrbit = (float)((double)pOrbitAU * 40000.0);
                        if (Mathf.Abs(pOrbit - radius) < 2199.95f)
                        {
                            radius = Mathf.Ceil(pOrbit + 2200f);
                        }
                    }
                }
            }
            return radius;
        }

        private static float PushPlanetsFloor(DysonSphere dysonSphere, float radius)
        {
            if (dysonSphere.starData?.planets != null)
            {
                for (int i = dysonSphere.starData.planets.Length - 1; i >= 0; i--)
                {
                    var p = dysonSphere.starData.planets[i];
                    if (p != null && p.orbitRadius > 0f)
                    {
                        float pOrbitAU = p.orbitAround != 0 && p.orbitAroundPlanet != null ? p.orbitAroundPlanet.orbitRadius : p.orbitRadius;
                        float pOrbit = (float)((double)pOrbitAU * 40000.0);
                        if (Mathf.Abs(pOrbit - radius) < 2199.95f)
                        {
                            radius = Mathf.Floor(pOrbit - 2200f);
                        }
                    }
                }
            }
            return radius;
        }

        private static float ComputeAngularSpeed(float gravity, float orbitRadius)
        {
            if (orbitRadius <= 0f || gravity <= 0f) return 0f;
            return Mathf.Sqrt(gravity / orbitRadius) / orbitRadius * 57.29578f;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.QueryLayerRadius))]
        public static bool QueryLayerRadius(DysonSphere __instance, ref float orbitRadius, out float orbitAngularSpeed, ref bool __result)
        {
            DysonSphereUtils.RefreshDysonOrbitBounds(__instance);
            orbitAngularSpeed = 0f;

            if (orbitRadius < __instance.minOrbitRadius) orbitRadius = __instance.minOrbitRadius;
            if (orbitRadius > __instance.maxOrbitRadius) orbitRadius = __instance.maxOrbitRadius;

            float requestedRadius = orbitRadius;

            // Pass 1: Forward-first search (push outward, then inward if needed)
            float cand1 = requestedRadius;
            cand1 = PushLayersCeil(__instance, cand1);
            cand1 = PushPlanetsCeil(__instance, cand1);
            cand1 = PushLayersCeil(__instance, cand1);
            if (cand1 > __instance.maxOrbitRadius)
            {
                cand1 = __instance.maxOrbitRadius;
            }
            cand1 = PushLayersFloor(__instance, cand1);
            cand1 = PushPlanetsFloor(__instance, cand1);
            cand1 = PushLayersFloor(__instance, cand1);

            if (cand1 < __instance.minOrbitRadius)
            {
                orbitRadius = __instance.minOrbitRadius;
                orbitAngularSpeed = ComputeAngularSpeed(__instance.gravity, orbitRadius);
                __result = false;
                return false;
            }

            // Pass 2: Backward-first search (push inward, then outward if needed)
            float cand2 = requestedRadius;
            cand2 = PushLayersFloor(__instance, cand2);
            cand2 = PushPlanetsFloor(__instance, cand2);
            cand2 = PushLayersFloor(__instance, cand2);
            if (cand2 < __instance.minOrbitRadius)
            {
                cand2 = __instance.minOrbitRadius;
            }
            cand2 = PushLayersCeil(__instance, cand2);
            cand2 = PushPlanetsCeil(__instance, cand2);
            cand2 = PushLayersCeil(__instance, cand2);

            if (cand2 > __instance.maxOrbitRadius)
            {
                orbitRadius = __instance.minOrbitRadius;
                orbitAngularSpeed = ComputeAngularSpeed(__instance.gravity, orbitRadius);
                __result = false;
                return false;
            }

            // Select candidate closest to the originally requested radius
            if (Mathf.Abs(cand1 - requestedRadius) < Mathf.Abs(cand2 - requestedRadius))
            {
                orbitRadius = cand1;
            }
            else
            {
                orbitRadius = cand2;
            }

            orbitAngularSpeed = ComputeAngularSpeed(__instance.gravity, orbitRadius);
            __result = true;
            return false;
        }
    }
}
