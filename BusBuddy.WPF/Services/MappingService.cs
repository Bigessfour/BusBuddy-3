using AutoMapper;
using BusBuddy.WPF.Mapping;
using BusBuddy.WPF.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Service for mapping between domain models and view models
    /// with performance tracking
    /// </summary>
    public class MappingService : IMappingService
    {
        private readonly IMapper _mapper;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly ILogger? _logger;

        public MappingService(IMapper mapper, ILogger? logger = null)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger;
            _performanceMonitor = new PerformanceMonitor(logger);
        }

        /// <summary>
        /// Maps a source object to a destination type with performance tracking
        /// </summary>
        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (source == null)
            {

                throw new ArgumentNullException(nameof(source));
            }


            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = _mapper.Map<TDestination>(source);
                return result;
            }
            finally
            {
                stopwatch.Stop();
                _logger?.Verbose("Mapped {SourceType} to {DestinationType} in {ElapsedMs}ms",
                    typeof(TSource).Name,
                    typeof(TDestination).Name,
                    stopwatch.ElapsedMilliseconds);
                _performanceMonitor.RecordMetric(
                    $"Map_{typeof(TSource).Name}To{typeof(TDestination).Name}",
                    stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Maps a collection of source objects to destination type with performance tracking
        /// </summary>
        public IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source)
        {
            if (source == null)
            {

                throw new ArgumentNullException(nameof(source));
            }

            // For empty collections, avoid unnecessary overhead

            if (!source.Any())
            {

                return Enumerable.Empty<TDestination>();
            }


            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = _mapper.Map<IEnumerable<TDestination>>(source);
                return result;
            }
            finally
            {
                stopwatch.Stop();
                _logger?.Verbose("Mapped collection of {SourceType} to {DestinationType} ({ItemCount} items) in {ElapsedMs}ms",
                    typeof(TSource).Name,
                    typeof(TDestination).Name,
                    source.Count(),
                    stopwatch.ElapsedMilliseconds);
                _performanceMonitor.RecordMetric(
                    $"MapCollection_{typeof(TSource).Name}To{typeof(TDestination).Name}",
                    stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Maps a source object to an existing destination object with performance tracking
        /// </summary>
        public void Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null)
            {

                throw new ArgumentNullException(nameof(source));
            }


            if (destination == null)
            {

                throw new ArgumentNullException(nameof(destination));
            }


            var stopwatch = Stopwatch.StartNew();
            try
            {
                _mapper.Map(source, destination);
            }
            finally
            {
                stopwatch.Stop();
                _logger?.Verbose("Mapped {SourceType} to existing {DestinationType} in {ElapsedMs}ms",
                    typeof(TSource).Name,
                    typeof(TDestination).Name,
                    stopwatch.ElapsedMilliseconds);
                _performanceMonitor.RecordMetric(
                    $"MapToExisting_{typeof(TSource).Name}To{typeof(TDestination).Name}",
                    stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Gets performance metrics for the mapping operations
        /// </summary>
        public string GetPerformanceReport()
        {
            return _performanceMonitor.GenerateReport();
        }
    }

    /// <summary>
    /// Interface for mapping service
    /// </summary>
    public interface IMappingService
    {
        TDestination Map<TSource, TDestination>(TSource source);
        IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source);
        void Map<TSource, TDestination>(TSource source, TDestination destination);
        string GetPerformanceReport();
    }

    /// <summary>
    /// Extension methods for AutoMapper configuration
    /// </summary>
    public static class AutoMapperExtensions
    {
        /// <summary>
        /// Adds AutoMapper to the service collection with proper configuration
        /// </summary>
        public static IServiceCollection AddAutoMapperServices(this IServiceCollection services)
        {
            // Register AutoMapper
            services.AddAutoMapper(config =>
            {
                config.AddProfile<MappingProfile>();
            }, typeof(MappingProfile).Assembly);

            // Register mapping service
            services.AddSingleton<IMappingService, MappingService>();

            return services;
        }
    }
}
