using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static ItemActionZoom;

public class FirstPersonView : IModApi
{

    // Shadow setup: We want the third party (full avatar) to
    // produce a shadow, but don't want to see the actual model.
    // Via `UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly`
    // We don't want the head light to produce any shadows for
    // the hand item or avatar either, to avoid self-shadowing.
    // By setting the layer to one that the light doesn't shadow

    public void InitMod(Mod mod)
    {
        Log.Out("OCB Harmony Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GamePrefs.Set(EnumGamePrefs.OptionsIntroMovieEnabled, false);
    }


    // Make sure the 1st party hands are always shown
    [HarmonyPatch(typeof(EntityPlayerLocal), "ShowWeaponCamera")]
    public class EntityPlayerLocalShowWeaponCameraPatch
    {
        public static bool Prefix(EntityPlayerLocal __instance, bool show)
        {
            Camera weaponCam = __instance.weaponCamera;
            Camera playerCam = __instance.playerCamera;
            // Show everything in player camera (also FPV arms)
            // Arms don't cast shadows, main model only has shadows
            playerCam.cullingMask |= (1 << Constants.cLayerHoldingItem);
            playerCam.cullingMask &= ~(1 << Constants.cLayerLocalPlayer);
            //playerCam.cullingMask &= ~(1 << Constants.cLayerHoldingItem);
            // Show nothing in weapons camera (all in player cam)
            weaponCam.cullingMask &= ~(1 << Constants.cLayerLocalPlayer);
            weaponCam.cullingMask &= ~(1 << Constants.cLayerHoldingItem);
            // Adjust the near clip distance
            playerCam.nearClipPlane = 0.02f;
            // Skip base setup
            return false;
        }
    }

    // Make sure head-gear isn't shown at all
    [HarmonyPatch(typeof(SDCSUtils), "Stitch")]
    public class SDCSUtilsStitchPatch
    {
        public static bool Prefix(GameObject sourceObj, bool isFPV)
        {
            // Render third person view normal
            bool gear = sourceObj.name.StartsWith("gear");
            // Disable shadows for first person view
            foreach (var renderer in sourceObj.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = !(gear && isFPV); // Do not render at all in FPV
                if (isFPV) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                else renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer meshRenderer)
                {
                    if (meshRenderer.name != "body") continue;
                    #pragma warning disable CS0618
                    meshRenderer.motionVectors = false;
                    #pragma warning restore CS0618
                }

            }
            return true;
        }
    }

    // Ensure that parts are added to the correct model
    [HarmonyPatch(typeof(MinEventActionAddPart), "Execute")]
    public class MinEventActionAddPartExecutePatch
    {
        static void Postfix(MinEventParams _params, bool __state)
        {
            if (!(_params.Self is EntityPlayerLocal self)) return;
            self.vp_FPCamera.Locked3rdPerson = __state;
        }
        static void Prefix(MinEventParams _params, ref bool __state)
        {
            if (!(_params.Self is EntityPlayerLocal self)) return;
            __state = self.vp_FPCamera.Locked3rdPerson;
            self.vp_FPCamera.Locked3rdPerson = true;
        }
    }

    // Ensure that parts are removed from the correct model
    [HarmonyPatch(typeof(MinEventActionRemovePart), "Execute")]
    public class MinEventActionRemovePartExecutePatch
    {
        static void Postfix(MinEventParams _params, bool __state)
        {
            if (!(_params.Self is EntityPlayerLocal self)) return;
            self.vp_FPCamera.Locked3rdPerson = __state;
        }
        static void Prefix(MinEventParams _params, ref bool __state)
        {
            if (!(_params.Self is EntityPlayerLocal self)) return;
            __state = self.vp_FPCamera.Locked3rdPerson;
            self.vp_FPCamera.Locked3rdPerson = true;
        }
    }

    [HarmonyPatch(typeof(ItemActionZoom), "startEndZoom")]
    public class ItemActionZoomStartEndZoomPatch
    {
        public static void Postfix(ItemActionData _actionData)
        {
            if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
                player.ShowWeaponCamera(true); // Update before/after zooming
        }
    }

    // Hide weapons when zoomed in (only show screen overlay)
    // Ensures no weapons accessories will obstruct the view
    [HarmonyPatch(typeof(ItemActionZoom), "OnScreenOverlay")]
    public class ItemActionZoomOnScreenOverlayPatch
    {
        public static void Postfix(ItemActionData _actionData)
        {
            // if (Enabled == false) return;
            if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
            {
                if (player.AimingGun == false) return;
                if (!(_actionData is ItemActionDataZoom data)) return;
                if (data.Scope == null) return;
                if (data.bZoomInProgress) return;
                if (data.ZoomOverlay == null) return;
                if (player.playerCamera == null) return;
                player.playerCamera.cullingMask &= ~(1 << Constants.cLayerLocalPlayer);
                player.playerCamera.cullingMask &= ~(1 << Constants.cLayerHoldingItem);
            }
        }
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "SetInRightHand")]
    public class AvatarLocalPlayerController_SetInRightHand
    {
        // static Transform LastHeldItem = null;
        static void Postfix(AvatarLocalPlayerController __instance, EntityAlive ___entity)
        {
            if (___entity?.inventory?.GetHoldingItemTransform() is Transform holditem)
            {
                foreach (var renderer in holditem.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var lod in holditem.GetComponentsInChildren<LightLOD>())
                {
                    if (lod.name == "Light_FirstPerson") lod.enabled = false;
                    else if (lod.name == "Light_ThirdPerson") lod.enabled = true;
                }
            }
        }

    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "SwitchModelAndView")]
    public class AvatarLocalPlayerControllerSwitchModelAndViewPatch
    {
        public static void Postfix(ref bool _bFPV,
            AvatarLocalPlayerController __instance)
        {
            // Update the cameras to show the correct layers
            if (__instance.FPSArms is BodyAnimator fpsAnimator)
            {
                // if (Enabled) fpsAnimator.State = BodyAnimator.EnumState.Visible;
                if (fpsAnimator.Parts.BodyObj.transform is Transform bodyTransform)
                {
                    // Force body to be always visible (force shadow only later)
                    foreach (var body in bodyTransform.GetComponentsInChildren<Renderer>(true))
                    {
                        if (_bFPV) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        else body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }

                if (fpsAnimator.Parts.RightHandT.transform is Transform handTransform)
                {
                    // Force body to be always visible (force shadow only later)
                    // if (Enabled) bodyAnimator.State = BodyAnimator.EnumState.Visible;
                    foreach (var hand in handTransform.GetComponentsInChildren<Renderer>(true))
                    {
                        if (_bFPV) hand.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        else hand.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
        }
    }

}
