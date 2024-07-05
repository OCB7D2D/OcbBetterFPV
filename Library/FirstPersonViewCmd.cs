using System;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonViewCmd : ConsoleCmdAbstract
{

    public override string[] getCommands() => new string[1]
    {
        "fpv"
    };

    public override string getDescription() => "Mess with first person view settings";

    // protected override string getHelp() => "n/a\n";

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        var player = GameManager.Instance.World.GetPrimaryPlayer();

        switch (_params.Count)
        {
            case 1:
                switch (_params[0])
                {
                    case "on":
                        //FirstPersonView.Enable(true);
                        break;
                    case "off":
                        //FirstPersonView.Enable(false);
                        break;
                    case "dbg":
                        DebugAvatarAndCams(player);
                        break;
                    case "pc":
                        if (player == null) break;
                        // Show character (hand) for player camera
                        Log.Out("cullingMask: {0}", player.playerCamera.cullingMask);
                        break;
                    case "gc":
                        if (player == null) break;
                        // Show character (hand) for player camera
                        Log.Out("cullingMask: {0}", player.weaponCamera.cullingMask);
                        break;
                    case "layers":
                        for (int i = 0; i < 32; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(LayerMask.LayerToName(i)))
                                Log.Out("{0}: {1} ({2})", i, LayerMask.LayerToName(i),
                                    LayerMask.GetMask(LayerMask.LayerToName(i)));
                        }
                        break;
                    default:
                        Log.Warning("Unknown command {0}", _params[0]);
                        break;
                }
                break;
            case 2:
                switch (_params[0])
                {
                    case "pcl":
                        if (player == null) break;
                        int pcl = int.Parse(_params[1]);
                        if (pcl < 0) player.playerCamera.cullingMask &= ~(-pcl);
                        else if (pcl > 0) player.playerCamera.cullingMask |= pcl;
                        else player.playerCamera.cullingMask = 0;
                        break;
                    case "gcl":
                        if (player == null) break;
                        int gcl = int.Parse(_params[1]);
                        if (gcl < 0) player.weaponCamera.cullingMask &= ~(-gcl);
                        else if (gcl > 0) player.weaponCamera.cullingMask |= gcl;
                        else player.weaponCamera.cullingMask = 0;
                        break;
                    default:
                        break;
                }
                break;
            default:
                Log.Warning("No arguments given for fpv command");
                break;
        }

    }

    private void DebugTransform(Transform transform, string ind = "")
    {
        Log.Out(ind + "  {0}", transform);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            DebugTransform(child, ind + "  ");
        }
    }

    private void DebugAvatarAndCams(EntityPlayerLocal player)
    {
        Log.Out("player cam cullingMask: 1024: {0}", player.playerCamera.cullingMask & 1024);
        Log.Out("weapon cam cullingMask: 1024: {0}", player.weaponCamera.cullingMask & 1024);
        DebugTransform(player.emodel.transform);
        if (player.emodel.avatarController is AvatarController avatar)
        {
            Log.Out("Got an Avatar {0}", avatar);
            if (avatar is AvatarCharacterController character)
            {
                Log.Out("Is character controller {0} (enabled {1})", character, character.enabled);
            }

            if (avatar is AvatarLocalPlayerController local)
            {
                Log.Out("Is local Avatar {0} (enabled {1})", local, local.enabled);
                Log.Out("Found character body animator {0} (enabled {1})",
                    local.characterBody, local.characterBody.state);
                local.enabled = true;
                if (local.FPSArms is BodyAnimator arms)
                {
                    Log.Out(" found arms animator {0} => {1} (enabled {2})",
                        arms, arms.state, arms.Animator.enabled);
                }
                foreach (var animator in local.BodyAnimators)
                {
                    if (animator is BodyAnimator body)
                    {
                        Log.Out(" found body animator {0} => {1} (enabled {2})",
                            body, body.state, body.Animator.enabled);
                        body.Animator.enabled = true;
                    }
                }
                foreach (var renderer in local.transform.GetComponentsInChildren<Renderer>(true))
                {
                    Log.Out("  => {0}", renderer);
                    renderer.enabled = true;
                }
            }
        }

    }
}
