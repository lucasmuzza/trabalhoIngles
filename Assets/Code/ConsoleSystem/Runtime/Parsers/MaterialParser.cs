using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class MaterialParser : ICommandParser
    {
        public Type TargetType => typeof(Material);

        public object Parse(string input)
        {
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in mats)
            {
                if (mat.name.Equals(input, StringComparison.OrdinalIgnoreCase))
                    return mat;
            }
            throw new Exception($"Material '{input}' não encontrado na memória/recursos.");
        }
    }
}