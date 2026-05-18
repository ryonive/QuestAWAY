using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace QuestAWAY;

public unsafe class Memory : IDisposable
{
    private delegate nint AreaMapOnMouseMoveDelegate(nint unk1, nint unk2);
    private delegate nint NaviMapOnMouseMoveDelegate(nint unk1, nint unk2, nint unk3, nint unk4, nint unk5);

    private Hook<NaviMapOnMouseMoveDelegate> NaviMapOnMouseMoveHook;
    private Hook<AtkResNode.Delegates.CheckCollisionAtCoords> CheckAtkCollisionNodeIntersectHook;
    private Hook<AreaMapOnMouseMoveDelegate> AreaMapOnMouseMoveHook;

    public EzPatch AreaMapCtrlInvertPatch = new("0F 95 C0 3A 85 ?? ?? ?? ??", 1, new("94"), false);

    private Memory()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "AreaMap", AddonAreaMapOnUpdateDetour);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "_NaviMap", AddonNaviMapOnUpdateDetour);

        NaviMapOnMouseMoveHook = Svc.Hook.HookFromSignature<NaviMapOnMouseMoveDelegate>("48 89 5C 24 ?? 57 48 83 EC 40 48 8B F9 0F B7 CA", NaviMapOnMouseMoveDetour);
        CheckAtkCollisionNodeIntersectHook = Svc.Hook.HookFromAddress<AtkResNode.Delegates.CheckCollisionAtCoords>(AtkResNode.Addresses.CheckCollisionAtCoords.Value, CheckAtkCollisionNodeIntersectDetour);
        AreaMapOnMouseMoveHook = Svc.Hook.HookFromSignature<AreaMapOnMouseMoveDelegate>("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 1E", AreaMapOnMouseMoveDetour);

        NaviMapOnMouseMoveHook.Enable();
        AreaMapOnMouseMoveHook.Enable();
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, "AreaMap", AddonAreaMapOnUpdateDetour);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, "_NaviMap", AddonNaviMapOnUpdateDetour);
        AreaMapCtrlInvertPatch?.Dispose();
        NaviMapOnMouseMoveHook?.Dispose();
        AreaMapOnMouseMoveHook?.Dispose();
        CheckAtkCollisionNodeIntersectHook?.Dispose(); 
    }


    private bool CheckAtkCollisionNodeIntersectDetour(AtkResNode* node, short x, short y, bool inclusive)
    {
        if(node != null && node->ParentNode != null)
        {
            // make intersection check fail if the map (both of them) icon is transparent
            return node->ParentNode->Color.A != 0 && CheckAtkCollisionNodeIntersectHook.Original(node, x, y, inclusive);
        }

        return CheckAtkCollisionNodeIntersectHook.Original(node, x, y, inclusive);
    }


    private nint AreaMapOnMouseMoveDetour(nint unk1, nint unk2)
    {
        // optimization: only hook CheckAtkCollisionNodeIntersect when mouseovering the AreaMap
        CheckAtkCollisionNodeIntersectHook.Enable();

        var result = AreaMapOnMouseMoveHook.Original(unk1, unk2);

        CheckAtkCollisionNodeIntersectHook.Disable();

        return result;
    }


    private nint NaviMapOnMouseMoveDetour(nint unk1, nint unk2, nint unk3, nint unk4, nint unk5)
    {
        // optimization: only hook CheckAtkCollisionNodeIntersect when mouseovering the minimap
        CheckAtkCollisionNodeIntersectHook.Enable();

        var result = NaviMapOnMouseMoveHook.Original(unk1, unk2, unk3, unk4, unk5);

        CheckAtkCollisionNodeIntersectHook.Disable();

        return result;
    }


    private void AddonNaviMapOnUpdateDetour(AddonEvent type, AddonArgs args)
    {
        // runs every frame (i think) if the minimap is visible
        P.ProfilingContinue();
        Utils.ProcessMinimap((AtkUnitBase*)args.Addon.Address);
        P.ProfilingPause();
    }

    private void AddonAreaMapOnUpdateDetour(AddonEvent type, AddonArgs args)
    {
        P.ProfilingContinue();
        Utils.ProcessAreaMap((AtkUnitBase*)args.Addon.Address);
        P.ProfilingPause();
    }
}
