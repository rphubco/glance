namespace Glance.Services;

using Glance.Core;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class NotesService
{
    readonly string _path;
    Dictionary<string, string> _notes = new();

    public NotesService()
    {
        _path = Path.Combine(Globals.Interface.ConfigDirectory.FullName, "notes.json");
        Load();
    }

    public string Get(string key) => _notes.TryGetValue(key, out var n) ? n : "";

    public void Set(string key, string note)
    {
        if (string.IsNullOrWhiteSpace(note)) _notes.Remove(key);
        else _notes[key] = note;
        Save();
    }
    public bool Has(string? id, string? name, string? world)
    {
        if (!string.IsNullOrEmpty(id) && _notes.ContainsKey(id)) return true;
        if (name != null && world != null && _notes.ContainsKey($"{name}@{world}")) return true;
        return false;
    }

    void Load()
    {
        if (!File.Exists(_path)) return;
        try { _notes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new(); }
        catch { _notes = new(); }
    }



    void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_notes)); }
        catch { }
    }
}
