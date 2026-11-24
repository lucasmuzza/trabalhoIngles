using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityConsole
{
    public class ResolutionSuggestionProvider : IAdvancedArgumentSuggestionsProvider
    {
        public IEnumerable<string> GetSuggestions(string currentInputToken) => GetSuggestions(new List<string>(), 0, currentInputToken);

        public IEnumerable<string> GetSuggestions(List<string> allTokens, int argumentIndex, string currentInputToken)
        {
            Resolution[] availableResolutions = Screen.resolutions;

            if (argumentIndex == 0)
            {
                // Sugere larguras únicas
                return availableResolutions
                    .Select(r => r.width.ToString())
                    .Distinct()
                    .Where(w => w.StartsWith(currentInputToken, StringComparison.OrdinalIgnoreCase));
            }
            else if (argumentIndex == 1)
            {
                // Sugere alturas compatíveis com a largura já digitada no argumento 0
                if (allTokens.Count > 0 && int.TryParse(allTokens[0], out int selectedWidth))
                {
                    return availableResolutions
                        .Where(r => r.width == selectedWidth)
                        .Select(r => r.height.ToString())
                        .Distinct()
                        .Where(h => h.StartsWith(currentInputToken, StringComparison.OrdinalIgnoreCase));
                }
            }

            return Enumerable.Empty<string>();
        }
    }
}