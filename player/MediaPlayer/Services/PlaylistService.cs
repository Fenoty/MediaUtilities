using System.Collections.ObjectModel;
using System.IO;
using MediaPlayer.Helpers;

namespace MediaPlayer.Services;

public class PlaylistService
{
    public ObservableCollection<string> Items { get; } = [];

    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private int _currentIndex = -1;

    public int CurrentIndex
    {
        get => _currentIndex;
        private set => _currentIndex = value;
    }

    public string? CurrentItem =>
        _currentIndex >= 0 && _currentIndex < Items.Count ? Items[_currentIndex] : null;

    public bool HasItems => Items.Count > 0;

    public event Action<string>? ItemSelected;
    public event Action? PlaylistChanged;

    public void AddFiles(IEnumerable<string> paths)
    {
        var added = false;
        foreach (var path in paths)
        {
            if (!MediaFileHelper.IsMediaFile(path) || !_paths.Add(path))
                continue;

            Items.Add(path);
            added = true;
        }

        if (added)
            PlaylistChanged?.Invoke();
    }

    public bool SelectIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
            return false;

        CurrentIndex = index;
        ItemSelected?.Invoke(Items[index]);
        return true;
    }

    public bool PlayItem(string path)
    {
        var index = Items.IndexOf(path);
        if (index < 0)
        {
            if (!MediaFileHelper.IsMediaFile(path) || !_paths.Add(path))
                return false;

            Items.Add(path);
            index = Items.Count - 1;
            PlaylistChanged?.Invoke();
        }

        return SelectIndex(index);
    }

    public bool PlayNext() => PlayNext(loop: true);

    public bool PlayNext(bool loop)
    {
        if (Items.Count == 0)
            return false;

        if (_currentIndex < 0)
            return SelectIndex(0);

        if (_currentIndex >= Items.Count - 1)
        {
            if (!loop)
                return false;

            return SelectIndex(0);
        }

        return SelectIndex(_currentIndex + 1);
    }

    public bool ReplayCurrent()
    {
        if (CurrentItem is not { } path)
            return false;

        ItemSelected?.Invoke(path);
        return true;
    }

    public bool PlayPrevious()
    {
        if (Items.Count == 0)
            return false;

        var prev = _currentIndex <= 0 ? Items.Count - 1 : _currentIndex - 1;
        return SelectIndex(prev);
    }
}
