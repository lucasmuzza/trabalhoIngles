using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[Serializable]
public struct AdverbChallengePhrase
{
    [TextArea] public string phraseWithBlank; // e.g., "She speaks English _____."
    public string[] acceptedAdverbs;        // e.g., new[] { "fluently" }
}

public class GameplayMenuScreen : NetworkUIScreen
{
    [Header("1v1 Adverb Challenges")]
    [SerializeField]
    private List<AdverbChallengePhrase> challenges = new()
    {
        new AdverbChallengePhrase { phraseWithBlank = "She runs _____ in the morning.", acceptedAdverbs = new[] { "quickly", "fast" } },
        new AdverbChallengePhrase { phraseWithBlank = "He finished the test _____.", acceptedAdverbs = new[] { "carefully" } },
        new AdverbChallengePhrase { phraseWithBlank = "They arrived _____ for the class.", acceptedAdverbs = new[] { "early" } }
    };

    [Header("UI Element Names (UI Toolkit)")]
    [SerializeField] private string phraseLabelName = "PhraseLabel";
    [SerializeField] private string currentInputLabelName = "CurrentInputLabel";
    [SerializeField] private string scoreLabelName = "ScoreLabel";
    [SerializeField] private string statusLabelName = "StatusLabel";

    [Header("Typing Rules")]
    [SerializeField] private int maxAdverbLength = 24;

    private Label phraseLabel;
    private Label currentInputLabel;
    private Label scoreLabel;
    private Label statusLabel;

    private string localBuffer = "";
    private bool inputSubscribed;

    // Network-synchronized match state
    private readonly NetworkVariable<int> currentChallengeIndex =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> hostScore =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> clientScore =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString128Bytes> roundStatus =
        new("Type the adverb and press ENTER", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    protected override void Awake()
    {
        base.Awake();
        if (rootVisualElement == null) return;

        phraseLabel = rootVisualElement.Q<Label>(phraseLabelName);
        currentInputLabel = rootVisualElement.Q<Label>(currentInputLabelName);
        scoreLabel = rootVisualElement.Q<Label>(scoreLabelName);
        statusLabel = rootVisualElement.Q<Label>(statusLabelName);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentChallengeIndex.OnValueChanged += OnMatchStateChanged;
        hostScore.OnValueChanged += OnMatchStateChanged;
        clientScore.OnValueChanged += OnMatchStateChanged;
        roundStatus.OnValueChanged += OnStatusChanged;

        RefreshMatchUI();
        RefreshLocalInputUI();
    }

    public override void OnNetworkDespawn()
    {
        currentChallengeIndex.OnValueChanged -= OnMatchStateChanged;
        hostScore.OnValueChanged -= OnMatchStateChanged;
        clientScore.OnValueChanged -= OnMatchStateChanged;
        roundStatus.OnValueChanged -= OnStatusChanged;

        UnsubscribeKeyboard();
        base.OnNetworkDespawn();
    }

    protected override void OnShow()
    {
        base.OnShow();
        SubscribeKeyboard();
        RefreshMatchUI();
        RefreshLocalInputUI();
    }

    protected override void OnHide()
    {
        UnsubscribeKeyboard();
        base.OnHide();
    }

    private void SubscribeKeyboard()
    {
        if (inputSubscribed || Keyboard.current == null) return;
        Keyboard.current.onTextInput += HandleTextInput;
        inputSubscribed = true;
    }

    private void UnsubscribeKeyboard()
    {
        if (!inputSubscribed || Keyboard.current == null) return;
        Keyboard.current.onTextInput -= HandleTextInput;
        inputSubscribed = false;
    }

    protected override void Update()
    {
        base.Update();

        if (!IsSpawned) return;
        if (visualizationRoot != null && visualizationRoot.style.display == DisplayStyle.None) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Handle Backspace
        if (kb.backspaceKey.wasPressedThisFrame && localBuffer.Length > 0)
        {
            localBuffer = localBuffer.Substring(0, localBuffer.Length - 1);
            OnLocalBufferChanged();
        }

        // Confirm attempt on Enter / Numpad Enter
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            ConfirmAttempt();
        }
    }

