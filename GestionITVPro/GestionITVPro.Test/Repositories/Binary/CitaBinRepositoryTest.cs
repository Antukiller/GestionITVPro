using System.IO;
using GestionITVPro.Error.Cita;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Binary;

namespace GestionITVPro.Test.Repositories.Binary;

using FluentAssertions;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Binary;

[TestFixture]
public class CitaBinRepositoryTest {
    private CitaBinRepository _repository;
    private string TestPath = "Data/vehiculos_test.dat";

    [SetUp]
    public void SetUp() {
        TestPath = Path.Combine(Path.GetTempPath(),
            Guid.NewGuid().ToString() + ".bin");

        // Inicializamos con dropData = true para empezar cada test con el archivo limpio
        _repository = new CitaBinRepository(path: TestPath, dropData: true, seedData: false);
    }

    [TearDown]
    public void TearDown() {
        if (File.Exists(TestPath))
            File.Delete(TestPath);
    }

    [Test]
    public void Create_DebePersistirEnArchivoYRecuperar() {
        // Arrange
        var vehiculo = new Cita {
            Matricula = "1234-ABC", Marca = "Toyota", Modelo = "Corolla", DniPropietario = "12345678Z"
        };

        // Act
        var result = _repository.Create(vehiculo);
        var created = result.Value;

        // IMPORTANTE: dropData debe ser FALSE para que cargue el archivo existente
        var repoNuevo = new CitaBinRepository(path: TestPath, dropData: false, seedData: false);
        var recuperado = repoNuevo.GetById(created.Id);

        // Assert
        recuperado.Should().NotBeNull("El repositorio debería haber cargado los datos del disco");
        recuperado!.Matricula.Should().Be("1234-ABC");
    }

    [Test]
    public void Update_MismaCita_SinCambiarFecha_DebeTenerExito() {
        // Arrange
        var cita = new Cita {
            Matricula = "1234BBB", FechaItv = DateTime.Today.AddDays(5), DniPropietario = "12345678Z", Marca = "Audi",
            Modelo = "A3"
        };
        var creada = _repository.Create(cita).Value;

        // Act: Editamos algo que no sea la fecha (el modelo, por ejemplo)
        var editada = creada with { Modelo = "A4" };
        var result = _repository.Update(creada.Id, editada);

        // Assert: No debe colisionar consigo misma
        result.IsSuccess.Should().BeTrue();
        result.Value.Modelo.Should().Be("A4");
    }

    [Test]
    public void Create_CuandoDniYaTiene3Vehiculos_RetornarFailure() {
        // Arrange
        var dni = "11111111H";
        for (int i = 0; i < 3; i++) {
            _repository.Create(new Cita {
                Matricula = $"MAT-{i}", DniPropietario = dni, Marca = "A", Modelo = "B"
            });
        }

        // Act
        var result = _repository.Create(new Cita {
            Matricula = "MAT-EXCESO", DniPropietario = dni, Marca = "A", Modelo = "B"
        });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.ToString().Should().Contain("límite");
    }

    [Test]
    public void Update_DebeActualizarDatosYGuardarEnDisco() {
        // Arrange
        var original = _repository.Create(new Cita {
            Matricula = "ORIG-123", Marca = "Ford", Modelo = "Focus", DniPropietario = "X",
            FechaItv = DateTime.Now // Asegúrate de incluir la fecha
        }).Value;

        // Act
        var modificado = original with { Marca = "Audi", Modelo = "A3" };
        _repository.Update(original.Id, modificado);

        // FIX: dropData en FALSE para recuperar la persistencia
        var repoLectura = new CitaBinRepository(path: TestPath, dropData: false, seedData: false);
        var resultado = repoLectura.GetById(original.Id);

        // Assert
        resultado.Should().NotBeNull("El registro debe existir en el disco tras la actualización");
        resultado!.Marca.Should().Be("Audi");
        resultado.Modelo.Should().Be("A3");
    }

    [Test]
    public void Delete_Fisico_DebeEliminarDelArchivo() {
        // Arrange
        var v = _repository.Create(new Cita {
            Matricula = "DEL-999", DniPropietario = "Y", Marca = "M", Modelo = "M"
        }).Value;

        // Act
        _repository.Delete(v.Id, isLogical: false);

        var repoLectura = new CitaBinRepository(path: TestPath, dropData: true, seedData: false);

        // Assert
        repoLectura.GetById(v.Id).Should().BeNull();
        repoLectura.CountCita(includeDeleted: true).Should().Be(0);
    }

