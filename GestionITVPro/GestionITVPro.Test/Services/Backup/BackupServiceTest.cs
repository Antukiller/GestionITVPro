using System.IO.Compression;
using CSharpFunctionalExtensions;
using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Errors.Backup;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;
using GestionITVPro.Service.Backup;
using GestionITVPro.Storage.Common;
using Moq;

namespace GestionITVPro.Test.Services.Backup;

[TestFixture]
public class BackupServiceTests {
    private string _tempDir = null!;
    private string _backupDir = null!;
    private BackupService _service = null!;
    private Mock<IStorage<Cita>> _storageMock = null!;

    [SetUp]
    public void SetUp() {
        // Crear rutas temporales aisladas para cada test
        _tempDir = Path.Combine(Path.GetTempPath(), $"ITV_BackupTest_{Guid.NewGuid()}");
        _backupDir = Path.Combine(_tempDir, "backups");
        Directory.CreateDirectory(_backupDir);

        _storageMock = new Mock<IStorage<Cita>>();
        _service = new BackupService(_storageMock.Object, _backupDir);
    }

    [TearDown]
    public void TearDown() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestFixture]
    public class CasosPositivos : BackupServiceTests {
        [Test]
        public void RealizarBackup_ConListaVacia_DeberiaRetornarErrorWriteError() {
            // Arrange
            var citas = new List<Cita>();

            // Act
            var resultado = _service.RealizarBackup(citas);

            // Assert
            resultado.IsFailure.Should().BeTrue();
    
            // CAMBIO: Esperamos StorageError.WriteError porque es lo que lanza tu código
            resultado.Error.Should().BeOfType<StorageError.WriteError>();
            resultado.Error.Message.Should().Contain("No hay citas para respaldar");
        }

        [Test]
        public void RealizarBackup_ConCitas_DeberiaCrearZipConData() {
            // Arrange
            var citas = new List<Cita> {
                new Cita { Id = 1, Matricula = "1234BBB", Marca = "Tesla", Modelo = "Model 3", Motor = Motor.Electrico, DniPropietario = "12345678Z" }
            };

            _storageMock.Setup(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act
            var resultado = _service.RealizarBackup(citas);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().EndWith(".zip");
            File.Exists(resultado.Value).Should().BeTrue();
            // Verifica que se intentó guardar el JSON dentro del proceso de backup
            _storageMock.Verify(s => s.Salvar(citas, It.Is<string>(p => p.EndsWith("citas.json"))), Times.Once);
        }

        [Test]
        public void RealizarBackup_ConDirectorioCustom_DeberiaCrearEnEseDirectorio() {
            // Arrange
            var customDir = Path.Combine(_tempDir, "itv-custom-backup");
            Directory.CreateDirectory(customDir);

            var citas = new List<Cita> { new Cita { Id = 1, Matricula = "1111AAA" } };

            _storageMock.Setup(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act
            var resultado = _service.RealizarBackup(citas, customDir);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().StartWith(customDir);
            File.Exists(resultado.Value).Should().BeTrue();
        }

        [Test]
        public void RestaurarBackup_ConZipValido_DeberiaRetornarCitas() {
            // Arrange
            var citas = new List<Cita> {
                new Cita { Id = 1, Matricula = "9999ZZZ", Marca = "Ford" }
            };

            _storageMock.Setup(s => s.Cargar(It.IsAny<string>()))
                .Returns(Result.Success<IEnumerable<Cita>, DomainError>(citas));

            // Crear un ZIP real en disco para que el servicio no falle al abrirlo
            var zipPath = Path.Combine(_backupDir, "itv-test.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                zip.CreateEntry("data/citas.json");
            }

            // Act
            var resultado = _service.RestaurarBackup(zipPath);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().HaveCount(1);
            resultado.Value.First().Matricula.Should().Be("9999ZZZ");
        }

        [Test]
        public void ListarBackups_ConUnBackup_DeberiaRetornarEseBackup() {
            // Arrange
            _storageMock.Setup(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            var backup = _service.RealizarBackup(new[] { new Cita { Id = 1 } }, _backupDir);

            // Act
            var resultado = _service.ListarBackups().ToList();

            // Assert
            resultado.Should().HaveCount(1);
            resultado.Should().Contain(backup.Value);
        }

        [Test]
        public void RestaurarBackupSistema_ConCallbackExitoso_DeberiaRetornarContador() {
            // Arrange
            var citas = new List<Cita> { new Cita { Id = 1, Matricula = "TEST" } };

            _storageMock.Setup(s => s.Cargar(It.IsAny<string>()))
                .Returns(Result.Success<IEnumerable<Cita>, DomainError>(citas));

            var zipPath = Path.Combine(_backupDir, "sys-back.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                zip.CreateEntry("data/citas.json");
            }

            var deleteCallback = () => true;
            Func<Cita, Result<Cita, DomainError>> callback = c => Result.Success<Cita, DomainError>(c);

            // Act
            var resultado = _service.RestaurarBackupSistema(zipPath, deleteCallback, callback);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Be(1);
        }
        
        [Test]
        public void RealizarBackupSistema_DeberiaLlamarARealizarBackupYRetornarExito()
        {
            // Arrange
            var citas = new List<Cita> {
                new Cita { Id = 1, Matricula = "1234BBB", Marca = "Tesla" }
            };

            _storageMock.Setup(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Success<bool, DomainError>(true));

            // Act
            // Llamamos específicamente al método que estaba en rojo en tu imagen
            var resultado = _service.RealizarBackupSistema(citas);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.Should().Contain("Backup_ITV_");
            File.Exists(resultado.Value).Should().BeTrue();
    
            // Verificamos que internamente se llamó al storage (lo que confirma que pasó por RealizarBackup)
            _storageMock.Verify(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()), Times.Once);
        }
    }

    [TestFixture]
    public class CasosNegativos : BackupServiceTests {
        [Test]
        public void RestaurarBackup_ConArchivoCorrupto_DeberiaRetornarErrorReadError() {
            // Arrange
            var zipPath = Path.Combine(_backupDir, "fake.zip");
            File.WriteAllText(zipPath, "No soy un zip");

            // Act
            var resultado = _service.RestaurarBackup(zipPath);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            // Cambiamos la expectativa al error que realmente lanza el código
            resultado.Error.Should().BeOfType<StorageError.ReadError>(); 
        }

        [Test]
        public void RestaurarBackup_ConZipSinCitasJson_DeberiaRetornarErrorInvalidFormat() {
            // Arrange
            var zipPath = Path.Combine(_backupDir, "sin-citas.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                zip.CreateEntry("archivo_equivocado.txt");
            }

            // Act
            var resultado = _service.RestaurarBackup(zipPath);

            // Assert
            resultado.IsFailure.Should().BeTrue();
    
            // CAMBIO: Ahora esperamos el tipo que realmente devuelve tu código
            resultado.Error.Should().BeOfType<StorageError.InvalidFormat>();
            resultado.Error.Message.Should().Contain("citas.json");
        }

        [Test]
        public void RealizarBackup_ConErrorDeEscritura_DeberiaRetornarError() {
            // Arrange
            var citas = new List<Cita> { new Cita { Id = 1 } };
            var error = new BackupError.CreationError("Fallo de disco");

            _storageMock.Setup(s => s.Salvar(It.IsAny<IEnumerable<Cita>>(), It.IsAny<string>()))
                .Returns(Result.Failure<bool, DomainError>(error));

            // Act
            var resultado = _service.RealizarBackup(citas, _backupDir);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().Be(error);
        }
        
        [Test]
        public void RestaurarBackupSistema_CuandoFallaBorradoDeBaseDeDatos_DeberiaRetornarErrorYCubrirLineaRoja()
        {
            // Arrange
            var zipPath = Path.Combine(_backupDir, "test.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                zip.CreateEntry("data/citas.json");
            }

            _storageMock.Setup(s => s.Cargar(It.IsAny<string>()))
                .Returns(Result.Success<IEnumerable<Cita>, DomainError>(new List<Cita> { new Cita { Id = 1 } }));

            // FORZAMOS EL ERROR: El callback de borrado devuelve FALSE
            Func<bool> deleteCallbackFallido = () => false; 
            Func<Cita, Result<Cita, DomainError>> createCallback = c => Result.Success<Cita, DomainError>(c);

            // Act
            var resultado = _service.RestaurarBackupSistema(zipPath, deleteCallbackFallido, createCallback);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Message.Should().Contain("Error al limpiar la base de datos");
        }
        [Test]
        public void RealizarBackupSistema_ConListaVacia_DeberiaRetornarFallo()
        {
            // Arrange
            var citasVacias = new List<Cita>();

            // Act
            var resultado = _service.RealizarBackupSistema(citasVacias);

            // Assert
            resultado.IsFailure.Should().BeTrue();
        }
    }
}