using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BusBuddy.Core.Services.GoogleMaps;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace BusBuddy.WPF.Controls;

public sealed class PlaceAddressAppliedEventArgs : EventArgs
{
    public PlaceAddressAppliedEventArgs(PlaceAddressApplier.AppliedAddress applied)
    {
        Applied = applied;
    }

    public PlaceAddressApplier.AppliedAddress Applied { get; }
}

/// <summary>
/// SfTextBoxExt + Google Places Autocomplete popup. One output: <see cref="AddressText"/> plus
/// <see cref="AddressApplied"/>. Fail-open when the Maps key is missing.
/// </summary>
public partial class PlacesAddressBox : UserControl, IDisposable
{
    public static readonly DependencyProperty AddressTextProperty = DependencyProperty.Register(
        nameof(AddressText),
        typeof(string),
        typeof(PlacesAddressBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnAddressTextChanged));

    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(
        nameof(Watermark),
        typeof(string),
        typeof(PlacesAddressBox),
        new PropertyMetadata(null, OnWatermarkChanged));

    public static readonly DependencyProperty FieldAutomationNameProperty = DependencyProperty.Register(
        nameof(FieldAutomationName),
        typeof(string),
        typeof(PlacesAddressBox),
        new PropertyMetadata("Street address", OnFieldAutomationNameChanged));

    public static readonly DependencyProperty UseFormattedAddressProperty = DependencyProperty.Register(
        nameof(UseFormattedAddress),
        typeof(bool),
        typeof(PlacesAddressBox),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsSuggestionsEnabledProperty = DependencyProperty.Register(
        nameof(IsSuggestionsEnabled),
        typeof(bool),
        typeof(PlacesAddressBox),
        new PropertyMetadata(true));

    private readonly PlacesAddressAutocompleteCoordinator _coordinator;
    private bool _syncingText;
    private bool _disposed;

    public PlacesAddressBox()
    {
        InitializeComponent();
        var places = App.ServiceProvider?.GetService<IPlacesAutocompleteService>();
        _coordinator = new PlacesAddressAutocompleteCoordinator(places);
        SuggestionsList.ItemsSource = _coordinator.Suggestions;
        _coordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<PlaceAddressAppliedEventArgs>? AddressApplied;

    public string AddressText
    {
        get => (string)GetValue(AddressTextProperty);
        set => SetValue(AddressTextProperty, value);
    }

    public string? Watermark
    {
        get => (string?)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public string FieldAutomationName
    {
        get => (string)GetValue(FieldAutomationNameProperty);
        set => SetValue(FieldAutomationNameProperty, value);
    }

    public bool UseFormattedAddress
    {
        get => (bool)GetValue(UseFormattedAddressProperty);
        set => SetValue(UseFormattedAddressProperty, value);
    }

    public bool IsSuggestionsEnabled
    {
        get => (bool)GetValue(IsSuggestionsEnabledProperty);
        set => SetValue(IsSuggestionsEnabledProperty, value);
    }

    public void FocusAddressInput()
    {
        AddressInput.Focus();
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlacesAddressAutocompleteCoordinator.IsPopupOpen))
        {
            SuggestionsPopup.IsOpen = _coordinator.IsPopupOpen;
        }
    }

    private static void OnAddressTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PlacesAddressBox box || box._syncingText)
        {
            return;
        }

        var next = e.NewValue as string ?? string.Empty;
        if (box.AddressInput.Text == next)
        {
            return;
        }

        box._syncingText = true;
        try
        {
            box.AddressInput.Text = next;
        }
        finally
        {
            box._syncingText = false;
        }
    }

    private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlacesAddressBox box)
        {
            box.AddressInput.Watermark = e.NewValue as string;
        }
    }

    private static void OnFieldAutomationNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlacesAddressBox box && e.NewValue is string name && !string.IsNullOrWhiteSpace(name))
        {
            AutomationProperties.SetName(box.AddressInput, name);
        }
    }

    private void AddressInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        try
        {
            AddressText = AddressInput.Text ?? string.Empty;
        }
        finally
        {
            _syncingText = false;
        }

        if (IsSuggestionsEnabled)
        {
            _ = _coordinator.RefreshSuggestionsAsync(AddressInput.Text);
        }
    }

    private async void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SuggestionsList.SelectedItem is not PlaceAutocompleteSuggestion suggestion)
        {
            return;
        }

        try
        {
            var details = await _coordinator.ApplySuggestionAsync(suggestion).ConfigureAwait(true);
            if (details is null)
            {
                return;
            }

            var applied = PlaceAddressApplier.Apply(suggestion, details);
            _syncingText = true;
            try
            {
                var line = UseFormattedAddress
                    ? applied.SingleLine()
                    : applied.Street ?? applied.SingleLine();
                AddressInput.Text = line;
                AddressText = line;
            }
            finally
            {
                _syncingText = false;
            }

            AddressApplied?.Invoke(this, new PlaceAddressAppliedEventArgs(applied));
        }
        finally
        {
            SuggestionsList.SelectedItem = null;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _coordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
        _coordinator.Dispose();
        GC.SuppressFinalize(this);
    }
}
