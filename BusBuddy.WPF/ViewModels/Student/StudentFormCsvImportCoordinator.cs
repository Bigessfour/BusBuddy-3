using System.Windows.Media;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>
/// Picks a roster file and hands it to the Core importer. Parsing, header-shape detection, and the
/// duplicate-name skip all live in <see cref="ISeedDataService.ImportStudentsFromCsvAsync"/>; the WPF
/// layer must never grow a second interpretation of a roster file.
/// </summary>
public sealed class StudentFormCsvImportCoordinator
{
    private static readonly ILogger Logger = Log.ForContext<StudentFormCsvImportCoordinator>();

    private readonly StudentFormValidationCoordinator _validation;

    public StudentFormCsvImportCoordinator(StudentFormValidationCoordinator validation) =>
        _validation = validation;

    public async Task ImportCsvAsync()
    {
        try
        {
            Logger.Information("Starting CSV import process");

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import students from CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != true)
            {
                _validation.SetStatus("CSV import cancelled", Brushes.Gray);
                return;
            }

            _validation.IsValidating = true;
            _validation.SetStatus("Importing CSV data...", Brushes.Blue);

            var importer = App.ServiceProvider?.GetService<ISeedDataService>();
            if (importer is null)
            {
                Logger.Warning("CSV import unavailable — ISeedDataService is not registered");
                _validation.SetStatus("❌ Import unavailable (no seed service)", Brushes.Red);
                return;
            }

            var added = await importer.ImportStudentsFromCsvAsync(dialog.FileName);
            _validation.SetStatus(
                added == 0 ? "No new students imported" : $"✓ Imported {added} student(s)",
                added == 0 ? Brushes.Gray : Brushes.Green);

            try
            {
                WeakReferenceMessenger.Default.Send(new StudentsImportedMessage(added));
            }
            catch (Exception messengerEx)
            {
                Logger.Warning(messengerEx, "StudentsImportedMessage broadcast failed; open lists may show stale data");
            }

            Logger.Information("CSV import completed Added={Added}", added);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during CSV import");
            _validation.SetGlobalError($"CSV import failed: {ex.Message}");
            _validation.SetStatus("❌ Import failed", Brushes.Red);
        }
        finally
        {
            _validation.IsValidating = false;
        }
    }
}
