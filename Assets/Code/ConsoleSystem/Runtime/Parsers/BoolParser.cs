using UnityEngine;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityConsole
{
    public class BoolParser : ICommandParser, IArgumentSuggestionsProvider
    {
        public Type TargetType => typeof(bool);

        public object Parse(string input)
        {
            if (input == "1" || input.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;

            if (input == "0" || input.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            
            throw new FormatException($"Não foi possível converter '{input}' para Boolean.");
        }

        public IEnumerable<string> GetSuggestions(string currentInputToken)
        {
            string[] boolOptions = { "true", "false"};
            return boolOptions.Where(opt => opt.StartsWith(currentInputToken, StringComparison.OrdinalIgnoreCase));
        }
    }
}