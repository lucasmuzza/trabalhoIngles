using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class Vector2IntParser : ICommandParser
    {
        public Type TargetType => typeof(Vector2Int);

        public object Parse(string input)
        {
            var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }
    }
}