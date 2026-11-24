using System;

namespace UnityConsole
{
    public struct SourceGeneratedCommand
    {
        public string CommandName { get; }
        public string Description { get; }
        public string HexColor { get; }
        public bool IsStatic { get; }
        public Type ClassType { get; }
        public string MethodName { get; }
        public Type[] ParameterTypes { get; }

        public SourceGeneratedCommand(string commandName, string description, string hexColor, bool isStatic, Type classType, string methodName, Type[] parameterTypes)
        {
            CommandName = commandName;
            Description = description;
            HexColor = hexColor;
            IsStatic = isStatic;
            ClassType = classType;
            MethodName = methodName;
            ParameterTypes = parameterTypes;
        }
    }
}