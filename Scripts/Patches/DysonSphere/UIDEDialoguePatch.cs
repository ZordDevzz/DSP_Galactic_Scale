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
                        float pOrbit = (float)((double)p.orbitRadius * 40000.0);
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
                        float pOrbit = (float)((double)p.orbitRadius * 40000.0);
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
                for (int j = 0; j < 10; j++)
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
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.QueryLayerRadius))]
        public static bool QueryLayerRadius(DysonSphere __instance, ref float orbitRadius, out float orbitAngularSpeed, ref bool __result)
        {
            DysonSphereUtils.RefreshDysonOrbitBounds(__instance);
            orbitAngularSpeed = 0f;
            if (orbitRadius < __instance.minOrbitRadius) orbitRadius = __instance.minOrbitRadius;
            if (orbitRadius > __instance.maxOrbitRadius) orbitRadius = __instance.maxOrbitRadius;

            if (__instance.layersSorted != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (__instance.layersSorted[i] != null && Mathf.Abs(__instance.layersSorted[i].orbitRadius - orbitRadius) < 999.95f)
                    {
                        orbitRadius = Mathf.Ceil(__instance.layersSorted[i].orbitRadius + 1000f);
                    }
                }
            }
            if (orbitRadius > __instance.maxOrbitRadius)
            {
                orbitRadius = __instance.maxOrbitRadius;
                __result = false;
                return false;
            }
            __result = true;
            return false;
        }
    }
}
