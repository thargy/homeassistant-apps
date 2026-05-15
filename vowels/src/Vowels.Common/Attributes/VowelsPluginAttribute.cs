namespace Vowels.Common.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class VowelsPluginAttribute : Attribute
{
    public string Name { get; }
    public string Version { get; }
    public bool AllowMultipleInstances { get; init; } = false;

    public VowelsPluginAttribute(string name, string version)
    {
        Name = name;
        Version = version;
    }
}
