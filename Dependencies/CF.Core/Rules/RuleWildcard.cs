namespace CF.Core.Rules;

/// <summary>Matches whole values, with ordinal literals, single-character wildcards and runs of any length.</summary>
internal static class RuleWildcard
{
    public static bool Matches(ReadOnlySpan<char> value, ReadOnlySpan<char> pattern)
    {
        var firstStar = pattern.IndexOf('*');
        if(firstStar < 0)
            return value.Length == pattern.Length && MatchesAt(value, pattern);

        var lastStar = pattern.LastIndexOf('*');
        var prefix = pattern[..firstStar];
        var suffix = pattern[(lastStar + 1)..];
        if(value.Length < prefix.Length + suffix.Length || !MatchesAt(value, prefix) || !MatchesAt(value[^suffix.Length..], suffix))
            return false;

        value = value[prefix.Length..^suffix.Length];
        while(firstStar < lastStar)
        {
            var start = firstStar + 1;
            var length = pattern[start..].IndexOf('*');
            firstStar = start + length;
            if(length == 0)
                continue;

            var segment = pattern.Slice(start, length);
            var match = Find(value, segment);
            if(match < 0)
                return false;

            value = value[(match + length)..];
        }

        return true;
    }

    private static bool MatchesAt(ReadOnlySpan<char> value, ReadOnlySpan<char> pattern)
    {
        for(var index = 0; index < pattern.Length; index++)
        {
            if(pattern[index] != '?' && pattern[index] != value[index])
                return false;
        }

        return true;
    }

    private static int Find(ReadOnlySpan<char> value, ReadOnlySpan<char> segment)
    {
        if(segment.Length > value.Length)
            return -1;

        if(!segment.Contains('?'))
            return value.IndexOf(segment, StringComparison.Ordinal);

        // Each bit tracks a prefix of the segment that ends at the current character.
        // Updating all active prefixes together avoids restarting the scan at each possible match.
        const int wordSize = 64;
        var words = (segment.Length - 1) / wordSize + 1;
        var active = new ulong[words];
        var any = new ulong[words];
        var literals = new Dictionary<(int Word, char Letter), ulong>();
        for(var index = 0; index < segment.Length; index++)
        {
            var word = index / wordSize;
            var bit = 1UL << (index % wordSize);
            if(segment[index] == '?')
            {
                any[word] |= bit;
                continue;
            }

            var key = (word, segment[index]);
            literals[key] = literals.GetValueOrDefault(key) | bit;
        }

        var fullMatch = 1UL << ((segment.Length - 1) % wordSize);
        for(var index = 0; index < value.Length; index++)
        {
            var carry = 1UL;
            for(var word = 0; word < words; word++)
            {
                var next = active[word] >> (wordSize - 1);
                var accepts = any[word] | literals.GetValueOrDefault((word, value[index]));
                active[word] = ((active[word] << 1) | carry) & accepts;
                carry = next;
            }

            if((active[^1] & fullMatch) != 0)
                return index - segment.Length + 1;
        }

        return -1;
    }
}
