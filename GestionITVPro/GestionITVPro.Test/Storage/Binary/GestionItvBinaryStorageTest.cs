using System.IO;
using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;
using GestionITVPro.Storage.Binary;

namespace GestionITVPro.Test.Storage.Binary;

[TestFixture]
public class GestionItvBinaryStorageTests {
    [SetUp]
    public void SetUp() {
        _storage = new GestionItvBinaryStorage();
        _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    private GestionItvBinaryStorage _storage = null!;
    private string _tempPath = null!;

    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvBinaryStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvBinaryStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void Salvar_ConDatosValidos_DeberiaGuardarCorrectamente() {
            // Arrange
            var vehiculos = new List<Cita> {
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                }
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
                new Cita {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                },
            };
            _storage.Salvar(vehiculos, _tempPath);

            // Act
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().HaveCount(1);
            resultado.Value.First().Matricula.Should().Be("1234-BBB");
            resultado.Value.First().Should().BeOfType<Cita>();
            (resultado.Value.First() as Cita)!.Cilindrada.Should().Be(3000);
        }
    }

    [TestFixture]
    public class CasosNegativos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvBinaryStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvBinaryStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void Cargar_CuandoArchivoNoExiste_DeberiaRetornarError() {
            // Arrange & Act
            var resultado = _storage.Cargar("ruta/inexistente.bin");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.FileNotFound>();
            (resultado.Error as StorageError.FileNotFound)?.FilePath.Should().Be("ruta/inexistente.bin");
            resultado.Error.Message.Should().Contain("ruta/inexistente.bin");
        }

        [Test]
        public void Cargar_CuandoArchivoNoEsBinarioValido_DeberiaRetornarError() {
            // Arrange
            File.WriteAllText(_tempPath, "Este no es un archivo binario válido serializado");

            // Act
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.InvalidFormat>();
            resultado.Error.Message.Should().Contain("formato del archivo es inválido");
        }

        [Test]
        public void Salvar_EnRutaInvalida_DeberiaRetornarError() {
            // Arrange
            var personas = new List<Cita>();

            // Act
            var resultado = _storage.Salvar(personas, "/ruta/invalida/archivo.bin");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.WriteError>();
            resultado.Error.Message.Should().Contain("Error al escribir");
        }
    }

    [TestFixture]
    public class CasosMixtos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvBinaryStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvBinaryStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void SalvarYLeer_RoundTrip_DeberiaMantenerDatos() {
            // Arrange
            var original = new List<Cita> {
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
            _storage.Salvar(original, _tempPath);
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().HaveCount(2);

            var vehiculo = resultado.Value.First() as Cita;
            vehiculo!.Matricula.Should().Be("1234-BBB");

            var vehiculo2 = resultado.Value.Last() as Cita;
            vehiculo2!.Matricula.Should().Be("2345-BBC");
        }

        [Test]
        public void Salvar_ListaVacia_DeberiaCrearArchivoVacio() {
            // Arrange
            var personas = new List<Cita>();

            // Act
            var resultado = _storage.Salvar(personas, _tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();
        }

        [Test]
        public void SalvarYLeer_MultiplesVeces_DeberiaMantenerConsistencia() {
            // Arrange
            var v = new List<Cita>() {
                new Cita {
                Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                Motor = Motor.Diesel,
                DniPropietario = "23232323Q"
                }
            };

            // Act - Guardar y cargar múltiples veces
            _storage.Salvar(v, _tempPath);
            var resultado1 = _storage.Cargar(_tempPath);

            _storage.Salvar(resultado1.Value, _tempPath);
            var resultado2 = _storage.Cargar(_tempPath);

            // Assert
            resultado1.IsSuccess.Should().BeTrue();
            resultado2.IsSuccess.Should().BeTrue();
            resultado2.Value.Should().HaveCount(1);
            resultado2.Value.First().Matricula.Should().Be("1234-BBB");
        }
    }
}