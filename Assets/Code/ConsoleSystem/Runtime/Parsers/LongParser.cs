using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class LongParser : ICommandParser
    {
        public Type TargetType => typeof(long);

        public object Parse(string input) => long.Parse(input);
    }
}