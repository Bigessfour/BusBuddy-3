using BusBuddy.Core.Services.RouteDetermination;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Route
{
    /// <summary>
    /// Year-start / transfer generate commands. Kept out of the main assignment file so generate
    /// does not keep growing the 2k-line route-building VM.
    /// </summary>
    public partial class RouteAssignmentViewModel
    {
        public event EventHandler? RoutesGenerated;

        public async Task GenerateRoutesAsync() =>
            await GenerateFleetAsync(FleetKind.HomeToSchool, preferSchoolWithStartTime: true).ConfigureAwait(true);

        public async Task GenerateTransferRoutesAsync() =>
            await GenerateFleetAsync(FleetKind.Transfer, preferSchoolWithStartTime: false).ConfigureAwait(true);

        private async Task GenerateFleetAsync(FleetKind fleet, bool preferSchoolWithStartTime)
        {
            if (_isGeneratingRoutes)
            {
                return;
            }

            _isGeneratingRoutes = true;
            RefreshCommandStates();
            try
            {
                StatusMessage = fleet == FleetKind.Transfer
                    ? "Generating transfer routes..."
                    : "Generating routes...";

                var outcome = await RouteGenerationCoordinator.GenerateAsync(
                        fleet,
                        SelectedRoute?.School,
                        preferSchoolWithStartTime,
                        _routeDetermination,
                        _destinations)
                    .ConfigureAwait(true);

                StatusMessage = outcome.StatusMessage;
                if (!outcome.Success || outcome.Result is null)
                {
                    return;
                }

                if (outcome.Result.Success)
                {
                    _map?.ApplyGenerationResult(outcome.Result);
                }

                await LoadDataFromServiceAsync().ConfigureAwait(true);

                var draft = AvailableRoutes.FirstOrDefault(r =>
                    r.RouteName.StartsWith("Draft-", StringComparison.OrdinalIgnoreCase));
                if (draft is not null)
                {
                    SelectedRoute = draft;
                }

                RoutesGenerated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Generate routes failed fleet={Fleet}", fleet);
                StatusMessage = $"Error generating routes: {ex.Message}";
            }
            finally
            {
                _isGeneratingRoutes = false;
                RefreshCommandStates();
            }
        }
    }
}