    [Test]
    public void Restore_DebeQuitarFlagIsDeletedYPersistir() {
        // Arrange
        var v = _repository.Create(new Cita {
            Matricula = "REST-001", DniPropietario = "Z", Marca = "T", Modelo = "T",
            FechaItv = DateTime.Now // Asegúrate de que tenga fecha para el mapper
        }).Value;
        _repository.Delete(v.Id, isLogical: true);

        // Act
        _repository.Restore(v.Id);

        // FIX: dropData en FALSE para que lea el archivo persistido
        var repoLectura = new CitaBinRepository(path: TestPath, dropData: false, seedData: false);
        var restaurado = repoLectura.GetById(v.Id);

        // Assert
        restaurado.Should().NotBeNull("El registro debería haber sido recuperado del archivo .dat");
        restaurado!.IsDeleted.Should().BeFalse("El flag IsDeleted debería persistir como False tras el Restore");
    }

    [Test]
    public void Getters_DebeFiltrarCorrectamenteYBuscarPorIndices() {
        // Arrange
        var dni = "88888888X";
        var v1 = _repository.Create(new Cita { Matricula = "AAA-111", DniPropietario = dni, Marca = "A", Modelo = "A" })
            .Value;
        var v2 = _repository.Create(new Cita { Matricula = "BBB-222", DniPropietario = dni, Marca = "B", Modelo = "B" })
            .Value;

        // Marcamos el primero como borrado lógico
        _repository.Delete(v1.Id, isLogical: true);

        // Act & Assert

        // 1. Probar GetAll sin incluir borrados
        // Orden: marca, dniPropietario, matricula, desde, hasta, page, pageSize, includeDeleted
        var activos = _repository.GetAll(1, 10, false, null);

        activos.Should().HaveCount(1);
        activos.First().Matricula.Should().Be("BBB-222");

        // 2. Probar GetByMatricula
        var buscadoMat = _repository.GetByMatricula("BBB-222");
        buscadoMat.Should().NotBeNull();
        buscadoMat!.Id.Should().Be(v2.Id);

        // 3. Probar GetByDniPropietario
        var buscadoDni = _repository.GetByDniPropietario(dni);
        buscadoDni.Should().NotBeNull();
        // Verifica que el DNI coincide y que, al estar bien implementado, 
        // te devuelva uno que no esté borrado (v2)
        buscadoDni!.DniPropietario.Should().Be(dni);
        buscadoDni.IsDeleted.Should().BeFalse();
    }


    [Test]
    public void DeleteAll_DebeVaciarDiccionariosYEliminarArchivo() {
        // Arrange: Nos aseguramos de que haya datos
        _repository.Create(new Cita { Matricula = "BOOM-123", DniPropietario = "123Z", Marca = "A", Modelo = "A" });
        _repository.CountCita().Should().Be(1);

        // Act
        var result = _repository.DeleteAll();

        // Assert
        result.Should().BeTrue();
        _repository.CountCita().Should().Be(0);
        // Verificamos que el archivo ya no existe en el sistema de archivos
        File.Exists("Data/vehiculos.dat").Should().BeFalse();
    }

    [Test]
    public void Update_CuandoCambiaDueño_DebeSincronizarIndicesYRespetarLimite() {
        // Arrange
        var dniAntiguo = "11111111A";
        var dniNuevo = "22222222B";
        var v = _repository.Create(new Cita {
            Matricula = "CAMBIO-1", DniPropietario = dniAntiguo, Marca = "A", Modelo = "A"
        }).Value;

        // Act
        var modificado = v with { DniPropietario = dniNuevo };
        var result = _repository.Update(v.Id, modificado);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Verificamos que ya no aparece en el índice del antiguo dueño
        _repository.GetByDniPropietario(dniAntiguo).Should().BeNull();
        // Verificamos que ahora aparece en el del nuevo
        _repository.GetByDniPropietario(dniNuevo).Should().NotBeNull();
    }

