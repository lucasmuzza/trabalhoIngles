using System;
using System.Globalization;
using UnityEngine;

namespace UnityConsole
{
    public class Vector3Parser : ICommandParser
    {
        public Type TargetType => typeof(Vector3);

        public object Parse(string input)
        {
            var parts = input.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            return new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture)
            );
        }
    }
}