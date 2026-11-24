using UnityEngine;
using System.Globalization;
using System;

namespace UnityConsole
{
    public class GameObjectParser : ICommandParser
    {
        public Type TargetType => typeof(GameObject);

        public object Parse(string input)
        {
            var go = GameObject.Find(input);
            if (go != null) return go;

            var allGo = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var resGo in allGo)
            {
                if (resGo.name.Equals(input, StringComparison.OrdinalIgnoreCase))
                    return resGo;
            }
            throw new Exception($"GameObject '{input}' não foi encontrado na cena.");
        }
    }
}