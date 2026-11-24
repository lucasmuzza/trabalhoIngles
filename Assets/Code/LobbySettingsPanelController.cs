using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.UIElements;

public class LobbySettingsPanelController
{
    private readonly LobbyNetworkState _networkState;
    private readonly List<string> _availableMaps;

    private readonly DropdownField _mapDropdown;
    private readonly Toggle _privateToggle;

    private readonly TextField _phraseInputField;
    private readonly TextField _answersInputField;
    private readonly Button _addChallengeButton;
    private readonly VisualElement _challengesListContainer;

    private delegate void SettingModifier(ref LobbySettings settings);

    public LobbySettingsPanelController(
        VisualElement settingsPanelRoot,
        LobbyNetworkState networkState,
        List<string> availableMaps)
    {
        _networkState = networkState;
        _availableMaps = availableMaps ?? new List<string>();

        _mapDropdown = settingsPanelRoot.Q<DropdownField>("mapDropdown");
        _privateToggle = settingsPanelRoot.Q<Toggle>("privateToggle");

        _phraseInputField = settingsPanelRoot.Q<TextField>("phraseInputField");
        _answersInputField = settingsPanelRoot.Q<TextField>("answersInputField");
        _addChallengeButton = settingsPanelRoot.Q<Button>("addChallengeButton");
        _challengesListContainer = settingsPanelRoot.Q<VisualElement>("challengesListContainer");

        if (_mapDropdown != null && _availableMaps.Count > 0)
            _mapDropdown.choices = new List<string>(_availableMaps);
    }

    public void InitializeHostControls()
    {
        SyncWithSettings(_networkState.Settings);
        BindCallbacks();
    }

    public void SyncWithSettings(LobbySettings settings)
    {
        _mapDropdown?.SetValueWithoutNotify(settings.MapName.ToString());
        _privateToggle?.SetValueWithoutNotify(settings.IsPrivate);

        RebuildChallengesList(settings);
    }

    private void BindCallbacks()
    {
        _mapDropdown?.RegisterValueChangedCallback(evt =>
        {
            if (string.IsNullOrWhiteSpace(evt.newValue)) return;
            UpdateSettings((ref LobbySettings s) => s.MapName = new FixedString32Bytes(evt.newValue));
        });

        _privateToggle?.RegisterValueChangedCallback(evt =>
            UpdateSettings((ref LobbySettings s) => s.IsPrivate = evt.newValue));

        if (_addChallengeButton != null)
        {
            _addChallengeButton.clicked += HandleAddChallengeClicked;
        }
    }

    private void HandleAddChallengeClicked()
    {
        if (!_networkState.IsServer) return;

        string phrase = _phraseInputField?.value?.Trim();
        string answers = _answersInputField?.value?.Trim();

        if (string.IsNullOrWhiteSpace(phrase) || string.IsNullOrWhiteSpace(answers))
            return;

        UpdateSettings((ref LobbySettings s) =>
        {
            s.AddChallenge(phrase, answers);
        });

        if (_phraseInputField != null) _phraseInputField.value = string.Empty;
        if (_answersInputField != null) _answersInputField.value = string.Empty;

        SyncWithSettings(_networkState.Settings);
    }

    private void RebuildChallengesList(LobbySettings settings)
    {
        if (_challengesListContainer == null) return;

        _challengesListContainer.Clear();

        for (int i = 0; i < settings.PhrasesAndAnswers.Length; i++)
        {
            int indexToRemove = i;
            AdverbRoundEntry entry = settings.PhrasesAndAnswers[i];

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            Label label = new Label($"{i + 1}. {entry.Phrase}  [{entry.AcceptedAdverbsRaw}]");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;

            row.Add(label);

            if (_networkState.IsServer)
            {
                Button removeBtn = new Button(() =>
                {
                    UpdateSettings((ref LobbySettings s) =>
                    {
                        if (indexToRemove >= 0 && indexToRemove < s.PhrasesAndAnswers.Length)
                            s.PhrasesAndAnswers.RemoveAt(indexToRemove);
                    });
                    SyncWithSettings(_networkState.Settings);
                })
                {
                    text = "X"
                };
                row.Add(removeBtn);
            }

            _challengesListContainer.Add(row);
        }
    }

    private void UpdateSettings(SettingModifier modifier)
    {
        if (!_networkState.IsServer) return;

        LobbySettings updated = _networkState.Settings;
        modifier(ref updated);
        updated.MaxPlayers = LobbySettings.DefaultMaxPlayers1v1;

        _networkState.ServerUpdateSettings(updated);
    }
}