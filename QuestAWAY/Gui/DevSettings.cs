using Dalamud.Bindings.ImGui;
using System.Diagnostics;
using System.Numerics;

namespace QuestAWAY.Gui;

internal class DevSettings
{
    internal static void Draw()
    {
        ImGui.Checkbox("[dev] Enable texture collecting", ref P.Collect);

        if(P.Collect)
        {
            if(ImGui.Button("Reset"))
            {
                P.TexSet.Clear();
            }

            ImGui.SameLine();
            ImGui.Checkbox("Display textures", ref P.CollectDisplay);

            if(P.CollectDisplay)
            {
                foreach(var e in P.TexSet)
                {
                    ImGuiDrawImage(e);
                    ImGui.SameLine();

                    if(!Static.MapIcons.Contains(e))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, 0xff0000ff);
                    }

                    if(ImGui.Button("Copy: " + e))
                    {
                        ImGui.SetClipboardText(e);
                    }

                    if(!Static.MapIcons.Contains(e))
                    {
                        ImGui.PopStyleColor();
                    }
                }
            }

            var s = string.Join("\n", P.TexSet);

            ImGui.InputTextMultiline("##QADATA", ref s, 1000000, new Vector2(300f, 100f));
        }

        ImGui.Separator();

        if(ImGui.Button("Clear hidden textures list" + (ImGui.GetIO().KeyCtrl ? "" : " (hold ctrl and click)")) && ImGui.GetIO().KeyCtrl)
        {
            P.Config.HiddenTextures.Clear();
            Utils.BuildHiddenByteSet();
        }

        ImGui.Checkbox("Profiling", ref P.Profiling);

        if(P.Profiling)
        {
            ImGui.Text("Total time: " + P.TotalTime);
            ImGui.Text("Total ticks: " + P.TotalTicks);
            ImGui.Text("Tick avg: " + (P.TotalTime / (float)P.TotalTicks));
            ImGui.Text("MS avg: " + (P.TotalTime / (float)P.TotalTicks / Stopwatch.Frequency * 1000) + " ms");

            if(ImGui.Button("Reset##SW"))
            {
                P.TotalTicks = 0;
                P.TotalTime = 0;
            }
        }
    }
}
