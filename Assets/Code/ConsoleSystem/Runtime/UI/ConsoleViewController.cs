using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityConsole
{
    public class ConsoleViewController : UIScreen
    {
        // Containers
        private VisualElement container;
        private VisualElement consoleHeader;
        private VisualElement consoleMain;
        private VisualElement consoleBottom;

        // Header
        private Button closeButton;

        // Main
        private ScrollView logArea;

        // Bottom
        private TextField commandInput;
        private VisualElement autocompleteBox;
        private ListView suggestionList;
        private VisualElement resizeHandle;

        [SerializeField] private bool shouldOpenOnStart;
        [SerializeField] private int sortingOrderOnOpen = 999;

        private bool isCollapseActive = false;
        private bool isResizing = false;
        private bool isDragging = false;
        
        private Vector2 startMousePos;
        private Vector2 startContainerSize;
        private Vector2 dragOffset;

        private PlayerInputHandler playerInputHandler;

        private struct LogRecord
        {
            public string condition;
            public string stackTrace;
            public LogType type;
        }

        private List<LogRecord> rawLogsHistory = new();
        private Dictionary<string, LogElementHolder> activeLogs = new();
        private List<string> availableCommands = new();
        private List<string> filteredSuggestions = new();
        private int selectedSuggestionIndex = -1;

        // Histórico de comandos digitados
        private List<string> commandHistory = new();
        private int historyIndex = -1;

        private bool isUpdatingSelectionInternally = false;

        private class LogElementHolder
        {
            public VisualElement root;
            public Label messageLabel;
            public Label counterLabel;
            public int count;
        }

        protected override void Awake()
        {
            base.Awake();

            visualizationRoot = rootVisualElement.Q<VisualElement>("ConsoleContainer");
            if (visualizationRoot != null)
            {
                consoleHeader = visualizationRoot.Q<VisualElement>("ConsoleHeader");
                consoleMain = visualizationRoot.Q<VisualElement>("ConsoleMain");
                consoleBottom = visualizationRoot.Q<VisualElement>("ConsoleBottom");

                closeButton = consoleHeader?.Q<Button>("CloseButton");
                logArea = consoleMain?.Q<ScrollView>("LogArea");

                commandInput = consoleBottom?.Q<TextField>("CommandInput");
                autocompleteBox = consoleBottom?.Q<VisualElement>("AutocompleteBox");
                suggestionList = consoleBottom?.Q<ListView>("SuggestionList");
                
                resizeHandle = consoleBottom?.Q<VisualElement>("ResizeHandle");
            }

            if (consoleHeader != null && visualizationRoot != null)
            {
                consoleHeader.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
                consoleHeader.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
                consoleHeader.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);
            }

            if (suggestionList != null)
            {
                suggestionList.makeItem = () => {
                    var label = new Label();
                    label.AddToClassList("suggestion-item-label");
                    label.pickingMode = PickingMode.Position; 
                    return label;
                };

                suggestionList.bindItem = (element, index) => {
                    if (index >= 0 && index < filteredSuggestions.Count)
                    {
                        ((Label)element).text = filteredSuggestions[index];
                    }
                };

                suggestionList.itemsSource = filteredSuggestions;
                suggestionList.selectionChanged += OnSuggestionSelectionChanged;
            }

            if (commandInput != null)
            {
                commandInput.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
                commandInput.RegisterValueChangedCallback(OnInputValueChanged);
            }
            
            if (closeButton != null) closeButton.clicked += RequestClose;

            if (resizeHandle != null)
            {
                resizeHandle.RegisterCallback<MouseDownEvent>(OnResizeMouseDown);
                resizeHandle.RegisterCallback<MouseMoveEvent>(OnResizeMouseMove);
                resizeHandle.RegisterCallback<MouseUpEvent>(OnResizeMouseUp);
            }

            Hide();
        }

        protected override void Start()
        {
            base.Start();

            playerInputHandler = PlayerInputHandler.Instance;

            if (playerInputHandler != null)
            {
                playerInputHandler.OnConsoleToggle += ToggleConsoleVisibility;
                playerInputHandler.OnConsoleConfirm += HandleConsoleConfirm;
                playerInputHandler.OnConsoleClosed += RequestClose;
            }

            Application.logMessageReceived += OnLogReceived;
            
            if (shouldOpenOnStart) 
                Show();
            else 
                Hide();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInputHandler != null)
            {
                playerInputHandler.OnConsoleToggle -= ToggleConsoleVisibility;
                playerInputHandler.OnConsoleConfirm -= HandleConsoleConfirm;
                playerInputHandler.OnConsoleClosed -= RequestClose;
            }

            if (commandInput != null)
            {
                commandInput.UnregisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
            }

            if (suggestionList != null)
            {
                suggestionList.selectionChanged -= OnSuggestionSelectionChanged;
            }

            Application.logMessageReceived -= OnLogReceived;
        }

        public override void Show()
        {
            base.Show();

            if (uiDocument != null && uiDocument.panelSettings != null)
            {
                uiDocument.panelSettings.sortingOrder = sortingOrderOnOpen;
            }

            if (playerInputHandler != null)
            {
                playerInputHandler.SwitchToConsole();
            }
            if (commandInput != null)
            {
                commandInput.Focus();
            }
        }

        public override void Hide()
        {
            base.Hide();

            if (playerInputHandler != null)
            {
                playerInputHandler.SwitchToGameplay();
            }
        }

        private void RequestClose()
        {
            UISystem.Instance.Back();
        }

        public void InitializeCommands(IEnumerable<string> commands)
        {
            availableCommands = commands.ToList();
        }

        private void ToggleConsoleVisibility(bool shouldOpen)
        {
            if (shouldOpen) 
                UISystem.Instance.Open<ConsoleViewController>();
            else 
                Hide();
        }

        private void OnInputValueChanged(ChangeEvent<string> evt)
        {
            if (isUpdatingSelectionInternally) return;

            string text = evt.newValue;

            if (string.IsNullOrEmpty(text))
            {
                if (autocompleteBox != null) autocompleteBox.style.display = DisplayStyle.None;
                selectedSuggestionIndex = -1;
                return;
            }

            // SE O COMANDO JÁ ESTIVER COMPLETO COM TODOS OS ARGUMENTOS:
            // Ocultamos o autocomplete para liberar as setas para cima/baixo para o histórico de comandos.
            if (ConsoleCommandManager.Instance != null && ConsoleCommandManager.Instance.IsCommandFullyTyped(text, out _))
            {
                if (autocompleteBox != null) autocompleteBox.style.display = DisplayStyle.None;
                selectedSuggestionIndex = -1;
                filteredSuggestions.Clear();
                return;
            }

            filteredSuggestions = GetSuggestionsForInput(text);

            if (filteredSuggestions.Count > 0 && autocompleteBox != null && suggestionList != null)
            {
                suggestionList.itemsSource = filteredSuggestions;
                suggestionList.Rebuild();
                autocompleteBox.style.display = DisplayStyle.Flex;
                
                selectedSuggestionIndex = 0;
                
                isUpdatingSelectionInternally = true;
                suggestionList.selectedIndex = selectedSuggestionIndex;
                isUpdatingSelectionInternally = false;
            }
            else if (autocompleteBox != null)
            {
                autocompleteBox.style.display = DisplayStyle.None;
                selectedSuggestionIndex = -1;
            }
        }

        private List<string> GetSuggestionsForInput(string input)
        {
            if (!input.Contains(' '))
            {
                return availableCommands
                    .Where(c => c.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            int spaceIndex = input.IndexOf(' ');
            string commandName = input.Substring(0, spaceIndex);
            string argumentLine = input.Substring(spaceIndex + 1);

            if (ConsoleCommandManager.Instance != null)
            {
                var suggestions = ConsoleCommandManager.Instance.GetArgumentSuggestions(commandName, argumentLine);
                if (suggestions != null && suggestions.Any())
                {
                    return suggestions.ToList();
                }
            }

            return new List<string>();
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            bool isAutocompleteVisible = autocompleteBox != null && autocompleteBox.style.display == DisplayStyle.Flex && filteredSuggestions.Count > 0;

            if (isAutocompleteVisible)
            {
                // Tab handles autocomplete selection exclusively
                if (evt.keyCode == KeyCode.Tab)
                {
                    evt.StopPropagation();
                    ApplySelectedSuggestion();
                    return;
                }

                if (evt.keyCode == KeyCode.DownArrow)
                {
                    evt.StopPropagation();
                    NavigateSuggestions(1);
                    return;
                }

                if (evt.keyCode == KeyCode.UpArrow)
                {
                    evt.StopPropagation();
                    NavigateSuggestions(-1);
                    return;
                }
                
                // Return (Enter) is deliberately omitted here so it falls through to SubmitCommand()!
            }
            else
            {
                // Navegação no Histórico de Comandos ao pressionar Seta Para Cima / Para Baixo sem autocomplete ativo
                if (evt.keyCode == KeyCode.UpArrow)
                {
                    if (commandHistory.Count > 0)
                    {
                        evt.StopPropagation();
                        if (historyIndex == -1)
                        {
                            historyIndex = commandHistory.Count - 1;
                        }
                        else if (historyIndex > 0)
                        {
                            historyIndex--;
                        }
                        SetInputTextFromHistory();
                    }
                    return;
                }

                if (evt.keyCode == KeyCode.DownArrow)
                {
                    if (historyIndex != -1)
                    {
                        evt.StopPropagation();
                        historyIndex++;
                        if (historyIndex >= commandHistory.Count)
                        {
                            historyIndex = -1;
                            commandInput.value = string.Empty;
                        }
                        else
                        {
                            SetInputTextFromHistory();
                        }
                    }
                    return;
                }
            }

            // Return / Enter always submits the command regardless of suggestions
            if (evt.keyCode == KeyCode.Return)
            {
                evt.StopPropagation();
                SubmitCommand();
            }
        }

        private void SetInputTextFromHistory()
        {
            if (commandInput == null || historyIndex < 0 || historyIndex >= commandHistory.Count) return;

            string historyText = commandHistory[historyIndex];
            isUpdatingSelectionInternally = true;
            commandInput.value = historyText;
            isUpdatingSelectionInternally = false;
            
            rootVisualElement.schedule.Execute(() =>
            {
                if (commandInput != null)
                {
                    commandInput.cursorIndex = historyText.Length;
                    commandInput.selectIndex = historyText.Length;
                }
            });
        }

        private void NavigateSuggestions(int direction)
        {
            if (filteredSuggestions.Count == 0 || suggestionList == null) return;

            selectedSuggestionIndex = (selectedSuggestionIndex + direction + filteredSuggestions.Count) % filteredSuggestions.Count;
            
            isUpdatingSelectionInternally = true;
            suggestionList.selectedIndex = selectedSuggestionIndex;
            suggestionList.ScrollToItem(selectedSuggestionIndex);
            isUpdatingSelectionInternally = false;
        }

        private void OnSuggestionSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (selectedItems == null || !selectedItems.Any()) return;
            if (isUpdatingSelectionInternally) return;

            string selectedValue = selectedItems.First() as string;
            if (!string.IsNullOrEmpty(selectedValue))
            {
                selectedSuggestionIndex = filteredSuggestions.IndexOf(selectedValue);
            }
        }

        private void HandleConsoleConfirm()
        {
            if (visualizationRoot != null && visualizationRoot.style.display == DisplayStyle.Flex)
            {
                SubmitCommand();
            }
        }

        private void ApplySelectedSuggestion()
        {
            if (selectedSuggestionIndex < 0 || selectedSuggestionIndex >= filteredSuggestions.Count || commandInput == null) 
                return;

            string suggestion = filteredSuggestions[selectedSuggestionIndex];
            
            string finalInput = suggestion;
            if (commandInput.value.Contains(' '))
            {
                int spaceIndex = commandInput.value.IndexOf(' ');
                string commandPrefix = commandInput.value.Substring(0, spaceIndex + 1);
                finalInput = commandPrefix + suggestion;
            }

            isUpdatingSelectionInternally = true;
            commandInput.value = finalInput;
            isUpdatingSelectionInternally = false;
            
            if (autocompleteBox != null) autocompleteBox.style.display = DisplayStyle.None;
            selectedSuggestionIndex = -1;

            commandInput.Focus();
            
            rootVisualElement.schedule.Execute(() =>
            {
                if (commandInput != null)
                {
                    commandInput.cursorIndex = finalInput.Length;
                    commandInput.selectIndex = finalInput.Length;
                }
            });
        }

        private void SubmitCommand()
        {
            if (commandInput == null) return;

            string text = commandInput.value;
            if (!string.IsNullOrEmpty(text))
            {
                // Adiciona ao histórico caso seja diferente do último comando digitado
                if (commandHistory.Count == 0 || commandHistory.Last() != text)
                {
                    commandHistory.Add(text);
                }
                historyIndex = -1; // Reseta o ponteiro de histórico

                if (ConsoleCommandManager.Instance != null)
                {
                    ConsoleCommandManager.Instance.ExecuteCommand(text);
                }
                commandInput.value = string.Empty;
                if (autocompleteBox != null) autocompleteBox.style.display = DisplayStyle.None;
                selectedSuggestionIndex = -1;
                commandInput.Focus();
            }
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && visualizationRoot != null)
            {
                isDragging = true;
                dragOffset = evt.position - new Vector3(visualizationRoot.worldBound.position.x, visualizationRoot.worldBound.position.y, 0);
                consoleHeader.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (isDragging && visualizationRoot != null)
            {
                Vector2 newPos = evt.position - new Vector3(dragOffset.x, dragOffset.y, 0);
                visualizationRoot.style.left = newPos.x;
                visualizationRoot.style.top = newPos.y;
                evt.StopPropagation();
            }
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (isDragging && consoleHeader != null)
            {
                isDragging = false;
                consoleHeader.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            rawLogsHistory.Add(new LogRecord { condition = condition, stackTrace = stackTrace, type = type });
            RenderLogItem(condition, type);
        }

        private void RenderLogItem(string condition, LogType type)
        {
            if (logArea == null) return;

            string key = condition + "" + type.ToString();

            if (isCollapseActive && activeLogs.TryGetValue(key, out var holder))
            {
                holder.count++;
                if (holder.counterLabel != null)
                {
                    holder.counterLabel.text = holder.count.ToString();
                    holder.counterLabel.style.display = DisplayStyle.Flex;
                }
                return;
            }

            var item = new VisualElement();
            item.AddToClassList("log-item");

            switch (type)
            {
                case LogType.Warning: item.AddToClassList("log-warning"); break;
                case LogType.Error: case LogType.Exception: item.AddToClassList("log-error"); break;
                default: item.AddToClassList("log-normal"); break;
            }

            var msgLabel = new Label(condition);
            item.Add(msgLabel);

            var countLabel = new Label("1");
            countLabel.AddToClassList("log-counter");
            countLabel.style.display = DisplayStyle.None;
            item.Add(countLabel);

            logArea.Add(item);

            if (isCollapseActive)
            {
                activeLogs[key] = new LogElementHolder { root = item, messageLabel = msgLabel, counterLabel = countLabel, count = 1 };
            }
            else
            {
                string uniqueKey = Guid.NewGuid().ToString();
                activeLogs[uniqueKey] = new LogElementHolder { root = item, messageLabel = msgLabel, counterLabel = countLabel, count = 1 };
            }

            logArea.RegisterCallback<GeometryChangedEvent>(ScrollToBottom);
        }

        public void ToggleCollapse(bool collapse)
        {
            isCollapseActive = collapse;

            if (logArea != null) logArea.Clear();
            activeLogs.Clear();

            foreach (var log in rawLogsHistory)
            {
                RenderLogItem(log.condition, log.type);
            }
            
            Debug.Log($"[Console] Modo Collapse: {(isCollapseActive ? "Ativado" : "Desativado")}");
        }

        private void OnResizeMouseDown(MouseDownEvent evt)
        {
            if (visualizationRoot == null || resizeHandle == null) return;

            isResizing = true;
            startMousePos = evt.mousePosition;
            startContainerSize = new Vector2(visualizationRoot.layout.width, visualizationRoot.layout.height);
            resizeHandle.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnResizeMouseMove(MouseMoveEvent evt)
        {
            if (!isResizing || visualizationRoot == null) return;

            Vector2 delta = (Vector2)evt.mousePosition - startMousePos;
            
            float newWidth = Mathf.Max(500, startContainerSize.x + delta.x);
            float newHeight = Mathf.Max(500, startContainerSize.y + delta.y);

            visualizationRoot.style.width = newWidth;
            visualizationRoot.style.height = newHeight;
            evt.StopPropagation();
        }

        private void OnResizeMouseUp(MouseUpEvent evt)
        {
            if (!isResizing || resizeHandle == null) return;
            isResizing = false;
            resizeHandle.ReleaseMouse();
            evt.StopPropagation();
        }

        private void ScrollToBottom(GeometryChangedEvent evt)
        {
            if (logArea == null) return;
            logArea.UnregisterCallback<GeometryChangedEvent>(ScrollToBottom);
            logArea.scrollOffset = new Vector2(logArea.scrollOffset.x, logArea.contentContainer.layout.height);
        }

        public void ClearConsole()
        {
            if (logArea != null) logArea.Clear();
            activeLogs.Clear();
            rawLogsHistory.Clear();
        }
    }
}