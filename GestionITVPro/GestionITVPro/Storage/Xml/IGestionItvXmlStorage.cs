using GestionITVPro.Models;
using GestionITVPro.Storage.Common;

namespace GestionITVPro.Storage.Xml;

// <summary>
/// Contrato para persistir y cargar personas en formato XML.
/// Hereda de <see cref="IStorage{T}"/> con T = <see cref="Cita"/>.
/// </summary>
public interface IGestionItvXmlStorage : IStorage<Cita> { }