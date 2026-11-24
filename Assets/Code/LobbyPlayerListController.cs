using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


/// <summary>
/// Drives the player list in the Lobby tab. Creates one card per player from the
/// PlayerCard template, updates name / ready state and loads the Steam avatar
/// through SteamManager (memory + disk cache).
/// </summary>
public class LobbyPlayerListController
{
    private const int MaxPlayers = 2;

    private readonly VisualTreeAsset _cardTemplate;
    private readonly ScrollView _list;
    private readonly Label _countLabel;
    private readonly Label _emptyLabel;
    private readonly Button _readyButton;

    private readonly Dictionary<ulong, VisualElement> _cards = new();

    public LobbyPlayerListController(VisualElement panel, VisualTreeAsset cardTemplate)
    {
        _cardTemplate = cardTemplate;

        _list = panel.Q<ScrollView>("playerList");
        _countLabel = panel.Q<Label>("playerCountLabel");
        _emptyLabel = panel.Q<Label>("emptyListLabel");
        _readyButton = panel.Q<Button>("readyButton");
    }

    public Button ReadyButton => _readyButton;

    public void SetPlayers(IReadOnlyList<LobbyPlayerData> players)
    {
        if (_list == null || _cardTemplate == null) return;

        // Remove cards of players who left
        var present = new HashSet<ulong>();
        foreach (var p in players) present.Add(p.SteamId);

        var toRemove = new List<ulong>();
        foreach (var kvp in _cards)
            if (!present.Contains(kvp.Key)) toRemove.Add(kvp.Key);

        foreach (ulong id in toRemove)
        {
            _cards[id].RemoveFromHierarchy();
            _cards.Remove(id);
        }

        // Add or update the rest
        foreach (var player in players)
        {
            if (!_cards.TryGetValue(player.SteamId, out VisualElement card))
            {
                card = _cardTemplate.Instantiate().Q<VisualElement>("playerCard");
                _cards[player.SteamId] = card;
                _list.Add(card);
                LoadAvatar(player.SteamId, card.Q<Image>("avatarImage"), card);
            }

            ApplyData(card, player);
        }

        _countLabel.text = $"{players.Count} / {MaxPlayers}";
        _emptyLabel.style.display = players.Count < MaxPlayers ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetLocalReadyButtonState(bool isReady)
    {
        if (_readyButton == null) return;
        _readyButton.text = isReady ? "CANCEL" : "READY";
        _readyButton.EnableInClassList("btn--active", isReady);
    }

    private static void ApplyData(VisualElement card, LobbyPlayerData data)
    {
        card.Q<Label>("playerName").text = string.IsNullOrEmpty(data.PlayerName.ToString()) ? "Player" : data.PlayerName.ToString();

        var roleTag = card.Q<Label>("roleTag");
        roleTag.text = data.IsHost ? "HOST" : "CHALLENGER";

        card.Q<Label>("readyLabel").text = data.IsReady ? "Ready" : "Not ready";
        card.Q<VisualElement>("readyBadge").EnableInClassList("ready-badge--ready", data.IsReady);

        card.EnableInClassList("player-card--ready", data.IsReady);
        card.EnableInClassList("player-card--local", data.IsLocal);
    }

    private async void LoadAvatar(ulong steamId, Image target, VisualElement card)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.IsInitialized) return;

        Texture2D tex = await SteamManager.Instance.GetOrCacheAvatarAsync(steamId);

        // The card may have been removed while the avatar was loading
        if (tex != null && card.panel != null && _cards.ContainsKey(steamId))
            target.image = tex;
    }
}
