using System;

namespace UnityConsole
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ConsoleContainerAttribute : Attribute
    {
        public string Prefix { get; }
        public ConsoleContainerAttribute(string prefix) => Prefix = prefix;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string HexColor { get; set; }

        public ConsoleCommandAttribute(string name = null, string description = "")
        {
            Name = name;
            Description = description;
        }
    }
}