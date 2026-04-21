using GestionITVPro.Models;
using GestionITVPro.Storage.Common;

namespace GestionITVPro.Storage.Json;

/// <summary>
/// Contrato para persistir y cargar personas en formato Json.
/// Hereda de <see cref="IStorage{T}"/> con T = <see cref="Vehiculo"/>.
/// </summary>
public interface IGestionItvJsonStorage : IStorage<Vehiculo> { }