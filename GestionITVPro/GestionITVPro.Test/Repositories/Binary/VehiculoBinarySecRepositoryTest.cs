using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Binary;

namespace GestionITVPro.Test.Repositories.Binary;

using FluentAssertions;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Binary;

[TestFixture]
public class VehiculoBinarySecRepositoryTest {
    private VehiculoBinRepository _repository;
    private const string TestPath = "Data/vehiculos_test.dat";

    [SetUp]
    public void SetUp() {
        // Inicializamos con dropData = true para empezar cada test con el archivo limpio
        _repository = new VehiculoBinRepository(dropData: true);
    }

    [Test]
    public void Create_DebePersistirEnArchivoYRecuperar() {
        // Arrange
        var vehiculo = new Vehiculo { 
            Matricula = "1234-ABC", 
            Marca = "Toyota", 
            Modelo = "Corolla", 
            DniPropietario = "12345678Z" 
        };

        // Act
        var created = _repository.Create(vehiculo).Value;
        
        // Creamos una nueva instancia del repositorio para forzar la lectura del disco
        var repoNuevo = new VehiculoBinRepository(dropData: false);
        var recuperado = repoNuevo.GetById(created.Id);

        // Assert
        recuperado.Should().NotBeNull();
        recuperado!.Matricula.Should().Be("1234-ABC");
        recuperado.Id.Should().Be(created.Id);
    }

    [Test]
    public void Create_CuandoDniYaTiene3Vehiculos_RetornarFailure() {
        // Arrange
        var dni = "11111111H";
        for (int i = 0; i < 3; i++) {
            _repository.Create(new Vehiculo { 
                Matricula = $"MAT-{i}", DniPropietario = dni, Marca = "A", Modelo = "B" 
            });
        }

        // Act
        var result = _repository.Create(new Vehiculo { 
            Matricula = "MAT-EXCESO", DniPropietario = dni, Marca = "A", Modelo = "B" 
        });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.ToString().Should().Contain("Límite alcanzado");
    }

    [Test]
    public void Update_DebeActualizarDatosYGuardarEnDisco() {
        // Arrange
        var original = _repository.Create(new Vehiculo { 
            Matricula = "ORIG-123", Marca = "Ford", Modelo = "Focus", DniPropietario = "X" 
        }).Value;

        // Act
        var modificado = original with { Marca = "Audi", Modelo = "A3" };
        _repository.Update(original.Id, modificado);

        var repoLectura = new VehiculoBinRepository(dropData: false);
        var resultado = repoLectura.GetById(original.Id);

        // Assert
        resultado!.Marca.Should().Be("Audi");
        resultado.Modelo.Should().Be("A3");
    }

    [Test]
    public void Delete_Fisico_DebeEliminarDelArchivo() {
        // Arrange
        var v = _repository.Create(new Vehiculo { 
            Matricula = "DEL-999", DniPropietario = "Y", Marca = "M", Modelo = "M" 
        }).Value;

        // Act
        _repository.Delete(v.Id, isLogical: false);
        
        var repoLectura = new VehiculoBinRepository(dropData: false);
        
        // Assert
        repoLectura.GetById(v.Id).Should().BeNull();
        repoLectura.CountVehiculos(includeDeleted: true).Should().Be(0);
    }

    [Test]
    public void Restore_DebeQuitarFlagIsDeletedYPersistir() {
        // Arrange
        var v = _repository.Create(new Vehiculo { 
            Matricula = "REST-001", DniPropietario = "Z", Marca = "T", Modelo = "T" 
        }).Value;
        _repository.Delete(v.Id, isLogical: true);

        // Act
        _repository.Restore(v.Id);
        
        var repoLectura = new VehiculoBinRepository(dropData: false);
        var restaurado = repoLectura.GetById(v.Id);

        // Assert
        restaurado.Should().NotBeNull();
        restaurado!.IsDeleted.Should().BeFalse();
    }
    
