using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityConsole
{
    public class ConsoleGroupSuggestionProvider : IArgumentSuggestionsProvider
    {
        // Garante que o provedor seja registrado automaticamente assim que o jogo inicia
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
        {
            // O ConsoleCommandManager vai criar a instância automaticamente se necessário
            if (ConsoleCommandManager.Instance != null)
            {
                ConsoleCommandManager.Instance.RegisterCommandSuggestions("commands.group", new ConsoleGroupSuggestionProvider());
            }
        }

        public IEnumerable<string> GetSuggestions(string currentInputToken)
        {
            if (ConsoleCommandManager.Instance == null)
            {
                yield break;
            }

            var commands = ConsoleCommandManager.Instance.GetAllRegisteredCommands();
            HashSet<string> groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var cmd in commands)
            {
                var containerAttr = cmd.ClassType.GetCustomAttribute<ConsoleContainerAttribute>();
                if (containerAttr != null && !string.IsNullOrEmpty(containerAttr.Prefix))
                {
                    groupNames.Add(containerAttr.Prefix);
                }

                if (cmd.ClassType != null)
                {
                    groupNames.Add(cmd.ClassType.Name);
                }
            }

            foreach (var group in groupNames.OrderBy(g => g))
            {
                if (group.StartsWith(currentInputToken, StringComparison.OrdinalIgnoreCase))
                {
                    yield return group;
                }
            }
        }
    }
}