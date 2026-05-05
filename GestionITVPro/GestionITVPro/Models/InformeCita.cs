namespace GestionITVPro.Models;


public sealed record InformeCita {
    public IEnumerable<Cita> ListadoCitas { get; init; } = Enumerable.Empty<Cita>();
    public int TotalCitas { get; init; }

    // --- Métricas de Motor ---
    public int Gasolina { get; init; }
    public int Diesel { get; init; }
    public int Hibrido { get; init; }
    public int Electrico { get; init; }
    public int TotalEco => Hibrido + Electrico;
    public double PorcentajeVehiculosEco => TotalCitas > 0 ? (double)TotalEco / TotalCitas * 100 : 0;

    // --- Métricas Operativas (Para el Dashboard y Progreso) ---
    public int CitasCompletadas { get; init; } 
    public int CitasPendientes => TotalCitas - CitasCompletadas;
    
    // Estos son los que usarán las barras de progreso del XAML
    public double PorcentajeCompletadas => TotalCitas > 0 ? (double)CitasCompletadas / TotalCitas * 100 : 0;
    public double PorcentajePendientes => TotalCitas > 0 ? (double)CitasPendientes / TotalCitas * 100 : 0;

    // --- Métricas de Fechas ---
    public int CitasParaHoy { get; init; }
    public int CitasAtrasadas { get; init; }
    public DateTime? UltimaCitaProgramada { get; init; }
}