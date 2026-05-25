using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BusTerminal.Api.Domain;

// Spec 004 / FR-012. Namespaced extension surface. Keys are vendor-prefixed
// (`<vendor>:<key>`). Values are arbitrary structured JSON preserved as JsonElement.
// The reserved `__indexable` key carries per-extension search-indexing inclusion
// control.
//
// Serialization is handled by ExtensionsJsonConverter (Domain/Serialization/), which
// preserves structured values intact without nested re-serialization.
public sealed class Extensions : IReadOnlyDictionary<string, JsonElement>
{
    public const string IndexableMarkerKey = "__indexable";

    private static readonly Regex NamespacedKeyPattern = new(
        "^[a-z][a-z0-9-]*:[a-zA-Z][a-zA-Z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<string, JsonElement> _entries;

    public Extensions(IReadOnlyDictionary<string, JsonElement> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var key in entries.Keys)
        {
            if (key == IndexableMarkerKey)
            {
                continue;
            }

            if (!NamespacedKeyPattern.IsMatch(key))
            {
                throw new ArgumentException(
                    $"Extension key '{key}' must be namespaced as <vendor>:<name> (regex {NamespacedKeyPattern}). The reserved '{IndexableMarkerKey}' sibling is the only exception.",
                    nameof(entries));
            }
        }

        _entries = entries;
    }

    public static Extensions Empty { get; } = new(new Dictionary<string, JsonElement>(0));

    public static bool IsValidKey(string key) =>
        key == IndexableMarkerKey || NamespacedKeyPattern.IsMatch(key);

    public JsonElement this[string key] => _entries[key];
    public IEnumerable<string> Keys => _entries.Keys;
    public IEnumerable<JsonElement> Values => _entries.Values;
    public int Count => _entries.Count;
    public bool ContainsKey(string key) => _entries.ContainsKey(key);
    public bool TryGetValue(string key, out JsonElement value) => _entries.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();
}
