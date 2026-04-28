using FluentAssertions;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;
using GestionITVPro.Storage.Xml;

namespace GestionITVPro.Test.Storage.Xml;

[TestFixture]
public class GestionItvXmlStorageTest {
    [SetUp]
    public void SetUp() {
        _storage = new GestionItvXmlStorage();
        _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    private GestionItvXmlStorage _storage = null!;
    private string _tempPath = null!;

    [TestFixture]
    public class CasosPositivos {
        private GestionItvXmlStorage _storage = null!;
        private string _tempPath = null!;

        [SetUp]
        public void SetUp() {
            _storage = new GestionItvXmlStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void Salvar_ConDatosValidos_DeberiaGuardarCorrectamente() {
            // Arrange
            var vehiculos = new List<Cita> {
                new Cita { Matricula = "1234ABC", Marca = "Toyota", Modelo = "Corolla" },
                new Cita { Matricula = "5678DEF", Marca = "Ford", Modelo = "Focus" }
            };

            // Act
            var resultado = _storage.Salvar(vehiculos, _tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();
        }

        [Test]
        public void Cargar_ConArchivoExistente_DeberiaRetornarDatos() {
            // Arrange
            var vehiculos = new List<Cita> {
                new Cita { Matricula = "1234ABC", Marca = "Toyota", Modelo = "Corolla" }
            };
            _storage.Salvar(vehiculos, _tempPath);

            // Act
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().HaveCount(1);
            resultado.Value.First().Matricula.Should().Be("1234ABC");
            resultado.Value.First().Should().BeOfType<Cita>();
        }

        [Test]
        public void Salvar_ListaVacia_DeberiaCrearArchivoVacio() {
            // Arrange
            var vehiculos = new List<Cita>();

            // Act
            var resultado = _storage.Salvar(vehiculos, _tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();
        }
    }

    [TestFixture]
    public class CasosNegativos {
        private GestionItvXmlStorage _storage = null!;
        private string _tempPath = null!;

        [SetUp]
        public void SetUp() {
            _storage = new GestionItvXmlStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void Cargar_CuandoArchivoNoExiste_DeberiaRetornarError() {
            // Arrange & Act
            var resultado = _storage.Cargar("ruta/inexistente.xml");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.FileNotFound>();
            (resultado.Error as StorageError.FileNotFound)?.FilePath.Should().Be("ruta/inexistente.xml");
            resultado.Error.Message.Should().Contain("ruta/inexistente.xml");
        }

        [Test]
        public void Salvar_EnRutaInvalida_DeberiaRetornarError() {
            // Arrange
            var vehiculos = new List<Cita>();

            // Act
            var resultado = _storage.Salvar(vehiculos, "/ruta/invalida/archivo.xml");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.WriteError>();
            resultado.Error.Message.Should().Contain("Error al escribir");
        }
    }

    [TestFixture]
    public class CasosMixtos {
        private GestionItvXmlStorage _storage = null!;
        private string _tempPath = null!;

        [SetUp]
        public void SetUp() {
            _storage = new GestionItvXmlStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xml");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void SalvarYLeer_RoundTrip_DeberiaMantenerDatos() {
            // Arrange
            var original = new List<Cita> {
                new Cita { 
                    Matricula = "1111AAA", 
                    Marca = "Seat", 
                    Modelo = "Ibiza",
                    IsDeleted = false
                }
            };

            // Act
            var saveResult = _storage.Salvar(original, _tempPath);
            var loadResult = _storage.Cargar(_tempPath);

            // Assert
            saveResult.IsSuccess.Should().BeTrue();
            loadResult.IsSuccess.Should().BeTrue();
            loadResult.Value.Should().HaveCount(1);

            var vehiculo = loadResult.Value.First();
            vehiculo.Matricula.Should().Be("1111AAA");
            vehiculo.Marca.Should().Be("Seat");
        }

        [Test]
        public void Salvar_ConVehiculoEliminado_DeberiaMantenerEstadoEliminado() {
            // Arrange
            var vehiculos = new List<Cita> {
                new Cita {
                    Matricula = "0000DEL",
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow
                }
            };

            // Act
            _storage.Salvar(vehiculos, _tempPath);
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.First().IsDeleted.Should().BeTrue();
            resultado.Value.First().DeletedAt.Should().NotBeNull();
        }
    }
}