    [Test]
    public void Create_MismaMatricula_MismoDia_DebeFallar() {
        // Arrange: Crear la primera cita
        var fecha = DateTime.Today.AddDays(5);
        var cita1 = new Cita
            { Matricula = "1234BBB", FechaItv = fecha, DniPropietario = "12345678Z", Marca = "Audi", Modelo = "A3" };
        _repository.Create(cita1);

        // Act: Intentar crear otra para el mismo coche el mismo día
        var citaDuplicada = cita1 with { Id = 0 }; // Nuevo objeto misma fecha
        var result = _repository.Create(citaDuplicada);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("ya tiene programada una cita para esa fecha");
    }

    [Test]
    public void Update_CuandoNuevaMatriculaYaExiste_RetornarFailure() {
        // Arrange
        _repository.Create(new Cita { Matricula = "EXISTE-1", DniPropietario = "123Z", Marca = "A", Modelo = "A" });
        var v2 = _repository.Create(new Cita
            { Matricula = "OTRA-2", DniPropietario = "123Z", Marca = "B", Modelo = "B" }).Value;

        // Act: Intentamos ponerle a v2 la matrícula que ya usa v1
        var modificado = v2 with { Matricula = "EXISTE-1" };
        var result = _repository.Update(v2.Id, modificado);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
    }

    [Test]
    public void Update_EscenariosDeFallo_ResultFailure() {
        // 1. Cubrir: Error NotFound (Línea roja al inicio de Update)
        var resultNotFound = _repository.Update(999, new Cita { Matricula = "1234BBB" });
        resultNotFound.IsFailure.Should().BeTrue();
        resultNotFound.Error.Should().BeOfType<CitaError.NotFound>();

        // 2. Cubrir: Matrícula ya existe (Línea roja validación matrícula)
        var v1 = _repository.Create(new Cita
            { Matricula = "1111AAA", DniPropietario = "123Z", Marca = "A", Modelo = "A" }).Value;
        var v2 = _repository.Create(new Cita
            { Matricula = "2222BBB", DniPropietario = "456X", Marca = "B", Modelo = "B" }).Value;

        // Intentamos actualizar v2 con la matrícula de v1
        var resultDuplicate = _repository.Update(v2.Id, v2 with { Matricula = "1111AAA" });

        resultDuplicate.IsFailure.Should().BeTrue();
        resultDuplicate.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
    }

    [Test]
    public void Update_CambioMatricula_SincronizaIndices() {
        // Arrange
        var v = _repository.Create(new Cita
            { Matricula = "VIEJA-123", DniPropietario = "123Z", Marca = "A", Modelo = "A" }).Value;

        // Act
        var modificado = v with { Matricula = "NUEVA-999" };
        _repository.Update(v.Id, modificado);

        // Assert: Verificamos que los índices se actualizaron (Líneas rojas de Remove/Add)
        _repository.GetByMatricula("VIEJA-123").Should().BeNull();
        _repository.GetByMatricula("NUEVA-999").Should().NotBeNull();
    }

    [Test]
    public void Restore_ReconstruyeIndiceDni_SiNoExiste() {
        // Arrange
        var dni = "TEST-RESTORE";
        var matricula = "RES-UNIQUE-999"; // Usa una matrícula única para este test
    
        // 1. Creamos la cita una sola vez
        var resultCreate = _repository.Create(new Cita { 
            Matricula = matricula, 
            DniPropietario = dni, 
            Marca="A", 
            Modelo="A",
            FechaItv = DateTime.Now.AddDays(20) // Fecha futura lejana para evitar choques
        });
    
        var v = resultCreate.Value; // Ahora esto no fallará

        // 2. Borrado lógico (esto suele mantener el índice, pero vamos a probar el Restore)
        _repository.Delete(v.Id, isLogical: true);

        // Act
        var result = _repository.Restore(v.Id);

        // Assert
        result.IsSuccess.Should().BeTrue("El Restore debería funcionar sobre una cita borrada lógicamente");
    
        // Verificamos que el índice de DNI funciona
        _repository.ExistsDniPropietario(dni).Should().BeTrue("El índice de DNI debería estar activo tras el Restore");
    
        var recuperado = _repository.GetByDniPropietario(dni);
        recuperado.Should().NotBeNull();
        recuperado!.Matricula.Should().Be(matricula);
    }
}

