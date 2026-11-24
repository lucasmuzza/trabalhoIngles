using System;
using System.Globalization;

namespace UnityConsole
{
    public class FloatParser : ICommandParser
    {
        public Type TargetType => typeof(float);

        public object Parse(string input) => float.Parse(input, CultureInfo.InvariantCulture);
    }
}


    