using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityConsole
{
    [ConsoleContainer("Console")]
    public class ConsoleHelpCommands : MonoBehaviour
    {
        private void Awake()
        {
            if (ConsoleCommandManager.Instance != null)
            {
                // Registra o provider dedicado para o comando commands.group
                ConsoleCommandManager.Instance.RegisterCommandSuggestions("commands.group", new ConsoleGroupSuggestionProvider());
            }
        }

        [ConsoleCommand(name: "commands", description: "Lista todos os comandos disponíveis no console.")]
        public static void ListAllCommands()
        {
            if (ConsoleCommandManager.Instance == null)
            {
                Debug.Log("[Console] ConsoleCommandManager não está ativo.");
                return;
            }

            var commands = ConsoleCommandManager.Instance.GetAllRegisteredCommands();
            
            Debug.Log("=== COMANDOS DISPONÍVEIS NO CONSOLE ===");
            foreach (var cmd in commands.OrderBy(c => c.CommandName))
            {
                string desc = string.IsNullOrEmpty(cmd.Description) ? "Sem descrição." : cmd.Description;
                Debug.Log($"  • <color=#00FFCC>{cmd.CommandName}</color>: {desc} <color=#888888>({cmd.ClassType.Name}.{cmd.MethodName})</color>");
            }
            Debug.Log($"Total de comandos registrados: {commands.Count()}");
        }

        [ConsoleCommand(name: "commands.group", description: "Lista todos os comandos dentro de um grupo/container específico (ex: commands.group player_inventory).")]
        public static void ListCommandsByGroup(string groupName)
        {
            if (ConsoleCommandManager.Instance == null)
            {
                Debug.Log("[Console] ConsoleCommandManager não está ativo.");
                return;
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                Debug.LogWarning("[Console] Informe o nome do grupo/container. Ex: commands.group player_inventory");
                return;
            }

            var commands = ConsoleCommandManager.Instance.GetAllRegisteredCommands();
            var matchingCommands = commands.Where(cmd => 
            {
                var containerAttr = cmd.ClassType.GetCustomAttribute<ConsoleContainerAttribute>();
                if (containerAttr != null && containerAttr.Prefix.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
                if (cmd.ClassType.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }).ToList();

            Debug.Log($"=== COMANDOS DO GRUPO: [{groupName.ToUpperInvariant()}] ===");
            
            if (matchingCommands.Count == 0)
            {
                Debug.Log($"Nenhum comando encontrado para o grupo '{groupName}'.");
                return;
            }

            foreach (var cmd in matchingCommands.OrderBy(c => c.CommandName))
            {
                string desc = string.IsNullOrEmpty(cmd.Description) ? "Sem descrição." : cmd.Description;
                Debug.Log($"  • <color=#00FFCC>{cmd.CommandName}</color>: {desc}");
            }
            Debug.Log($"Total no grupo '{groupName}': {matchingCommands.Count}");
        }

        [ConsoleCommand(name: "clear", description: "Limpa todo o histórico de logs do console.")]
        public static void ClearConsoleCommand()
        {
            var controller = FindFirstObjectByType<ConsoleViewController>();
            if (controller != null)
            {
                controller.ClearConsole();
            }
            else
            {
                Debug.LogWarning("[Console] ConsoleViewController não foi encontrado na cena.");
            }
        }

        [ConsoleCommand(name: "collapse", description: "Ativa ou desativa o agrupamento de logs repetidos.")]
        public static void ToggleCollapseCommand(bool enable)
        {
            var controller = FindFirstObjectByType<ConsoleViewController>();
            if (controller != null)
            {
                controller.ToggleCollapse(enable);
            }
        }
    }
}