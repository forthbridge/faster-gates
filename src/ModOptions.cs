using Menu.Remix.MixedUI;

namespace FasterGates;

public sealed class ModOptions : OptionsTemplate
{
    public static ModOptions Instance = new();

    public static void RegisterOI()
    {
        if (MachineConnector.GetRegisteredOI(Plugin.MOD_ID) != Instance)
        {
            MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Instance);
        }
    }


    // Configurables

    public static Configurable<int> gateSpeed = Instance.config.Bind("gateOpeningSpeed", 300, new ConfigurableInfo(
        "How quickly gates open relative to normal. 100% is normal." +
        "\nHigher is faster, lower is slower.",
        new ConfigAcceptableRange<int>(10, 1000), "", "Gate Opening Speed Multiplier"));

    public static Configurable<int> waitTime = Instance.config.Bind("waitTime", 100, new ConfigurableInfo(
        "How long gates wait to start opening relative to normal. 100% is normal." +
        "\nHigher is longer, lower is shorter.",
        new ConfigAcceptableRange<int>(10, 1000), "", "Gate Wait Time Multiplier"));

    public static Configurable<bool> instantGates = Instance.config.Bind("instantGates", false, new ConfigurableInfo(
        "Overrides the Gate Opening Speed Multiplier and makes gate opening effectively instant.",
        null, "", "Instant Gates?"));


    private const int NUMBER_OF_TABS = 1;

    public override void Initialize()
    {
        base.Initialize();

        Tabs = new OpTab[NUMBER_OF_TABS];
        int tabIndex = -1;

        AddTab(ref tabIndex, "General");

        AddSlider(gateSpeed, (string)gateSpeed.info.Tags[0], "10%", "1000%");
        AddSlider(waitTime, (string)waitTime.info.Tags[0], "10%", "1000%");
        DrawSliders(ref Tabs[tabIndex]);

        AddCheckBox(instantGates, (string)instantGates.info.Tags[0]);
        DrawCheckBoxes(ref Tabs[tabIndex]);

        AddNewLine(11);
        DrawBox(ref Tabs[tabIndex]);
    }
}