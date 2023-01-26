using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class FirstPersonView : IModApi
{

    public void InitMod(Mod mod)
    {
        Debug.Log("Loading OCB First Person View Patch: " + GetType().ToString());
        new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
    }

    private static bool enabled = true;

    public static void Enable(bool on)
    {
        enabled = on;
    }

    public static void UpdatePlayerCamera(Camera camera)
    {
        if (camera == null) return;
        if (enabled)
        {
            // Always show the character
            camera.cullingMask |= 1024;
            // Allow weapon to render closer
            // May also fix some other clipping
            camera.nearClipPlane = 0.02f;
            // This fixes some light issues?
            //camera.cullingMask = 277087745;
        }
        else
        {
            // Reset to the default values
            // camera.nearClipPlane = 0.095f;
            camera.cullingMask &= ~1024;
        }
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController))]
    [HarmonyPatch("SwitchModelAndView")]
    public class AvatarLocalPlayerController_SwitchModelAndView
    {
        public static void Postfix(ref bool _bFPV,
            AvatarLocalPlayerController __instance)
        {

            // Update the cameras to show the correct layers
            if (GameManager.Instance.World.GetPrimaryPlayer() is EntityPlayerLocal player)
            {
                // Show character (hand) for player camera
                UpdatePlayerCamera(player.playerCamera);
            }

            // Setup the main character body
            if (__instance.CharacterBody is BodyAnimator bodyAnimator)
            {
                if (bodyAnimator.Parts.BodyTransform is Transform bodyTransform)
                {
                    // Force body to be always visible (force shadow only later)
                    if (enabled) bodyAnimator.State = BodyAnimator.EnumState.Visible;
                    foreach (var body in bodyTransform.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        // Setup shadow casting for the main character body (either fully or just shadows)
                        if (!enabled) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        else if (!_bFPV) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        else body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                }
            }

            // This has some interesting side effects ...
            //__instance.PrimaryBody = __instance.CharacterBody;

            // Abort now, nothing to revert from this point on
            if (enabled == false || _bFPV == false) return;

            // Setup the "first person view" arms
            if (__instance.FPSArms is BodyAnimator fpsArmsAnimator)
            {
                fpsArmsAnimator.State = BodyAnimator.EnumState.Visible;
                if (fpsArmsAnimator.Parts.BodyTransform is Transform bodyTransform)
                {
                    foreach (var arm in bodyTransform.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        // Disable shadow casting for the "first person view arms"
                        arm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }

        }
    }

    [HarmonyPatch(typeof(EModelBase))]
    [HarmonyPatch("SwitchModelAndView")]
    public class EModelBase_SwitchModelAndView
    {
        public static void Postfix(EModelBase __instance,
            ref Transform ___spotlightTransform)
        {
            if (__instance.GetModelTransform() is Transform root)
            {
                if (root.GetComponent<Animator>() is Animator animator)
                {
                    // Make sure our main model has animated shadows
                    animator.enabled = enabled || !__instance.IsFPV;
                    // Mark animator to not trigger any events
                    // Customized SetTrigger function is below
                    animator.fireEvents = !__instance.IsFPV;
                }
            }
        }
    }
    
    [HarmonyPatch(typeof(ItemActionZoom))]
    [HarmonyPatch("OnScreenOverlay")]
    public class ItemActionZoom_OnScreenOverlay
    {

        static System.Type ActionDataZoom = AccessTools.TypeByName("ItemActionDataZoom");
        static FieldInfo ActionDataZoomScope = ActionDataZoom.GetField("Scope");
        static FieldInfo ActionDataZoomOverlay = ActionDataZoom.GetField("ZoomOverlay");
        static FieldInfo ActionDataZoomInProgress = ActionDataZoom.GetField("bZoomInProgress");

        public static void Postfix(ItemActionData _actionData)
        {
            if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
            {
                if (player.AimingGun == false) return;
                if (ActionDataZoomScope.GetValue(_actionData) == null) return;
                if ((bool)ActionDataZoomInProgress.GetValue(_actionData)) return;
                if (ActionDataZoomOverlay.GetValue(_actionData) == null) return;
                if (player.playerCamera != null) player.playerCamera.cullingMask &= -1025;
            }

        }
    }

    [HarmonyPatch(typeof(ItemActionZoom))]
    [HarmonyPatch("startEndZoom")]
    public class ItemActionZoom_startEndZoom
    {
        public static void Postfix()
        {
            if (enabled == false) return;
            if (GameManager.Instance.World.GetPrimaryPlayer() is EntityPlayerLocal player)
            {
                if (player.playerCamera != null) player.playerCamera.cullingMask |= 1024;
                // Weapons Camera never shows the character/hand (remove layer 1024)
                if (player.weaponCamera != null) player.weaponCamera.cullingMask &= -1025;
            }
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal))]
    [HarmonyPatch("ShowWeaponCamera")]
    public class EntityPlayerLocal_ShowWeaponCamera
    {
        public static void Postfix(EntityPlayerLocal __instance, bool show)
        {
            if (enabled == false) return;
            Camera camera = __instance.vp_FPCamera.transform.
                FindInChilds("WeaponCamera").GetComponent<Camera>();
            // Weapons Camera never shows the character/hand
            if (camera != null) camera.cullingMask &= -1025;
        }
    }

    // Copied from original and added `fireEvents` condition
    [HarmonyPatch(typeof(AvatarMultiBodyController))]
    [HarmonyPatch("SetTrigger")]
    public class AvatarMultiBodyController_SetTrigger
    {
        public static bool Prefix(
            AvatarMultiBodyController __instance,
            Dictionary<int, AnimParamData> ___ChangedAnimationParameters,
            ref List<BodyAnimator> ___bodyAnimators,
            ref bool ___changed,
            int _propertyHash)
        {
            ___changed = false;
            for (int index = 0; index < ___bodyAnimators.Count; ++index)
            {
                if (___bodyAnimators[index].Animator != null &&
                    !___bodyAnimators[index].Animator.GetBool(_propertyHash))
                {
                    // Only trigger if `fireEvents` is `true`
                    // Maybe there is a way to trigger the animations, but
                    // to not send the event in the end that causes damage!?
                    if (___bodyAnimators[index].Animator.fireEvents)
                        ___bodyAnimators[index].Animator.SetTrigger(_propertyHash);
                    ___changed = true;
                }
            }
            if (__instance.HeldItemAnimator != null && __instance.HeldItemAnimator.gameObject
                .activeInHierarchy && !__instance.HeldItemAnimator.GetBool(_propertyHash))
            {
                __instance.HeldItemAnimator.SetTrigger(_propertyHash);
                ___changed = true;
            }
            if (__instance.Entity.isEntityRemote || !___changed) return false;
            ___ChangedAnimationParameters[_propertyHash] = new AnimParamData(
                _propertyHash, AnimParamData.ValueTypes.Trigger, true);
            return false;
        }
    }

    // Conditional patch for Undad Legacy
    [HarmonyPatch]
    class UndeadLegacyPatch
    {
        private static bool Prepare()
        {
            return ModManager.ModLoaded("UndeadLegacy_CoreModule");
        }
        public static MethodBase TargetMethod()
        {
            var mod = ModManager.GetMod("UndeadLegacy_CoreModule");
            return AccessTools.FirstMethod(
                mod.MainAssembly.GetTypes().First(
                    (t) => t.Name == "ItemActionULM_Zoom"),
                method => method.Name == "startEndZoom");
        }
        public static void Postfix()
        {
            if (GameManager.Instance.World.GetPrimaryPlayer() is EntityPlayerLocal player)
                player.weaponCamera.cullingMask &= ~1024;
        }
    }

}