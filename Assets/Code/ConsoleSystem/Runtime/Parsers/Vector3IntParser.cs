using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class Vector3IntParser : ICommandParser
    {
        public Type TargetType => typeof(Vector3Int);

        public object Parse(string input)
        {
            var parts = input.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new Vector3Int(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        }
    }
}