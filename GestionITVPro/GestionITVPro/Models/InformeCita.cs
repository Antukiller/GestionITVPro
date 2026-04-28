namespace GestionITVPro.Models;


/// <summary>
/// Contiene datos estadísticos consolidados y el análisis temporal de las citas.
/// </summary>
public sealed record InformeCita {
    /// <summary>
    /// Listado de citas analizadas, ordenadas cronológicamente.
    /// </summary>
    public IEnumerable<Cita> ListadoCitas { get; init; } = Enumerable.Empty<Cita>();

    public int TotalCitas { get; init; }

    // --- Métricas de Vehículo ---
    // Desglose detallado por el Enum que tienes
    public int Gasolina { get; init; }
    public int Diesel { get; init; }
    public int Hibrido { get; init; }
    public int Electrico { get; init; }

    // Propiedad calculada para "Combustión" (incluyendo híbridos)
    public int TotalCombustion => Gasolina + Diesel + Hibrido;

    // Propiedad calculada para "Etiqueta Eco/Cero"
    public int TotalEco => Hibrido + Electrico;
    public double CilindradaMedia { get; init; }

    // --- NUEVAS METRICAS DE FECHAS ---

    /// <summary>
    /// Citas programadas para el día de hoy.
    /// </summary>
    public int CitasParaHoy { get; init; }

    /// <summary>
    /// Citas cuya fecha ya ha pasado y no han sido procesadas (pendientes de gestión).
    /// </summary>
    public int CitasAtrasadas { get; init; }

    /// <summary>
    /// Citas programadas para los próximos 7 días.
    /// </summary>
    public int CitasProximaSemana { get; init; }

    /// <summary>
    /// La fecha más lejana en el calendario de citas actual.
    /// </summary>
    public DateTime? UltimaCitaProgramada { get; init; }

    // --- Cálculos Automáticos ---

    /// <summary>
    /// Indica si el sistema está saturado hoy (más de 10 citas, por ejemplo).
    /// </summary>
    public bool AlertaSaturacionHoy => CitasParaHoy > 10;

    // Cambiamos PorcentajeElectricos por PorcentajeEco para que coincida con el cálculo
    public double PorcentajeVehiculosEco => TotalCitas > 0 ? (double)TotalEco / TotalCitas * 100 : 0;
}