using System.Text;
using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Storage;
using GestionITVPro.Models;
using GestionITVPro.Storage.Csv;

namespace GestionITVPro.Test.Storage.Csv;

[TestFixture]
public class GestionItvCsvStorageTest {
    [SetUp]
    public void SetUp() {
        _storage = new GestionItvCsvStorage();
        _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }

    private GestionItvCsvStorage _storage = null!;
    private string _tempPath = null!;

    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvCsvStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvCsvStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void Salvar_ConDatosValidos_DeberiaGuardarCorrectamente() {
            // Arrange
            var vehiculos = new List<Vehiculo> {
                new Vehiculo {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                },

                new Vehiculo {
                    Id = 2, Matricula = "2345-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 0,
                    Motor = Motor.Electrico,
                    DniPropietario = "18981710V"
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
            var personas = new List<Vehiculo> {
                new Vehiculo {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                }
            };
            _storage.Salvar(personas, _tempPath);

            // Act
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().HaveCount(1);
            resultado.Value.First().Matricula.Should().Be("1234-BBB");
            resultado.Value.First().Should().BeOfType<Vehiculo>();
            (resultado.Value.First() as Vehiculo)!.Cilindrada.Should().Be(3000);
        }
    }

    [TestFixture]
    public class CasosNegativos {
        [SetUp]
        public void SetUp() {
            _storage = new GestionItvCsvStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvCsvStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void Cargar_CuandoArchivoNoExiste_DeberiaRetornarError() {
            // Arrange & Act
            var resultado = _storage.Cargar("ruta/inexistente.csv");

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.FileNotFound>();
            (resultado.Error as StorageError.FileNotFound)?.FilePath.Should().Be("ruta/inexistente.csv");
            resultado.Error.Message.Should().Contain("ruta/inexistente.csv");
        }

        [Test]
        public void Cargar_CuandoArchivoTieneFormatoInvalido_DeberiaRetornarError() {
            // Arrange - usar separador incorrecto (el servicio espera ;)
            // Escribimos con comma pero el servicio espera punto y coma
            using var writer = new StreamWriter(_tempPath, false, Encoding.UTF8);
            writer.WriteLine(
                "Id;Dni;Nombre;Apellidos;Email;Telefono;Imagen;FechaNacimiento;Ciclo;Curso;Tipo;Calificacion;Experiencia;IsDeleted;Direccion");
            writer.WriteLine(
                "1;A;Juan;Perez;juan@test.com;123;img.png;2020-01-01;DAM;1;Estudiante;7.5;2024-01-01;false;Calle 1");
            writer.WriteLine(
                "2;B;Ana;Garcia;ana@test.com;456;img2.jpg;2019-01-01;DAW;2;Docente;;2024-01-01;false;Calle 2");

            // Act
            var resultado = _storage.Cargar(_tempPath);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<StorageError.InvalidFormat>();
        }

        [Test]
        public void Salvar_EnRutaInvalida_DeberiaRetornarError() {
            // Arrange
            var personas = new List<Vehiculo>();

            // Act
            var resultado = _storage.Salvar(personas, "/ruta/invalida/archivo.csv");

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
            _storage = new GestionItvCsvStorage();
            _tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        }

        [TearDown]
        public void TearDown() {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        private GestionItvCsvStorage _storage = null!;
        private string _tempPath = null!;

        [Test]
        public void SalvarYLeer_RoundTrip_DeberiaMantenerDatos() {
            // Arrange
            var original = new List<Vehiculo> {
                new Vehiculo {
                    Id = 1, Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000,
                    Motor = Motor.Diesel,
                    DniPropietario = "23232323Q"
                },

                new Vehiculo {
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

            var estudiante = resultado.Value.First() as Vehiculo;
            estudiante!.Matricula.Should().Be("1234-BBB");

            var docente = resultado.Value.Last() as Vehiculo;
            docente!.Matricula.Should().Be("2345-BBC");
        }

        [Test]
        public void Salvar_ListaVacia_DeberiaCrearArchivoVacio() {
            // Arrange
            var personas = new List<Vehiculo>();

            // Act
            var resultado = _storage.Salvar(personas, _tempPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            File.Exists(_tempPath).Should().BeTrue();
        }
    }
}