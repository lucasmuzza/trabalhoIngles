using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class IntParser : ICommandParser
    {
        public Type TargetType => typeof(int);

        public object Parse(string input) => int.Parse(input);
    }

    
}