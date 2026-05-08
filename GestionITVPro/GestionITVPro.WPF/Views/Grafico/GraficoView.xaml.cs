using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GestionITVPro.WPF.ViewModels.Graficos;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Serilog;
using SkiaSharp;
using Microsoft.Extensions.DependencyInjection;

namespace GestionITVPro.WPF.Views.Grafico;

public partial class GraficoView : Page 
{
    private readonly ILogger _logger = Log.ForContext<GraficoView>();
    private readonly GraficoViewModel _viewModel;

    public GraficoView() 
    {
        InitializeComponent();
        
        // Obtenemos el ViewModel del contenedor de servicios (ajusta según tu App.xaml.cs)
        // Si no usas DI, puedes hacer: _viewModel = new GraficoViewModel(citasService);
        _viewModel = App.ServiceProvider.GetRequiredService<GraficoViewModel>();
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    // DENTRO DE GraficoView.xaml.cs
    private void OnLoaded(object sender, RoutedEventArgs e) 
    {
        ActualizarGraficos();
    }

    private void ActualizarGraficos() 
    {
        try {
            if (_viewModel == null) return;

            // 1. Gráfico de Motores
            // Usamos directamente _viewModel.MotorStatsList que es pública
            MotorChart.Series = _viewModel.MotorStatsList.Select(m => new PieSeries<double> 
            {
                Values = new[] { m.Porcentaje },
                Name = m.Nombre,
                Fill = new SolidColorPaint(SKColor.Parse(m.ColorHex)),
                InnerRadius = 60
            }).ToArray();

            // 2. Gráfico de Calendario
            CalendarioChart.Series = new ISeries[] {
                new ColumnSeries<double> {
                    Values = _viewModel.CalendarioStatsList.Select(d => (double)d.Cantidad).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#00F2FF")),
                    Name = "Citas"
                }
            };

            CalendarioChart.XAxes = new Axis[] {
                new Axis { 
                    Labels = _viewModel.CalendarioStatsList.Select(d => d.Etiqueta).ToArray(),
                    LabelsPaint = new SolidColorPaint(SKColors.White)
                }
            };
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al dibujar gráficos");
        }
    }

    private void InitializeMotorChart() 
    {
        try {
            // Mapeamos tu lista de motores a series de LiveCharts
            var series = _viewModel.MotorStatsList.Select(m => new PieSeries<double> 
            {
                Values = new[] { m.Porcentaje },
                Name = m.Nombre,
                Fill = new SolidColorPaint(SKColor.Parse(m.ColorHex)),
                InnerRadius = 60 // Estilo Donut
            }).ToArray();

            // Si tienes un control llamado 'MotorChart' en tu XAML
            // MotorChart.Series = series; 
            
            _logger.Information("✅ Gráfico de motores configurado");
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error en InitializeMotorChart");
        }
    }

    private void InitializeCalendarioChart() 
    {
        try {
            var datos = _viewModel.CalendarioStatsList;
            var valores = datos.Select(d => (double)d.Cantidad).ToArray();
            var etiquetas = datos.Select(d => d.Etiqueta).ToArray();

            // Si tienes un control llamado 'CalendarioChart' en tu XAML
            /*
            CalendarioChart.Series = new ISeries[] {
                new ColumnSeries<double> {
                    Values = valores,
                    Fill = new SolidColorPaint(SKColor.Parse("#00F2FF")),
                    Name = "Citas"
                }
            };

            CalendarioChart.XAxes = new Axis[] {
                new Axis { Labels = etiquetas, LabelsPaint = new SolidColorPaint(SKColors.White) }
            };
            */
            
            _logger.Information("✅ Gráfico de calendario configurado");
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error en InitializeCalendarioChart");
        }
    }
}