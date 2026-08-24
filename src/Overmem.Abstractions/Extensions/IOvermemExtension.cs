using Microsoft.Extensions.DependencyInjection;

namespace Overmem.Abstractions.Extensions;

/// <summary>
/// Contract for Overmem extensions that register game-specific or domain-specific services.
/// </summary>
public interface IOvermemExtension
{
    /// <summary>
    /// Human-readable name of the extension (e.g. "PES 2021 Agenda").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Brief description of what the extension provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Register extension-specific services into the DI container.
    /// </summary>
    IServiceCollection RegisterServices(IServiceCollection services);
}
