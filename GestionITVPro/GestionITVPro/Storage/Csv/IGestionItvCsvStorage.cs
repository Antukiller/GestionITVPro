using GestionITVPro.Models;
using GestionITVPro.Storage.Common;

namespace GestionITVPro.Storage.Csv;

/// <summary>
///  Contrato para persistir y cargar personas en formato CSV.
///  Hereda de <see cref="IStorage{T}" /> con T = <see cref="Vehiculo" />.
/// </summary>
public interface IGestionItvCsvStorage : IStorage<Vehiculo> { }