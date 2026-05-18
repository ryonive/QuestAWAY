using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Logging;
using ECommons.Schedulers;
using ECommons.Singletons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using QuestAWAY.Gui;
using QuestAWAY.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuestAWAY;

internal unsafe class QuestAWAY : IDalamudPlugin
{
    public string Name => "QuestAWAY";
    internal static QuestAWAY P;
    internal bool Collect = false;
    internal bool CollectDisplay = false;
    internal HashSet<string> TexSet = [];
    internal HashSet<string> UserDefinedTextureSet = [];
    internal Vector2 QuickMenuPos = new(0f, 0f);
    internal Vector2 QuickMenuSize = Vector2.Zero;
    internal Configuration Config;
    internal static Vector2 Vector2Scale = new(48f, 48f);
    internal bool OnlySelected = false;
    internal bool OpenQuickEnable = false;
    internal byte[][] CfgHideSet = { };
    internal volatile bool ReprocessNaviMap = true;
    internal volatile bool ReprocessAreaMap = true;
    internal long Tick = 0;
    internal bool Profiling = false;
    internal long TotalTime;
    internal long TotalTicks;
    internal byte[][] FateTexture;
    internal byte[] AreaMarkerTexture;
    internal Category SelectedCategory = Category.All;
    internal Stopwatch Stopwatch;
    internal WindowSystem WindowSystem;
    internal ConfigGui ConfigGui;

    public static Configuration CurrentProfile;

    public void Dispose()
    {
        Safe(() =>
        {
            Svc.Framework.Update -= OnUpdate;
            Svc.PluginInterface.UiBuilder.Draw -= Draw;
            Svc.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
            Svc.ClientState.TerritoryChanged -= ClientState_TerritoryChanged;
        });
        Safe(Config.Save);
        Safe(() =>
        {
            CurrentProfile.Enabled = false;
            ReprocessNaviMap = true;
            ReprocessAreaMap = true;
            Utils.ProcessMinimap((AtkUnitBase*)Svc.GameGui.GetAddonByName("_NaviMap", 1).Address);
            Utils.ProcessAreaMap((AtkUnitBase*)Svc.GameGui.GetAddonByName("AreaMap", 1).Address);

            Svc.Commands.RemoveHandler("/questaway");
        });
        ECommonsMain.Dispose();

        P = null;
    }

    public QuestAWAY(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        P = this;

        _ = new TickScheduler(delegate
        {
            WindowSystem = new();
            ConfigGui = new();

            WindowSystem.AddWindow(ConfigGui);

            Config = pluginInterface.GetPluginConfig() as Configuration ?? new();
            Static.PaddingVector = ImGui.GetStyle().WindowPadding;
            SingletonServiceManager.Initialize(typeof(ServiceManager));
            Svc.Framework.Update += OnUpdate;
            Svc.PluginInterface.UiBuilder.Draw += Draw;
            Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { ConfigGui.IsOpen = true; };
            Stopwatch = new Stopwatch();
            Svc.Commands.AddHandler("/questaway", new CommandInfo(delegate { ConfigGui.IsOpen = !ConfigGui.IsOpen; })
            {
                HelpMessage = "open/close configuration"
            });
            Svc.ClientState.TerritoryChanged += ClientState_TerritoryChanged;
            ClientState_TerritoryChanged(Svc.ClientState.TerritoryType);
        });
    }

    internal void ClientState_TerritoryChanged(uint e)
    {
        if(!Config.ZoneSettings.TryGetValue(e, out CurrentProfile))
        {
            CurrentProfile = Config;
        }

        ApplyMemoryReplacer();
        Utils.BuildHiddenByteSet();
    }

    internal static void ApplyMemoryReplacer()
    {
        if(CurrentProfile.Enabled && CurrentProfile.AetheryteInFront)
        {
            if(!S.Memory.AreaMapCtrlInvertPatch.Enabled)
            {
                S.Memory.AreaMapCtrlInvertPatch.Enable();
            }
        }
        else
        {
            if(S.Memory.AreaMapCtrlInvertPatch.Enabled)
            {
                S.Memory.AreaMapCtrlInvertPatch.Disable();
            }
        }
    }

    private void Draw()
    {
        if(OpenQuickEnable)
        {
            var b = true;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(QuickMenuPos);
            ImGui.Begin("QuestAWAY quick enable", ref b,
                ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoFocusOnAppearing
                );

            if(Static.ImGuiToggleButton(FontAwesomeIcon.ExclamationCircle, (CurrentProfile.Enabled ? "Disable" : "Enable") + " QuestAWAY", ref CurrentProfile.Enabled))
            {
                ReprocessAreaMap = true;
                ReprocessNaviMap = true;

                ApplyMemoryReplacer();
            }

            ImGui.SameLine();

            if(Static.ImGuiIconButton(FontAwesomeIcon.Cog, "QuestAWAY settings"))
            {
                ConfigGui.IsOpen = true;
            }

            QuickMenuSize = ImGui.GetWindowSize();

            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }

    internal void ProfilingContinue()
    {
        if(!Profiling)
        {
            return;
        }

        Stopwatch.Start();
    }

    internal void ProfilingPause()
    {
        if(!Profiling)
        {
            return;
        }

        Stopwatch.Stop();
    }

    private void ProfilingRestart()
    {
        if(!Profiling)
        {
            return;
        }

        TotalTime += Stopwatch.ElapsedTicks + 1;
        TotalTicks++;

        Stopwatch.Restart();
    }

    public void OnUpdate(object _)
    {
        try
        {
            ProfilingRestart();

            var o = Svc.GameGui.GetAddonByName("AreaMap", 1);

            if(o != IntPtr.Zero)
            {
                var masterWindow = (AtkUnitBase*)o.Address;

                if(masterWindow->IsVisible && masterWindow->UldManager.NodeListCount > 3)
                {
                    var mapCNode = (AtkComponentNode*)masterWindow->UldManager.NodeList[3];
                    OpenQuickEnable = CurrentProfile.QuickEnable;

                    if(OpenQuickEnable)
                    {
                        QuickMenuPos.X = masterWindow->X + (mapCNode->AtkResNode.X * masterWindow->Scale) + (mapCNode->AtkResNode.Width * masterWindow->Scale / 2) - (QuickMenuSize.X / 2);
                        QuickMenuPos.Y = masterWindow->Y + (mapCNode->AtkResNode.Y * masterWindow->Scale) - QuickMenuSize.Y;
                    }
                }
                else
                {
                    OpenQuickEnable = false;
                }
            }
            else
            {
                OpenQuickEnable = false;
            }
            ProfilingPause();
        }
        catch(Exception e)
        {
            PluginLog.Error("=== Error during QuestAway plugin execution ===" + e.Message + "\n" + e.StackTrace);
            Svc.Chat.Print("[QuestAway] An error occurred, please send your log to developer.");
        }
    }
}