    [Test]
    public void Getters_DebeFiltrarCorrectamenteYBuscarPorIndices() {
        // Arrange
        var dni = "88888888X";
        var v1 = _repository.Create(new Vehiculo { Matricula = "AAA-111", DniPropietario = dni, Marca="A", Modelo="A" }).Value;
        var v2 = _repository.Create(new Vehiculo { Matricula = "BBB-222", DniPropietario = dni, Marca="B", Modelo="B" }).Value;
        _repository.Delete(v1.Id, isLogical: true); // Marcamos uno como borrado

        // Act & Assert
        // 1. Probar GetAll sin incluir borrados (Líneas rojas en imagen)
        var activos = _repository.GetAll(page: 1, pageSize: 10, includeDeleted: false);
        activos.Should().HaveCount(1);
        activos.First().Matricula.Should().Be("BBB-222");

        // 2. Probar GetByMatricula (Línea roja en imagen)
        var buscadoMat = _repository.GetByMatricula("BBB-222");
        buscadoMat.Should().NotBeNull();
        buscadoMat!.Id.Should().Be(v2.Id);

        // 3. Probar GetByDniPropietario (Línea roja en imagen)
        var buscadoDni = _repository.GetByDniPropietario(dni);
        buscadoDni.Should().NotBeNull();
        // Al ser un índice de lista, devuelve el primero activo
        buscadoDni!.DniPropietario.Should().Be(dni);
    }
    
    
    [Test]
    public void DeleteAll_DebeVaciarDiccionariosYEliminarArchivo() {
        // Arrange: Nos aseguramos de que haya datos
        _repository.Create(new Vehiculo { Matricula = "BOOM-123", DniPropietario = "123Z", Marca="A", Modelo="A" });
        _repository.CountVehiculos().Should().Be(1);

        // Act
        var result = _repository.DeleteAll();

        // Assert
        result.Should().BeTrue();
        _repository.CountVehiculos().Should().Be(0);
        // Verificamos que el archivo ya no existe en el sistema de archivos
        File.Exists("Data/vehiculos.dat").Should().BeFalse();
    }
    
    [Test]
    public void Update_CuandoCambiaDueño_DebeSincronizarIndicesYRespetarLimite() {
        // Arrange
        var dniAntiguo = "11111111A";
        var dniNuevo = "22222222B";
        var v = _repository.Create(new Vehiculo { 
            Matricula = "CAMBIO-1", DniPropietario = dniAntiguo, Marca="A", Modelo="A" 
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
    public void Update_CuandoNuevaMatriculaYaExiste_RetornarFailure() {
        // Arrange
        _repository.Create(new Vehiculo { Matricula = "EXISTE-1", DniPropietario = "123Z", Marca="A", Modelo="A" });
        var v2 = _repository.Create(new Vehiculo { Matricula = "OTRA-2", DniPropietario = "123Z", Marca="B", Modelo="B" }).Value;

        // Act: Intentamos ponerle a v2 la matrícula que ya usa v1
        var modificado = v2 with { Matricula = "EXISTE-1" };
        var result = _repository.Update(v2.Id, modificado);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
    }
    
    [Test]
    public void Update_EscenariosDeFallo_ResultFailure() {
        // 1. Cubrir: Error NotFound (Línea roja al inicio de Update)
        var resultNotFound = _repository.Update(999, new Vehiculo { Matricula = "1234BBB" });
        resultNotFound.IsFailure.Should().BeTrue();
        resultNotFound.Error.Should().BeOfType<VehiculoError.NotFound>();

        // 2. Cubrir: Matrícula ya existe (Línea roja validación matrícula)
        var v1 = _repository.Create(new Vehiculo { Matricula = "1111AAA", DniPropietario = "123Z", Marca="A", Modelo="A" }).Value;
        var v2 = _repository.Create(new Vehiculo { Matricula = "2222BBB", DniPropietario = "456X", Marca="B", Modelo="B" }).Value;

        // Intentamos actualizar v2 con la matrícula de v1
        var resultDuplicate = _repository.Update(v2.Id, v2 with { Matricula = "1111AAA" });
    
        resultDuplicate.IsFailure.Should().BeTrue();
        resultDuplicate.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
    }
    
    [Test]
    public void Update_CambioMatricula_SincronizaIndices() {
        // Arrange
        var v = _repository.Create(new Vehiculo { Matricula = "VIEJA-123", DniPropietario = "123Z", Marca="A", Modelo="A" }).Value;

        // Act
        var modificado = v with { Matricula = "NUEVA-999" };
        _repository.Update(v.Id, modificado);

        // Assert: Verificamos que los índices se actualizaron (Líneas rojas de Remove/Add)
        _repository.GetByMatricula("VIEJA-123").Should().BeNull();
        _repository.GetByMatricula("NUEVA-999").Should().NotBeNull();
    }
    
    [Test]
    public void Restore_ReconstruyeIndiceDni_SiNoExiste() {
        // Arrange: Crear y borrar físicamente para limpiar índices, pero mantener en _porId
        var dni = "TEST-RESTORE";
        var v = _repository.Create(new Vehiculo { Matricula = "RES-111", DniPropietario = dni, Marca="A", Modelo="A" }).Value;
    
        // Forzamos que el DNI no esté en el índice (borrando todos y recreando el estado)
        _repository.DeleteAll(); 
        // Usamos el ID counter para asegurar que 'v' existe en _porId pero sus índices no
        _repository.Create(v with { Id = 1 });
        _repository.Delete(1, isLogical: true);

        // Act: Al restaurar, entrará en los bloques rojos de "new List" y "Add"
        var result = _repository.Restore(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repository.ExistsDniPropietario(dni).Should().BeTrue();
    }
}