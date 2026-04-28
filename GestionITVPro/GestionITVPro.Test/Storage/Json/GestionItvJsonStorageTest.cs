using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;
using GestionITVPro.Storage.Json;

namespace GestionITVPro.Test.Storage.Json;

[TestFixture]
public class GestionItvJsonStorageTest {
    [SetUp]
    public void SetUp() {
        _storage = new GestionItvJsonStorage();
        _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    private GestionItvJsonStorage _storage = null!;
    private string _tempPath = null!;

    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void Setup() {
            _storage = new GestionItvJsonStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvJsonStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void Salvar_ConDatosValido_GuardarCorrectamente() {
            // Arrange
            var v = new List<Cita> {
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                },

                new Cita {
                    Id = 2, Matricula = "2345-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 0,
                    Motor = Motor.Electrico,
                    DniPropietario = "18981710V"
                }
            };
            // Act
            var result = _storage.Salvar(v, _tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();

        }

        [Test]
        public void Cargar_ConArchivoExistente_RetornarDatos() {
            // Arrange
            var v = new List<Cita> {
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                }
            };
            _storage.Salvar(v, _tempPath);

            // Act
            var result = _storage.Cargar(_tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
            result.Value.First().Matricula.Should().Be("1234-BBB");
            result.Value.First().Should().BeOfType<Cita>();
        }

        [Test]
        public void Salvar_ListaVacia_CrearArhivoVacio() {
            // Arrange
            var v = new List<Cita>();

            // Act 
            var result = _storage.Salvar(v, _tempPath);

            // Assert
            result.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();
        }
    }

    // ... dentro de GestionItvJsonStorageTest ...

    [TestFixture]
    public class CassosNegativos { // Nota: Tienes un typo en "Cassos", podrías corregirlo a "Casos"
        private GestionItvJsonStorage _storage = null!;
        private string _tempPath = null!;

        [SetUp]
        public void SetUp() {
            _storage = new GestionItvJsonStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void Cargar_CuandoArchivoNoExiste_RetornarError() {
            // Arrange & Act
            var result = _storage.Cargar("ruta/inexistente.json");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<StorageError.FileNotFound>();
            (result.Error as StorageError.FileNotFound)?.FilePath.Should().Be("ruta/inexistente.json");
            result.Error.Message.Should().Contain("ruta/inexistente.json");
        }

        [Test]
        public void Salvar_EnRutaInvalida_DeberiaRetornarError() {
            // Arrange
            var vehiculos = new List<Cita>();

            // Act
            var resultado = _storage.Salvar(vehiculos, "/ruta/invalida/archivo.json");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.WriteError>();
            resultado.Error.Message.Should().Contain("Error al escribir");
        }
    } // <-- Esta llave DEBE cerrar la clase CassosNegativos DESPUÉS de los tests

    [TestFixture]
    public class CasosMixtos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvJsonStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvJsonStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void SalvarYLeer_RoundTrip_DeberiaMantenerDatos() {
            // Arrange
            var v = new List<Cita> {
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                }
            };

            // Act
            var saveResult = _storage.Salvar(v, _tempPath);
            var loadResult = _storage.Cargar(_tempPath);

            // Assert
            saveResult.IsSuccess.Should().BeTrue();
            loadResult.IsSuccess.Should().BeTrue();
            loadResult.Value.Should().HaveCount(1);

            var vehiculo = loadResult.Value.First() as Cita;
            vehiculo.Should().NotBeNull();
            vehiculo!.Matricula.Should().Be("1234-BBB");
            vehiculo.Marca.Should().Be("BMW");
            vehiculo.Cilindrada.Should().Be(3000);
        }


        [Test]
        public void Salvar_ConEstudianteEliminado_DeberiaMantenerEstadoEliminado() {
            // Arrange
            var v = new List<Cita> {
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "Eliminada",
                    IsDeleted = true, DeletedAt = DateTime.UtcNow
                }
            };

            // Act
            _storage.Salvar(v, _tempPath);
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.First().IsDeleted.Should().BeTrue();
            resultado.Value.First().DeletedAt.Should().NotBeNull();
        }
    }
}