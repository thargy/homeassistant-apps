namespace Vowels.Common.Attributes;

/// <summary>
/// Assembly-level marker indicating that this assembly contains one or more Vowels plugins.
/// PluginManager uses this attribute to skip assemblies that have no plugins without
/// paying the cost of a full type scan.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class VowelsPluginAssemblyAttribute : Attribute { }
