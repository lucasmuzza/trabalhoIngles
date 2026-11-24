using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityConsole
{
    public interface IArgumentSuggestionsProvider
    {
        IEnumerable<string> GetSuggestions(string currentInputToken);
    }

    // Interface para comandos com múltiplos argumentos que precisam de contexto (ex: largura e altura)
    public interface IAdvancedArgumentSuggestionsProvider : IArgumentSuggestionsProvider
    {
        IEnumerable<string> GetSuggestions(List<string> allTokens, int argumentIndex, string currentInputToken);
    }

    public class ConsoleCommandManager : MonoBehaviour
    {
        private static ConsoleCommandManager _instance;
        public static ConsoleCommandManager Instance;

        private Dictionary<string, SourceGeneratedCommand> _commandCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<Type, ICommandParser> _parsers = new();
        private Dictionary<string, IArgumentSuggestionsProvider> _commandSpecificProviders = new(StringComparer.OrdinalIgnoreCase);

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            DontDestroyOnLoad(gameObject);

            InitializeParsers();
            LoadSourceGeneratedCommands();
        }

        private void InitializeParsers()
        {
            RegisterParser(new IntParser());
            RegisterParser(new FloatParser());
            RegisterParser(new DoubleParser());
            RegisterParser(new LongParser());
            RegisterParser(new BoolParser());
            RegisterParser(new Vector2Parser());
            RegisterParser(new Vector3Parser());
            RegisterParser(new Vector2IntParser());
            RegisterParser(new Vector3IntParser());
            RegisterParser(new QuaternionParser());
            RegisterParser(new GameObjectParser());
            RegisterParser(new MaterialParser());
        }

        private void LoadSourceGeneratedCommands()
        {
            List<SourceGeneratedCommand> generatedList = new();

            try
            {
                Type generatedClass = Type.GetType("UnityConsole.GeneratedConsoleCommands");
                if (generatedClass != null)
                {
                    MethodInfo registerMethod = generatedClass.GetMethod("GeneratedRegister");
                    registerMethod?.Invoke(null, new object[] { generatedList });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Ainda não há comandos gerados pelo Roslyn estruturados: {e.Message}");
            }

            foreach (var cmd in generatedList)
            {
                _commandCache[cmd.CommandName] = cmd;
            }

            // Garante que a UI receba os comandos após os provedores estarem registrados
            var uiController = FindFirstObjectByType<ConsoleViewController>();
            if (uiController != null)
            {
                uiController.InitializeCommands(_commandCache.Keys);
            }
        }

        public void RegisterParser(ICommandParser parser)
        {
            _parsers[parser.TargetType] = parser;
        }

        /// <summary>
        /// Permite registrar provedores de sugestões customizados para um comando específico de forma desacoplada.
        /// </summary>
        public void RegisterCommandSuggestions(string commandName, IArgumentSuggestionsProvider provider)
        {
            _commandSpecificProviders[commandName] = provider;
        }

        public void ExecuteCommand(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return;

            // Divide mantendo strings compostas entre aspas juntas: ex: comando 10 "meu texto longo"
            var tokens = TokenizeInput(rawInput);
            if (tokens.Count == 0) return;

            string commandName = tokens[0].ToLower();

            if (!_commandCache.TryGetValue(commandName, out var cmd))
            {
                Debug.LogError($"[Console] Comando '{commandName}' não encontrado.");
                return;
            }

            try
            {
                // Isola os argumentos textuais passados
                List<string> textArgs = tokens.GetRange(1, tokens.Count - 1);

                if (textArgs.Count < cmd.ParameterTypes.Length)
                {
                    Debug.LogError($"[Console] Parâmetros insuficientes para '{commandName}'. Esperado: {cmd.ParameterTypes.Length}, Recebido: {textArgs.Count}");
                    return;
                }

                object[] parsedArgs = new object[cmd.ParameterTypes.Length];
                for (int i = 0; i < cmd.ParameterTypes.Length; i++)
                {
                    Type targetType = cmd.ParameterTypes[i];
                    if (_parsers.TryGetValue(targetType, out var parser))
                    {
                        parsedArgs[i] = parser.Parse(textArgs[i]);
                    }
                    else
                    {
                        // Fallback genérico caso use um tipo primitivo/Enum não registrado explicitamente
                        parsedArgs[i] = Convert.ChangeType(textArgs[i], targetType, CultureInfo.InvariantCulture);
                    }
                }

                // Invocação dinâmica
                object targetInstance = null;
                if (!cmd.IsStatic)
                {
                    targetInstance = FindFirstObjectByType(cmd.ClassType);
                    if (targetInstance == null)
                    {
                        Debug.LogError($"[Console] Instância ativa de '{cmd.ClassType.Name}' não encontrada na cena para executar o comando não-estático.");
                        return;
                    }
                }

                // Busca o método considerando a assinatura exata dos parâmetros para evitar AmbiguousMatchException.
                MethodInfo method = cmd.ClassType.GetMethod(
                    name: cmd.MethodName,
                    bindingAttr: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    binder: null,
                    types: cmd.ParameterTypes,
                    modifiers: null
                );

                object result = method?.Invoke(targetInstance, parsedArgs);

                if (result != null)
                {
                    Debug.Log($"[Console] {commandName} retornado: {result}");
                }
            }
            catch (TargetInvocationException tie)
            {
                Debug.LogError($"[Console] Exceção dentro do comando {commandName}: {tie.InnerException?.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Console] Falha de processamento no comando {commandName}: {e.Message}");
            }
        }

        private List<string> TokenizeInput(string input)
        {
            var list = new List<string>();
            // Regex para extrair argumentos separados por espaços, preservando blocos entre aspas
            var matches = Regex.Matches(input, @"(?<=\x22)[^\x22]*(?=\x22)|[^\s\x22]+");
            foreach (Match match in matches)
            {
                list.Add(match.Value);
            }
            return list;
        }

        /// <summary>
        /// Fornece sugestões de autocomplete para os argumentos do comando analisando o índice ativo e provedores específicos.
        /// </summary>
        public IEnumerable<string> GetArgumentSuggestions(string commandName, string rawInputLine)
        {
            var tokens = TokenizeInput(rawInputLine);
            int argumentIndex = rawInputLine.EndsWith(" ") ? tokens.Count : Math.Max(0, tokens.Count - 1);
            string currentTokenToFilter = tokens.Count > 0 && !rawInputLine.EndsWith(" ") ? tokens.Last() : string.Empty;

            // 1. Verifica se existe um provedor customizado registrado para este comando específico
            if (_commandSpecificProviders.TryGetValue(commandName, out var customProvider))
            {
                if (customProvider is IAdvancedArgumentSuggestionsProvider advancedProvider)
                {
                    return advancedProvider.GetSuggestions(tokens, argumentIndex, currentTokenToFilter);
                }
                return customProvider.GetSuggestions(currentTokenToFilter);
            }

            // 2. Fallback padrão baseado no tipo do parâmetro esperado
            if (!_commandCache.TryGetValue(commandName, out var cmd))
            {
                return Enumerable.Empty<string>();
            }

            Type[] paramTypes = cmd.ParameterTypes;
            if (paramTypes == null || argumentIndex >= paramTypes.Length)
            {
                return Enumerable.Empty<string>();
            }

            Type targetParamType = paramTypes[argumentIndex];
            if (_parsers.TryGetValue(targetParamType, out var parser))
            {
                if (parser is IArgumentSuggestionsProvider typeProvider)
                {
                    return typeProvider.GetSuggestions(currentTokenToFilter);
                }
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Verifica se o comando atual já foi totalmente preenchido com todos os argumentos obrigatórios.
        /// </summary>
        public bool IsCommandFullyTyped(string rawInput, out SourceGeneratedCommand matchedCmd)
        {
            matchedCmd = default;
            if (string.IsNullOrWhiteSpace(rawInput)) return false;

            var tokens = TokenizeInput(rawInput);
            if (tokens.Count == 0) return false;

            string commandName = tokens[0].ToLower();
            if (!_commandCache.TryGetValue(commandName, out matchedCmd))
            {
                return false;
            }

            int providedArgsCount = tokens.Count - 1;
            int requiredArgsCount = matchedCmd.ParameterTypes?.Length ?? 0;

            return providedArgsCount >= requiredArgsCount;
        }

        /// <summary>
        /// Retorna uma cópia de todos os comandos registrados no cache.
        /// </summary>
        public IEnumerable<SourceGeneratedCommand> GetAllRegisteredCommands()
        {
            return _commandCache.Values;
        }
    }
}