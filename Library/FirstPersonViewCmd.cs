using System.Collections.Generic;
using UnityEngine;

public class FirstPersonViewCmd : ConsoleCmdAbstract
{

    public override string[] GetCommands() => new string[1]
    {
        "fpv"
    };

    public override string GetDescription() => "First Person View";

    public override string GetHelp() => "n/a\n";

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        var player = GameManager.Instance.World.GetPrimaryPlayer();

        switch (_params.Count)
        {
            case 1:
                switch (_params[0])
                {
                    case "on":
                        FirstPersonView.Enable(true);
                        break;
                    case "off":
                        FirstPersonView.Enable(false);
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

    protected override string[] getCommands()
    {
        return new string[] {
            "on",
            "off",
            "pc",
            "gc",
            "layers",
            "pcl",
            "gcl",
        };
    }

    protected override string getDescription()
    {
        return "Mess with first person view settings";
    }

}
