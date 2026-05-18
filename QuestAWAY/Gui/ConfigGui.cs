using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.Funding;
using ECommons.ImGuiMethods;
using System.Numerics;

namespace QuestAWAY.Gui;

internal class ConfigGui : Window
{
    public ConfigGui() : base("QuestAWAY configuration")
    { }

    public override void OnClose()
    {
        base.OnClose();
        P.Config.Save();
    }

    public override void PreDraw()
    {
        base.PreDraw();
        ImGui.SetNextWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        P.ReprocessAreaMap = true;
        P.ReprocessNaviMap = true;
        PatreonBanner.DrawRight();
        ImGuiEx.EzTabBar("questaway", PatreonBanner.Text,
            ("Global settings", MainSettings.Draw, null, true),
            ("Per-zone settings", ZoneSettings.Draw, null, true),
            ("Developer functions", DevSettings.Draw, null, true)
            );
    }
}
