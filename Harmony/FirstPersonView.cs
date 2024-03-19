using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class FirstPersonView : IModApi
{

    public void InitMod(Mod mod)
    {
        Log.Out("OCB Harmony Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GamePrefs.Set(EnumGamePrefs.OptionsIntroMovieEnabled, false);
    }

    // Kept for reference on how to detect invocation
    // [HarmonyPatch(typeof(Behaviour), "set_enabled")]
    // static class AvatarMultiBodyControllerPatch
    // {
    //     static void Prefix(Object __instance, bool value)
    //     {
    //         if (!(__instance is Animator animator)) return;
    //         if (animator.name != "player_maleRagdoll") return;
    //         Log.Out("++++ SET TO {0} => {1}", animator, value);
    //         Log.Out(System.Environment.StackTrace);
    //     }
    // }

    private static bool Enabled = true;

    public static void Enable(bool on)
    {
        Enabled = on;
    }

    // Check if animator is third party animator
    static bool IsTpvAnimator(Animator animator) =>
        animator.name == "player_maleRagdoll" ||
        animator.name == "player_femaleRagdoll";

    // Ensure the third party animator is always enabled
    // Need the animations for the shadows of the body
    // But we do not want any events from the animations
    [HarmonyPatch(typeof(AvatarLocalPlayerController), "LateUpdate")]
    static class AvatarLocalPlayerControllerLateUpdatePatch
    {
        static void Postfix(bool ___isFPV, Animator ___anim)
        {
            // Only patch up third party animators
            if (!IsTpvAnimator(___anim)) return;
            // Fire only in third party view
            ___anim.fireEvents = !___isFPV;
            // Animator is always enabled
            ___anim.enabled = true;
        }
    }

    // Adjust player camera to fix a few issues from showing
    // the actual third party model (e.g. render close stuff)
    public static void UpdatePlayerCameraAndLights(EntityPlayerLocal player)
    {
        if (player.playerCamera == null) return;
        if (player.weaponCamera == null) return;
        if (Enabled)
        {
            // Also show holding hands model in the main camera
            player.playerCamera.cullingMask |= 1 << Constants.cLayerHoldingItem;
            player.weaponCamera.cullingMask &= ~(1 << Constants.cLayerHoldingItem);
            // Allow weapon to render closer
            // May also fix some other clipping
            player.playerCamera.nearClipPlane = 0.02f;
            // This fixes some light issues?
            // Note: probably no longer needed?
            // camera.cullingMask = 277087745;
            // Change shadow settings for e.g. headlamp to avoid some self-shadowing
            foreach (var light in player.transform.GetComponentsInChildren<Light>())
            {
                // Do not shine light on player model (avoid weird shadows)
                light.cullingMask &= ~(1 << Constants.cLayerLocalPlayer);
                light.cullingMask |= 1 << Constants.cLayerHoldingItem;
                // Check if these are still needed?
                light.shadowNearPlane = 0.55f;
                light.shadowBias = 0.25f;
            }
            // player.weaponCamera.enabled = false;
        }
        else
        {
            // Reset to the default values
            player.playerCamera.nearClipPlane = 0.095f;
            player.playerCamera.cullingMask &= ~(1 << Constants.cLayerHoldingItem);
            player.weaponCamera.cullingMask |= 1 << Constants.cLayerHoldingItem;
            // player.weaponCamera.enabled = true;
        }
    }

    [HarmonyPatch(typeof(AvatarLocalPlayerController), "SwitchModelAndView")]
    public class AvatarLocalPlayerControllerSwitchModelAndViewPatch
    {
        public static void Postfix(ref bool _bFPV,
            AvatarLocalPlayerController __instance)
        {

            // Update the cameras to show the correct layers
            if (GameManager.Instance.World.GetPrimaryPlayer() is EntityPlayerLocal player)
            {
                // Show character (hand) for player camera
                UpdatePlayerCameraAndLights(player);
            }

            // Setup the main character body
            if (__instance.CharacterBody is BodyAnimator bodyAnimator)
            {
                if (bodyAnimator.Parts.BodyObj.transform is Transform bodyTransform)
                {
                    // Force body to be always visible (force shadow only later)
                    if (Enabled) bodyAnimator.State = BodyAnimator.EnumState.Visible;
                    foreach (var body in bodyTransform.GetComponentsInChildren<Renderer>())
                    {
                        // Setup shadow casting for the main character body (either fully or just shadows)
                        if (!Enabled) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        else if (!_bFPV) body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        // The `ShadowsOnly` option will make the actual model "invisible"
                        else body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                }
            }

            // This has some interesting side effects ...
            //__instance.PrimaryBody = __instance.CharacterBody;
            /*
            // Abort now, nothing to revert from this point on
            if (Enabled == false || _bFPV == false) return;

            // Setup the "first person view" arms
            if (__instance.FPSArms is BodyAnimator fpsArmsAnimator)
            {
                fpsArmsAnimator.State = BodyAnimator.EnumState.Visible;
                if (fpsArmsAnimator.Parts.RightHandT is Transform handTransform)
                {
                    // Log.Out("Gotcha hand transform {0}", handTransform);
                    foreach (var hand in handTransform.GetComponentsInChildren<Renderer>())
                    {
                        // Enable shadow casting for the "first person view hand object"
                        // hand.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
                if (fpsArmsAnimator.Parts.BodyObj.transform is Transform bodyTransform)
                {
                    foreach (var arm in bodyTransform.GetComponentsInChildren<Renderer>())
                    {
                        // Disable shadow casting for the "first person view arms"
                        arm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
            */

        }
    }
    /*
    [HarmonyPatch(typeof(EModelBase), "SwitchModelAndView")]
    public class EModelBase_SwitchModelAndView
    {
        public static void Postfix(EModelBase __instance)
        {
            if (__instance.GetRightHandTransform() is Transform handTransform)
            {
                foreach (var hand in handTransform.GetComponentsInChildren<Renderer>())
                {
                    // Disable shadow casting for the "first person view hand object"
                    hand.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

            }
        }
    }
    */

    static Transform LastHeldItem = null;

    // Clone hold item and put it into the third party player hand
    [HarmonyPatch(typeof(AvatarLocalPlayerController), "SetInRightHand")]
    public class AvatarLocalPlayerController_SetInRightHand
    {

        static void Postfix(
            AvatarLocalPlayerController __instance,
            EntityAlive ___entity)
        {
            if (__instance.CharacterBody is BodyAnimator bodyAnimator)
            {
                if (___entity?.inventory?.GetHoldingItemTransform() is Transform hold)
                {
                    if (LastHeldItem != null)
                    {
                        LastHeldItem.gameObject.SetActive(false);
                        Object.Destroy(LastHeldItem.gameObject);
                        LastHeldItem = null;
                    }
                    ItemValue _itemValue = ___entity.inventory.holdingItemItemValue;
                    // ToDo: implement some caching to for all slot allocated items
                    Transform heldItem = ___entity.inventory.holdingItem.CloneModel(___entity.world, _itemValue,
                        ___entity.GetPosition(), ___entity.inventory.inactiveItems, BlockShape.MeshPurpose.Hold);
                    LastHeldItem = heldItem;
                    heldItem.parent = bodyAnimator.Parts.RightHandT;
                    heldItem.localPosition = Vector3.zero;
                    heldItem.localRotation = Quaternion.identity;
                    heldItem.SetChildLayer(Constants.cLayerHoldingItem);
                    heldItem.gameObject.layer = Constants.cLayerHoldingItem;
                    heldItem.gameObject.SetActive(true);
                    heldItem.name += "(Shadow)";
                    // Disable shadows for the hand held item in the fpsArms
                    foreach (var rendered in hold.GetComponentsInChildren<Renderer>(true))
                        rendered.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // Enable shadows for the hand held item on the avatar body 
                    foreach (var rendered in heldItem.GetComponentsInChildren<Renderer>(true))
                        rendered.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }

        }
    }

    /*
    [HarmonyPatch(typeof(EntityPlayerLocal), "ShowWeaponCamera")]
    public class EntityPlayerLocal_ShowWeaponCamera
    {
        public static void Postfix(EntityPlayerLocal __instance, bool show)
        {
            if (Enabled == false) return;
            Camera camera = __instance.vp_FPCamera.transform.
                FindInChilds("WeaponCamera").GetComponent<Camera>();
            // Weapons Camera never shows the character/hand
            if (camera != null) camera.cullingMask &= -1025;
            // if (camera != null) camera.enabled = false;
            // Disable duplicated? light for torches (and similar objects)
            // Not exactly sure why this works, but tests seems to show it does
            if (__instance.vp_FPCamera?.transform.FindInChilds("Light_FirstPerson") is Transform lfp)
            {
                foreach (var light in lfp.GetComponentsInChildren<LightLOD>())
                {
                    light.GetLight().enabled = false;
                    light.enabled = false;
                }
            }
        }
    }
    */

    // ####################################################################
    // Ensure to adjust culling masks for the cameras when changed
    // ####################################################################

    [HarmonyPatch(typeof(ItemActionZoom), "startEndZoom")]
    public class ItemActionZoomStartEndZoomPatch
    {
        public static void Postfix(ItemActionData _actionData)
        {
            if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
                UpdatePlayerCameraAndLights(player);
        }
    }

    [HarmonyPatch(typeof(ItemActionZoom), "OnScreenOverlay")]
    public class ItemActionZoomOnScreenOverlayPatch
    {
        public static void Postfix(ItemActionData _actionData)
        {
            if (_actionData.invData.holdingEntity is EntityPlayerLocal player)
                UpdatePlayerCameraAndLights(player);
        }
    }

    [HarmonyPatch(typeof(EntityPlayerLocal), "ShowWeaponCamera")]
    public class EntityPlayerLocal_ShowWeaponCamera
    {
        public static void Postfix(EntityPlayerLocal __instance)
        {
            UpdatePlayerCameraAndLights(__instance);
        }
    }

    // Ensure we obey disabled events for animators
    // As we disable events on the third party model
    [HarmonyPatch(typeof(AnimatorMeleeAttackState), "OnStateEnter")]
    public class AnimatorMeleeAttackState_OnStateEnter
    {
        public static bool Prefix(Animator animator)
        {
            return animator?.fireEvents ?? false;
        }
    }

    // Conditional patch for Undead Legacy
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
                Assembly.GetExecutingAssembly().GetTypes().First(
                    (t) => t.Name == "ItemActionULM_Zoom"),
                method => method.Name == "startEndZoom");
        }
        public static void Postfix()
        {
            if (GameManager.Instance.World.GetPrimaryPlayer() is EntityPlayerLocal player)
                UpdatePlayerCameraAndLights(player);
        }
    }

}