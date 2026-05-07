using System.Globalization;
using System.IO;
using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Errors.Report;
using GestionITVPro.Models;
using GestionITVPro.Service.Report;

namespace GestionITVPro.Test.Services.Report;

[TestFixture]
public class ReportServiceTests {
    private ReportService _service = null!;
    private string _tempDirPath = null!;
    private CultureInfo _originalCulture = null!;

    [SetUp]
    public void SetUp() {
        _tempDirPath = Path.Combine(Path.GetTempPath(), $"ITV_Reports_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirPath);
        _service = new ReportService(_tempDirPath);

        // Forzar cultura es-ES para que las medias y fechas coincidan con el formato esperado
        _originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        CultureInfo.CurrentUICulture = new CultureInfo("es-ES");
    }

    [TearDown]
    public void TearDown() {
        CultureInfo.CurrentCulture = _originalCulture;
        if (Directory.Exists(_tempDirPath)) {
            try { Directory.Delete(_tempDirPath, true); } catch { }
        }
    }

    [TestFixture]
    public class Estadisticas : ReportServiceTests {
        [Test]
        public void GenerarInformeEstadistico_DeberiaCalcularMétricasCorrectamente() {
            // Arrange
            var hoy = DateTime.Today;
            var citas = new List<Cita> {
                // Cita para HOY (Gasolina)
                new() { 
                    Motor = Motor.Gasolina, 
                    FechaInspeccion = hoy, 
                    FechaItv = hoy.AddYears(-4) // Fecha matriculación
                },
                // Cita ATRASADA (Eléctrico)
                new() { 
                    Motor = Motor.Electrico, 
                    FechaInspeccion = hoy.AddDays(-1), 
                    FechaItv = hoy.AddYears(-2),
                    IsDeleted = false 
                },
                // Cita FUTURA (Diesel)
                new() { 
                    Motor = Motor.Diesel, 
                    FechaInspeccion = hoy.AddDays(2), 
                    FechaItv = hoy.AddYears(-10) 
                }
            };

            // Act
            var stats = _service.GenerarInformeEstadistico(citas);

            // Assert
            stats.TotalCitas.Should().Be(3);
            stats.Gasolina.Should().Be(1);
            stats.Electrico.Should().Be(1);
            stats.Diesel.Should().Be(1);

            // Ahora sí coincidirán
            stats.CitasParaHoy.Should().Be(1, "Solo la de gasolina tiene FechaInspeccion para hoy");
            stats.CitasAtrasadas.Should().Be(1, "Solo la eléctrica tiene FechaInspeccion de ayer");
        }
        [Test]
        public void GenerarInformeEstadistico_SinCitas_DeberiaManejarValoresNulos() {
            // Act
            var stats = _service.GenerarInformeEstadistico(new List<Cita>());

            // Assert
            stats.TotalCitas.Should().Be(0);
            stats.UltimaCitaProgramada.Should().BeNull();
        }
    }

    [TestFixture]
    public class GeneracionHtml : ReportServiceTests {
        [Test]
        public void GenerarInformeCitasHtml_DeberiaContenerEstructuraBasicaYBadgesEco() {
            // Arrange
            var citas = new List<Cita> {
                new() { Matricula = "ECO-001", Motor = Motor.Hibrido, Marca = "Toyota", Modelo = "Prius" },
                new() { Matricula = "GAS-002", Motor = Motor.Gasolina, Marca = "Ford", Modelo = "Focus" }
            };

            // Act
            var resultado = _service.GenerarInformeCitasHtml(citas);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            var html = resultado.Value;
            html.Should().Contain("Resumen de Citas ITV");
            html.Should().Contain("ECO-001");
            html.Should().Contain("badge-eco"); // Verifica que se aplicó el estilo ECO
            html.Should().Contain("Toyota Prius");
        }

        [Test]
        public void GenerarInformeMotoresHtml_DeberiaListarConteoPorMotor() {
            // Arrange
            var citas = new List<Cita> {
                new() { Motor = Motor.Diesel },
                new() { Motor = Motor.Diesel }
            };

            // Act
            var resultado = _service.GenerarInformeMotoresHtml(citas);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Contain("<li><strong>Diesel:</strong> 2</li>");
        }
    }

    [TestFixture]
    public class Persistencia : ReportServiceTests {
        [Test]
        public void GuardarInformeHtml_DeberiaCrearArchivoEnDisco() {
            // Arrange
            var html = "<h1>Test</h1>";
            var path = Path.Combine(_tempDirPath, "test.html");

            // Act
            var resultado = _service.GuardarInformeHtml(html, path);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Be(html);
        }

        [Test]
        public void GuardarInformeHtml_ConErrorDeRuta_DeberiaRetornarDatabaseError() {
            // Act
            // Ruta inválida para forzar el catch
            var resultado = _service.GuardarInformeHtml("html", "Z:\\Ruta\\No\\Existente\\report.html");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            // Según tu código, devuelves CitaErrors.DatabaseError
            resultado.Error.Message.Should().Contain("No se pudo guardar el archivo");
        }

        [Test]
        public void GuardarInformePdf_DeberiaRetornarExitoSimulado() {
            // Act
            var resultado = _service.GuardarInformePdf("<html></html>", "test.pdf");

            // Assert
            resultado.IsSuccess.Should().BeTrue();
        }
    }
}