    private void HandleTextInput(char ch)
    {
        if (!IsSpawned) return;
        if (visualizationRoot != null && visualizationRoot.style.display == DisplayStyle.None) return;

        // Ignore control characters (Enter, Backspace, Tab)
        if (char.IsControl(ch)) return;

        // Allow letters and hyphens (for adverbs like "well-known" or "quickly")
        if (!char.IsLetter(ch) && ch != '-') return;

        if (localBuffer.Length >= maxAdverbLength) return;

        localBuffer += ch;
        OnLocalBufferChanged();
    }

    private void OnLocalBufferChanged()
    {
        RefreshLocalInputUI();

        PlayerOverheadTypingUI localOverhead = GetLocalPlayerOverheadUI();
        if (localOverhead != null)
        {
            localOverhead.SyncTypedTextServerRpc(localBuffer);
        }
    }

    private void ConfirmAttempt()
    {
        string attempt = localBuffer.Trim();
        if (string.IsNullOrEmpty(attempt)) return;

        SubmitAdverbServerRpc(attempt);

        // Clear local input buffer for the next try / next phrase
        localBuffer = "";
        RefreshLocalInputUI();
    }

    [ServerRpc()]
    private void SubmitAdverbServerRpc(FixedString64Bytes submittedAdverb, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (challenges == null || challenges.Count == 0) return;

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int idx = Mathf.Clamp(currentChallengeIndex.Value, 0, challenges.Count - 1);
        AdverbChallengePhrase activePhrase = challenges[idx];

        string guess = submittedAdverb.ToString().Trim();
        bool isMatch = IsAdverbValid(guess, activePhrase.acceptedAdverbs);

        PlayerOverheadTypingUI senderOverhead = GetPlayerOverheadUI(senderClientId);
        if (senderOverhead != null)
        {
            senderOverhead.ServerSetSubmissionResult(isMatch, guess);
        }

        if (isMatch)
        {
            // Award 1 point to whoever confirmed the right adverb first
            if (senderClientId == NetworkManager.ServerClientId)
                hostScore.Value += 1;
            else
                clientScore.Value += 1;

            roundStatus.Value = $"Player {senderClientId} nailed '{guess}'!";

            // Advance to next challenge (wraps around if at the end)
            currentChallengeIndex.Value = (idx + 1) % challenges.Count;
            ClearAllOverheadTexts();
        }
        else
        {
            roundStatus.Value = $"Player {senderClientId} tried '{guess}' — not quite!";
        }
    }

    private static bool IsAdverbValid(string guess, string[] acceptedAdverbs)
    {
        if (string.IsNullOrWhiteSpace(guess) || acceptedAdverbs == null) return false;

        foreach (string expected in acceptedAdverbs)
        {
            if (string.IsNullOrWhiteSpace(expected)) continue;
            if (string.Equals(guess.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private PlayerOverheadTypingUI GetLocalPlayerOverheadUI()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.LocalClient == null)
            return null;

        NetworkObject localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        return localPlayerObj != null ? localPlayerObj.GetComponent<PlayerOverheadTypingUI>() : null;
    }

    private PlayerOverheadTypingUI GetPlayerOverheadUI(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            return null;

        return client.PlayerObject != null
            ? client.PlayerObject.GetComponent<PlayerOverheadTypingUI>()
            : null;
    }

    private void ClearAllOverheadTexts()
    {
        if (!IsServer || NetworkManager.Singleton == null) return;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            PlayerOverheadTypingUI overhead = kvp.Value.PlayerObject != null
                ? kvp.Value.PlayerObject.GetComponent<PlayerOverheadTypingUI>()
                : null;

            overhead?.ServerClearTypedText();
        }
    }

    private void OnMatchStateChanged<T>(T previousValue, T newValue)
    {
        RefreshMatchUI();
    }

    private void OnStatusChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
    {
        RefreshMatchUI();
    }

    private void RefreshMatchUI()
    {
        if (challenges != null && challenges.Count > 0)
        {
            int idx = Mathf.Clamp(currentChallengeIndex.Value, 0, challenges.Count - 1);
            if (phraseLabel != null)
                phraseLabel.text = challenges[idx].phraseWithBlank;
        }

        if (scoreLabel != null)
            scoreLabel.text = $"Host: {hostScore.Value}  |  Challenger: {clientScore.Value}";

        if (statusLabel != null)
            statusLabel.text = roundStatus.Value.ToString();
    }

    private void RefreshLocalInputUI()
    {
        if (currentInputLabel != null)
            currentInputLabel.text = string.IsNullOrEmpty(localBuffer) ? "_ (type adverb)" : localBuffer;
    }
}