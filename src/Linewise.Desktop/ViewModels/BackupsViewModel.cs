using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Abstractions;
using Linewise.Desktop.Resources;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The copies of the database, and the way back from one.
/// </summary>
/// <remarks>
/// A backup has been taken on every startup since phase 2, ten rolling copies kept, and the
/// restore routine has been tested since then. Neither had a button, so on the one machine
/// holding the only copy of the roster there was no way to use either.
/// </remarks>
public sealed partial class BackupsViewModel : ObservableObject
{
    private readonly Func<Task<IReadOnlyList<BackupDescriptor>>> _list;
    private readonly Func<Task<string>> _backup;
    private readonly Func<string, Task> _restore;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public BackupsViewModel(
        Func<Task<IReadOnlyList<BackupDescriptor>>> list,
        Func<Task<string>> backup,
        Func<string, Task> restore)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(restore);

        _list = list;
        _backup = backup;
        _restore = restore;
    }

    public ObservableCollection<BackupViewModel> Backups { get; } = [];

    public bool HasBackups => Backups.Count > 0;

    public async Task LoadAsync()
    {
        Backups.Clear();

        foreach (var backup in await _list().ConfigureAwait(true))
        {
            Backups.Add(new BackupViewModel(backup, this));
        }

        OnPropertyChanged(nameof(HasBackups));
    }

    [RelayCommand]
    private async Task BackUpNowAsync()
    {
        IsBusy = true;

        try
        {
            await _backup().ConfigureAwait(true);
            await LoadAsync().ConfigureAwait(true);

            Status = Strings.BackupTaken;
            Log.Information("Backup taken on request.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not take a backup.");
            Status = Strings.CouldNotBackUp;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Replaces the live database with a copy, and says so plainly first.
    /// </summary>
    /// <remarks>
    /// The one genuinely destructive thing in this application. Everything entered since the
    /// backup was taken is gone, and no undo reaches it, so the confirmation says that in
    /// those words rather than asking whether somebody is sure.
    /// <para>
    /// The application has to be restarted afterwards. The context was opened against the
    /// file that has just been replaced, and carrying on with it would write the old data
    /// back over the restored copy.
    /// </para>
    /// </remarks>
    internal async Task RestoreAsync(BackupViewModel backup)
    {
        IsBusy = true;

        try
        {
            await _restore(backup.Path).ConfigureAwait(true);

            Log.Warning("Database restored from a backup. The application must be restarted.");
            Status = Strings.RestoredRestartNeeded;
            NeedsRestart = true;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not restore from a backup.");
            Status = Strings.CouldNotRestore;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// A restore has happened and nothing on screen can be trusted until the application is
    /// started again.
    /// </summary>
    [ObservableProperty]
    private bool _needsRestart;
}

/// <summary>One copy of the database, and what it would cost to go back to it.</summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly BackupsViewModel _owner;

    public BackupViewModel(BackupDescriptor descriptor, BackupsViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
        Path = descriptor.Path;
        TakenAt = descriptor.TakenAtUtc.ToLocalTime();
        SizeInBytes = descriptor.SizeInBytes;
    }

    public string Path { get; }

    /// <summary>Shown in local time. UTC is right for storing and wrong for reading.</summary>
    public DateTime TakenAt { get; }

    public long SizeInBytes { get; }

    public string TakenAtLabel => Strings.BackupTakenAt(TakenAt);

    public string SizeLabel => Strings.BackupSize(Math.Max(1, SizeInBytes / 1024));

    [RelayCommand]
    private Task Restore() => _owner.RestoreAsync(this);
}
