using System;

namespace UnityConsole
{
    public interface ICommandParser
    {
        Type TargetType { get; }
        object Parse(string input);
    }
}