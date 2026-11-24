using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class DoubleParser : ICommandParser
    {
        public Type TargetType => typeof(double);

        public object Parse(string input) => double.Parse(input, CultureInfo.InvariantCulture);
    }
}