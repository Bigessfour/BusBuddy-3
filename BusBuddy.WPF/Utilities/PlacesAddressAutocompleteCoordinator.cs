using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BusBuddy.Core.Services.GoogleMaps;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Debounced Places Autocomplete popup state shared by clerk address fields (StudentForm, SchoolDestinationForm).
/// </summary>
public sealed class PlacesAddressAutocompleteCoordinator : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<PlacesAddressAutocompleteCoordinator>();

    private readonly IPlacesAutocompleteService? _places;
    private readonly ObservableCollection<PlaceAutocompleteSuggestion> _suggestions = new();
    private CancellationTokenSource? _cts;
    private int _suppressAutocomplete;
    private string? _sessionToken;
    private bool _isPopupOpen;

    public PlacesAddressAutocompleteCoordinator(IPlacesAutocompleteService? places)
    {
        _places = places;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PlaceAutocompleteSuggestion> Suggestions => _suggestions;

    public bool IsEnabled => _places?.IsConfigured == true;

    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        private set
        {
            if (_isPopupOpen == value)
            {
                return;
            }

            _isPopupOpen = value;
            OnPropertyChanged();
        }
    }

    public static string CreateSessionToken() => Guid.NewGuid().ToString("N");

    public async Task RefreshSuggestionsAsync(string? input)
    {
        if (_places is null || !IsEnabled || Volatile.Read(ref _suppressAutocomplete) > 0)
        {
            await RunOnUiAsync(ClearSuggestions).ConfigureAwait(true);
            return;
        }

        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length < 3)
        {
            _sessionToken = null;
            await RunOnUiAsync(ClearSuggestions).ConfigureAwait(true);
            return;
        }

        _sessionToken ??= CreateSessionToken();

        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            await Task.Delay(350, cts.Token).ConfigureAwait(true);
            var suggestions = await _places
                .GetSuggestionsAsync(trimmed, _sessionToken, cts.Token)
                .ConfigureAwait(true);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            await RunOnUiAsync(() =>
            {
                _suggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    _suggestions.Add(suggestion);
                }

                IsPopupOpen = _suggestions.Count > 0;
            }).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Clerk still typing.
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Places address suggestions failed");
        }
    }

    public async Task<PlaceAddressDetails?> ApplySuggestionAsync(PlaceAutocompleteSuggestion? suggestion)
    {
        if (suggestion is null || _places is null)
        {
            return null;
        }

        try
        {
            Interlocked.Increment(ref _suppressAutocomplete);
            var token = _sessionToken;
            _sessionToken = null;
            var details = await _places
                .GetPlaceDetailsAsync(suggestion.PlaceId, token)
                .ConfigureAwait(true);
            await RunOnUiAsync(ClearSuggestions).ConfigureAwait(true);
            return details;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Places suggestion apply failed");
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _suppressAutocomplete);
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private void ClearSuggestions()
    {
        _suggestions.Clear();
        IsPopupOpen = false;
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Background).Task;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
