using Dalamud.Utility;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using SomethingNeedDoing.Core.Interfaces;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace SomethingNeedDoing.LuaMacro.Wrappers;
public unsafe class AddonWrapper(string name) : IWrapper
{
    private AtkUnitBase* Addon => (AtkUnitBase*)Svc.GameGui.GetAddonByName(name).Address;
    private Pointer<AtkResNode>[] NodeList => Addon->UldManager.Nodes.ToArray();
    private AtkValue[] AtkValuesList => Addon->AtkValuesSpan.ToArray();

    [LuaDocs(description: "Check if the Addon Exists, regardless of visibility.")] public bool Exists => Addon != null;
    [LuaDocs(description: "Check if the Addon is Visible and Ready.")]
    public bool Ready
    {
        get
        {
            var addon = Addon;
            return addon != null && IsAddonReady(addon);
        }
    }

    [LuaDocs] public AtkValueWrapper GetAtkValue(int index) => new(Addon->AtkValues[index]);

    [LuaDocs]
    public AtkValueWrapper[] AtkValues
    {
        get
        {
            return AtkValuesList.Select(v => new AtkValueWrapper(v)).ToArray();
        }
    }

    [LuaDocs] public NodeWrapper GetNode(params int[] nodeIds) => new(Addon, nodeIds);

    [LuaDocs]
    public NodeWrapper[] Nodes // 1. 返回类型改为数组 []
    {
        get
        {
            return NodeList.Select(v => new NodeWrapper(v)).ToArray();
        }
    }

    [LuaDocs]
    public void Fire(params object[] values)
    {
        var addonPtr = Addon;

        if (addonPtr == null)
        {
            PluginLog.Warning($"[InputNumeric] Addon '{name}' not found.");
            return;
        }

        if (!addonPtr->IsVisible)
        {
            PluginLog.Warning($"[InputNumeric] Addon '{name}' is not visible. Skipping callback.");
            return;
        }

        try
        {
            object[] safeValues = [.. values.Select(v => v is long l ? (int)l : v)];

            Callback.Fire(addonPtr, true, safeValues);

            PluginLog.Debug($"[InputNumeric] Callback fired successfully for '{name}'.");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"[InputNumeric] Error firing callback for '{name}'. Types: [{string.Join(", ", values.Select(x => x.GetType().Name))}]\nException: {ex}");
        }
    }
}

public unsafe class NodeWrapper : IWrapper
{
    public NodeWrapper(AtkUnitBase* addon, params int[] nodeIds) => Node = GetNodeByIDChain(addon->RootNode, nodeIds);
    public NodeWrapper(Pointer<AtkResNode> node) => Node = node.Value;
    private AtkResNode* Node { get; set; }

    [LuaDocs] public uint Id => Node->NodeId;
    [LuaDocs] public bool IsVisible => Node->IsVisible();
    [LuaDocs] public string Text { get => Node->GetAsAtkTextNode()->NodeText.GetText(); set => Node->GetAsAtkTextNode()->NodeText.SetString(value); }
    [LuaDocs] public NodeType NodeType => Node->Type;
}

public class AtkValueWrapper(AtkValue value) : IWrapper
{
    private AtkValue Value = value;

    [LuaDocs] public string ValueString => Value.Type is ValueType.String ? Value.String.AsReadOnlySeStringSpan().ToString() : Value.GetValueAsString();

}
