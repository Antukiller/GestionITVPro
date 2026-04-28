using CSharpFunctionalExtensions;
using FluentAssertions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using GestionITVPro.Service.ImportExport;
using GestionITVPro.Storage.Common;
using Moq;

namespace GestionITVPro.Test.Services.ImportExport;

[TestFixture]
public class ImportExportServiceTest {
    [SetUp]
    public void SetUp() {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ImportExportTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        _storageMock = new Mock<IStorage<Cita>>();
        _service = new ImportExportService(_storageMock.Object);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string _tempDir = null!;
    private ImportExportService _service = null!;
    private Mock<IStorage<Cita>> _storageMock = null!;

    [TestFixture]
    public class CasosPositios : ImportExportServiceTest {
        [Test]
        public void ExportarDatos_Citas_RetonarContador() {
            // Arrange
            var citas = new List<Cita>() {
                new Cita { Id = 1, Matricula = "1234-BBB" },
                new Cita { Id = 2, Matricula = "2345-MMM" },
                new Cita { Id = 3, Matricula = "6789-VVV" }
            };

            var path = Path.Combine(_tempDir, "export.json");
            _storageMock.Setup(c => c.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act 
            var resultado = _service.ExportarDatos(citas, path);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Be(3);
        }

        [Test]
        public void ImportDatos_ConArchivo_RetornarCitas() {
            // Arrange
            var c = new List<Cita>() {
                new Cita { Id = 1, Matricula = "1234-BBB" },
            };
            var path = Path.Combine(_tempDir, "import.json");

            _storageMock.Setup(c => c.Cargar(path))
                .Returns(Result.Success<IEnumerable<Cita>, DomainError>(c));

            // Act
            var result = _service.ImportarDatos(path);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
            result.Value.First().Matricula.Should().Be("1234-BBB");
        }

        [Test]
        public void ExportarDatosSistema_LlamarExportarDatosConRutaVacia() {
            // Arrange
            var c = new List<Cita>() {
                new Cita { Id = 1, Matricula = "1234-BBB" },
            };
            _storageMock.Setup(c => c.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act
            var r = _service.ExportarDatosSistema(c);

            // Assert
            r.IsSuccess.Should().BeTrue();
            _storageMock.Verify(c => c.Salvar(It.IsAny<IEnumerable<Cita>>(), string.Empty));
        }

        [Test]
        public void ImportarDatosSistema_ConRuta_LLamarImportarDatos() {
            var path = Path.Combine(_tempDir, "test.json");
            var c = new List<Cita> { new Cita { Id = 1 } };

            _storageMock.Setup(c => c.Cargar(path))
                .Returns(Result.Success<IEnumerable<Cita>, DomainError>(c));

            // Act
            var r = _service.ImportarDatosSistema(path);

            // Assert
            r.IsSuccess.Should().BeTrue();
            _storageMock.Verify(c => c.Cargar(path), Times.Once);
        }

        [Test]
        public void ExportarDatos_ConListaVacia_RetornarCero() {
            var c = new List<Cita>();
            var path = Path.Combine(_tempDir, "empty.json");

            _storageMock.Setup(c => c.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act
            var r = _service.ExportarDatos(c, path);

            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Should().Be(0);
        }

    }

    [TestFixture]
    public class CasosNegativos : ImportExportServiceTest {
        [Test]
        public void ImportarDatos_ConError_RetornarError() {
            // Arrange
            var path = Path.Combine(_tempDir, "no-existe.jsonj");
            var error = new TestError("File not found");

            _storageMock.Setup(c => c.Cargar(path))
                .Returns(Result.Failure<IEnumerable<Cita>, DomainError>(error));

            // Act
            var r = _service.ImportarDatos(path);

            // Assert
            r.IsFailure.Should().BeTrue();
        }

        [Test]
        public void ExportarDatos_ConError_RetornarError() {
            // Arrange
            var c = new List<Cita>() { new Cita { Id = 1 } };
            var path = Path.Combine(_tempDir, "no-existe.jsonj");
            var error = new TestError("File not found");

            _storageMock.Setup(c => c.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Failure<bool, DomainError>(error));

            // Act
            var r = _service.ExportarDatos(c, path);

            // Assert
            r.IsFailure.Should().BeTrue();
        }

        private record TestError(string Message) : DomainError(Message);
    }
}
    
