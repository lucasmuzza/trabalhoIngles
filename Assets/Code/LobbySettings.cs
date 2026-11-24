using System;
using Unity.Collections;
using Unity.Netcode;

public struct AdverbRoundEntry : INetworkSerializable, IEquatable<AdverbRoundEntry>
{
    public FixedString128Bytes Phrase;
    // Store multiple valid adverbs separated by '|' or ',' (e.g. "quickly|fast")
    public FixedString64Bytes AcceptedAdverbsRaw;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Phrase);
        serializer.SerializeValue(ref AcceptedAdverbsRaw);
    }

    public bool Equals(AdverbRoundEntry other)
    {
        return Phrase.Equals(other.Phrase) &&
               AcceptedAdverbsRaw.Equals(other.AcceptedAdverbsRaw);
    }

    public override bool Equals(object obj) => obj is AdverbRoundEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Phrase, AcceptedAdverbsRaw);

    public bool MatchesAdverb(string guess)
    {
        if (string.IsNullOrWhiteSpace(guess) || AcceptedAdverbsRaw.IsEmpty)
            return false;

        string trimmedGuess = guess.Trim();
        string raw = AcceptedAdverbsRaw.ToString();
        string[] candidates = raw.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.Equals(trimmedGuess, candidates[i].Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
{
    public const int DefaultMaxPlayers1v1 = 2;

    public FixedString32Bytes PresetName;
    public FixedString32Bytes MapName;
    public int MaxPlayers;
    public bool IsPrivate;

    // 1v1 adverb phrases and accepted answers configured for the match
    public FixedList512Bytes<AdverbRoundEntry> PhrasesAndAnswers;

    public static LobbySettings CreateDefault1v1(string defaultMapName = "Arena_01", bool isPrivate = false)
    {
        LobbySettings settings = new LobbySettings
        {
            PresetName = "1v1 Adverbs",
            MapName = new FixedString32Bytes(defaultMapName ?? "Arena_01"),
            MaxPlayers = DefaultMaxPlayers1v1,
            IsPrivate = isPrivate,
            PhrasesAndAnswers = default
        };

        settings.AddChallenge("She runs _____ in the morning.", "quickly|fast");
        settings.AddChallenge("He finished the test _____.", "carefully");
        settings.AddChallenge("They arrived _____ for the class.", "early");

        return settings;
    }

    public bool AddChallenge(string phrase, string acceptedAdverbsPipeOrCommaSeparated)
    {
        if (PhrasesAndAnswers.Length >= PhrasesAndAnswers.Capacity)
            return false;

        PhrasesAndAnswers.Add(new AdverbRoundEntry
        {
            Phrase = new FixedString128Bytes(phrase ?? string.Empty),
            AcceptedAdverbsRaw = new FixedString64Bytes(acceptedAdverbsPipeOrCommaSeparated ?? string.Empty)
        });

        return true;
    }

    public void ClearChallenges()
    {
        PhrasesAndAnswers.Clear();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PresetName);
        serializer.SerializeValue(ref MapName);
        serializer.SerializeValue(ref MaxPlayers);
        serializer.SerializeValue(ref IsPrivate);

        int phraseCount = PhrasesAndAnswers.Length;
        serializer.SerializeValue(ref phraseCount);

        if (serializer.IsReader)
        {
            PhrasesAndAnswers.Clear();
            for (int i = 0; i < phraseCount; i++)
            {
                AdverbRoundEntry entry = default;
                entry.NetworkSerialize(serializer);
                PhrasesAndAnswers.Add(entry);
            }
        }
        else
        {
            for (int i = 0; i < phraseCount; i++)
            {
                AdverbRoundEntry entry = PhrasesAndAnswers[i];
                entry.NetworkSerialize(serializer);
            }
        }
    }

    public bool Equals(LobbySettings other)
    {
        if (PresetName != other.PresetName ||
            MapName != other.MapName ||
            MaxPlayers != other.MaxPlayers ||
            IsPrivate != other.IsPrivate ||
            PhrasesAndAnswers.Length != other.PhrasesAndAnswers.Length)
        {
            return false;
        }

        for (int i = 0; i < PhrasesAndAnswers.Length; i++)
        {
            if (!PhrasesAndAnswers[i].Equals(other.PhrasesAndAnswers[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object obj) => obj is LobbySettings other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(PresetName, MapName, MaxPlayers, IsPrivate);
}