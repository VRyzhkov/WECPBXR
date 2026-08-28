using WECPBXR.Core.Models;

namespace WECPBXR.Core.Mapping;

public static class ControllerProfileCatalog
{
    public const string DefaultProfileId = "wecpbxr-default";
    public const string AkaiMidimixProfileId = "akai-midimix";

    private const int MidiChannel = 1;

    private static readonly ControllerProfile[] Profiles =
    [
        CreateDefaultProfile(),
        CreateAkaiMidimixProfile()
    ];

    public static IReadOnlyList<ControllerProfile> All => Profiles;

    public static ControllerProfile Default => Profiles[0];

    public static ControllerProfile GetOrDefault(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Default;
        }

        return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }

    private static ControllerProfile CreateDefaultProfile()
    {
        List<ControllerControlDefinition> controls = [];

        AddKnobs(controls, left: 185, top: 78, columnStep: 92, rowStep: 100, width: 76, height: 104);
        AddFaders(controls, masterLeft: 82, firstChannelLeft: 184, top: 470, columnStep: 92, width: 78, height: 178);
        AddButton(controls, "bank-prev", "BANK L", 34, 85, 203, 72, 38);
        AddButton(controls, "bank-next", "BANK R", 35, 85, 303, 72, 38);
        AddButton(controls, "solo", "SOLO", 36, 85, 374, 72, 38);
        AddButton(controls, "send-all", "SEND ALL", 37, 85, 426, 72, 38);
        AddMatrixButtons(controls, left: 188, top: 374, columnStep: 92, rowStep: 52, width: 72, height: 38);

        return new ControllerProfile(DefaultProfileId, "WECPBXR default controller", 960, 724, controls);
    }

    private static ControllerProfile CreateAkaiMidimixProfile()
    {
        List<ControllerControlDefinition> controls = [];

        AddKnobs(controls, left: 80, top: 64, columnStep: 108, rowStep: 103, width: 76, height: 104);
        AddFaders(controls, masterLeft: 888, firstChannelLeft: 70, top: 490, columnStep: 109, width: 78, height: 178);
        AddButton(controls, "send-all", "SEND ALL", 37, 878, 70, 72, 38);
        AddButton(controls, "bank-prev", "BANK L", 34, 878, 180, 72, 38);
        AddButton(controls, "bank-next", "BANK R", 35, 878, 290, 72, 38);
        AddButton(controls, "solo", "SOLO", 36, 878, 390, 72, 38);
        AddMatrixButtons(controls, left: 75, top: 385, columnStep: 109, rowStep: 52, width: 72, height: 38);

        return new ControllerProfile(AkaiMidimixProfileId, "AKAI MIDImix", 960, 724, controls);
    }

    private static void AddKnobs(
        List<ControllerControlDefinition> controls,
        int left,
        int top,
        int columnStep,
        int rowStep,
        int width,
        int height)
    {
        for (int i = 1; i <= 24; i++)
        {
            int column = (i - 1) % 8;
            int row = (i - 1) / 8;
            controls.Add(new ControllerControlDefinition(
                Id("knob", i),
                $"Knob {i}",
                ControlKind.Knob,
                new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, i),
                left + column * columnStep,
                top + row * rowStep,
                width,
                height));
        }
    }

    private static void AddFaders(
        List<ControllerControlDefinition> controls,
        int masterLeft,
        int firstChannelLeft,
        int top,
        int columnStep,
        int width,
        int height)
    {
        for (int i = 1; i <= 9; i++)
        {
            int left = i == 1
                ? masterLeft
                : firstChannelLeft + (i - 2) * columnStep;

            controls.Add(new ControllerControlDefinition(
                Id("fader", i),
                $"Fader {i}",
                ControlKind.Fader,
                new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, 24 + i),
                left,
                top,
                width,
                height));
        }
    }

    private static void AddMatrixButtons(
        List<ControllerControlDefinition> controls,
        int left,
        int top,
        int columnStep,
        int rowStep,
        int width,
        int height)
    {
        for (int i = 1; i <= 16; i++)
        {
            int column = (i - 1) % 8;
            int row = (i - 1) / 8;
            AddButton(
                controls,
                Id("button", i),
                $"Button {i}",
                37 + i,
                left + column * columnStep,
                top + row * rowStep,
                width,
                height);
        }
    }

    private static void AddButton(
        List<ControllerControlDefinition> controls,
        string id,
        string label,
        int midiNumber,
        int left,
        int top,
        int width,
        int height)
    {
        controls.Add(new ControllerControlDefinition(
            id,
            label,
            ControlKind.Button,
            new MidiBinding(MidiMessageKind.ControlChange, MidiChannel, midiNumber),
            left,
            top,
            width,
            height));
    }

    private static string Id(string prefix, int number)
    {
        return $"{prefix}-{number:00}";
    }
}
