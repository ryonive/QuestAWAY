using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TerraFX.Interop.Windows;

namespace QuestAWAY;

public static unsafe class Utils
{
    internal static void ProcessAreaMap(AtkUnitBase* masterWindow)
    {
        if((!CurrentProfile.Enabled || !CurrentProfile.Bigmap) && !P.ReprocessAreaMap)
        {
            return;
        }

        if(masterWindow != null
            && masterWindow->IsVisible
            && masterWindow->UldManager.NodeListCount > 3)
        {
            var mapCNode = (AtkComponentNode*)masterWindow->UldManager.NodeList[3];

            for(var i = 6; i < mapCNode->Component->UldManager.NodeListCount; i++)
            {
                ProcessShit((AtkComponentNode*)mapCNode->Component->UldManager.NodeList[i], !(CurrentProfile.Enabled && CurrentProfile.Bigmap), true);
            }

            P.ReprocessAreaMap = false;
        }
    }

    internal static void ProcessMinimap(AtkUnitBase* masterWindow)
    {
        if((!CurrentProfile.Enabled || !CurrentProfile.Minimap) && !P.ReprocessNaviMap)
        {
            return;
        }

        if(masterWindow != null
            && masterWindow->IsVisible
            && masterWindow->UldManager.NodeListCount > 2)
        {
            var mapCNode = (AtkComponentNode*)masterWindow->UldManager.NodeList[2];

            for(var i = 4; i < mapCNode->Component->UldManager.NodeListCount; i++)
            {
                ProcessShit((AtkComponentNode*)mapCNode->Component->UldManager.NodeList[i],
                    !(CurrentProfile.Enabled && CurrentProfile.Minimap));
            }

            P.ReprocessNaviMap = false;
        }
    }

    internal static void ProcessShit(AtkComponentNode* mapIconNode, bool showUnconditionally = false, bool isAreaMap = false)
    {
        if(!mapIconNode->AtkResNode.IsVisible())
        {
            return;
        }

        if(mapIconNode->Component->UldManager.NodeListCount <= 4)
        {
            return;
        }

        AtkImageNode* imageNode;

        if(mapIconNode->Component->UldManager.NodeList[4]->Type == NodeType.Image)
        {
            imageNode = (AtkImageNode*)mapIconNode->Component->UldManager.NodeList[4];
        }
        else if(mapIconNode->Component->UldManager.NodeList[3]->Type == NodeType.Image)
        {
            imageNode = (AtkImageNode*)mapIconNode->Component->UldManager.NodeList[3];
        }
        else
        {
            return;
        }

        var textureInfo = imageNode->PartsList->Parts[imageNode->PartId].UldAsset;

        if(textureInfo->AtkTexture.TextureType == TextureType.Resource)
        {
            if(P.Collect)
            {
                P.TexSet.Add(Marshal.PtrToStringAnsi((IntPtr)textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName.BufferPtr).Replace(".tex", "").Replace("_hr1", ""));
            }

            var fNamePtr = textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName.BufferPtr;
            if(!showUnconditionally)
            {
                /*if(StartsWithAny(fNamePtr, fateTexture))
                {
                    PluginLog.Information($"{imageNode->AtkResNode.AddBlue}, {imageNode->AtkResNode.AddRed}, {imageNode->AtkResNode.AddGreen}, {imageNode->AtkResNode.MultiplyBlue}, {imageNode->AtkResNode.MultiplyRed}, {imageNode->AtkResNode.MultiplyGreen}, ");
                }*/
                if(
                    StartsWithAny(fNamePtr, P.CfgHideSet)
                    ||
                    (CurrentProfile.HideFateCircles && StartsWithAny(fNamePtr, P.FateTexture)
                    && imageNode->AtkResNode.AddBlue == 128 && imageNode->AtkResNode.AddGreen == 48 && imageNode->AtkResNode.MultiplyBlue == 100 && imageNode->AtkResNode.MultiplyGreen == 60)
                    )
                {
                    if(mapIconNode->AtkResNode.Color.A != 0)
                    {
                        mapIconNode->AtkResNode.Color.A = 0;
                    }
                }
                else
                {
                    if(mapIconNode->AtkResNode.Color.A == 0)
                    {
                        mapIconNode->AtkResNode.Color.A = 0xff;
                    }
                }

                if(isAreaMap && (CurrentProfile.HideAreaMarkers || P.ReprocessAreaMap) && StartsWith(fNamePtr, P.AreaMarkerTexture))
                {
                    if(CurrentProfile.HideAreaMarkers)
                    {
                        if(imageNode->AtkResNode.Color.A != 0)
                        {
                            imageNode->AtkResNode.Color.A = 0;
                        }
                    }
                    else
                    {
                        if(imageNode->AtkResNode.Color.A == 0)
                        {
                            imageNode->AtkResNode.Color.A = 0xff;
                        }
                    }
                }
            }
            else
            {
                if(mapIconNode->AtkResNode.Color.A == 0)
                {
                    mapIconNode->AtkResNode.Color.A = 0xff;
                }

                if(StartsWith(fNamePtr, P.AreaMarkerTexture) && imageNode->AtkResNode.Color.A == 0)
                {
                    imageNode->AtkResNode.Color.A = 0xff;
                }
            }
        }
    }

    internal static bool StartsWithAny(byte* sPtr, byte[][] set)
    {
        foreach(var b in set)
        {
            if(StartsWith(sPtr, b))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool StartsWith(byte* sPtr, byte[] b)
    {
        for(var i = 0; i < b.Length; i++)
        {
            if(*(sPtr + i) != b[i])
            {
                break;
            }

            if(i == b.Length - 1)
            {
                return true;
            }
        }

        return false;
    }

    internal static void BuildHiddenByteSet()
    {
        P.FateTexture = new byte[][] { Encoding.ASCII.GetBytes("ui/icon/060000/060496"), Encoding.ASCII.GetBytes("ui/icon/060000/060495") };
        P.AreaMarkerTexture = Encoding.ASCII.GetBytes("ui/icon/060000/060442");
        var userLines = CurrentProfile.CustomPathes.Split('\n');

        for(var n = 0; n < userLines.Length; n++)
        {
            userLines[n] = userLines[n].Trim();
        }

        var userLinesSet = userLines.ToHashSet();

        userLinesSet.RemoveWhere((string line) => { return line.Length == 0; });
        userLinesSet.UnionWith(CurrentProfile.HiddenTextures);

        P.CfgHideSet = new byte[userLinesSet.Count][];
        var i = 0;

        foreach(var e in userLinesSet)
        {
            P.CfgHideSet[i++] = Encoding.ASCII.GetBytes(e);
        }
    }
}
