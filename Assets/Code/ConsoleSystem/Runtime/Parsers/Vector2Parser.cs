using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class Vector2Parser : ICommandParser
    {
        public Type TargetType => typeof(Vector2);

        public object Parse(string input)
        {
            var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new Vector2(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture)
            );
        }
    }
}