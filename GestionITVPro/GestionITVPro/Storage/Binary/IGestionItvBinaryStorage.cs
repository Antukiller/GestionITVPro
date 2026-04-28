using GestionITVPro.Models;
using GestionITVPro.Storage.Common;

namespace GestionITVPro.Storage.Binary;

/// <summary>
/// Contrato para persistir y cargar personas en formato binario.
/// Hereda de <see cref="IStorage{T}" /> con T = <see cref="Cita" />.
/// </summary>
public interface IGestionItvBinaryStorage : IStorage<Cita> { }