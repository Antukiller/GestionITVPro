
using FluentAssertions;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Ado;
using GestionITVPro.Error.Vehiculo;

namespace GestionITVPro.Test.Repositories.Ado;

[TestFixture]
public class VehiculoAdoRepositoryTest {
    private VehiculoAdoRepository _repository;

    [SetUp]
    public void SetUp() {
        // Inicializamos con dropData: true para recrear la tabla en cada test
        _repository = new VehiculoAdoRepository(dropData: true, seedData: false);
    }

    [Test]
    public void Create_DebeInsertarEnBaseDeDatosYRecuperar() {
        // Arrange
        var vehiculo = new Vehiculo { 
            Matricula = "ADO-1234", 
            Marca = "Toyota", 
            Modelo = "Yaris", 
            Cilindrada = 1500,
            DniPropietario = "12345678Z" 
        };

        // Act
        var result = _repository.Create(vehiculo);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var recuperado = _repository.GetById(result.Value.Id);
        recuperado.Should().NotBeNull();
        recuperado!.Matricula.Should().Be("ADO-1234");
    }

    [Test]
    public void Create_CuandoMatriculaDuplicada_RetornaFailure() {
        // Arrange
        var v1 = new Vehiculo { Matricula = "DUP-111", Marca="A", Modelo="B", DniPropietario="1Z" };
        _repository.Create(v1);

        // Act
        var result = _repository.Create(v1); // Intentar insertar misma matrícula

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
    }

    [Test]
    public void Update_DebeModificarRegistroExistente() {
        // Arrange
        var v = _repository.Create(new Vehiculo { 
            Matricula = "UP-000", Marca = "Ford", Modelo = "Fiesta", DniPropietario = "X" 
        }).Value;

        // Act
        var modificado = v with { Marca = "Audi", Modelo = "A1" };
        var result = _repository.Update(v.Id, modificado);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var actualizado = _repository.GetById(v.Id);
        actualizado!.Marca.Should().Be("Audi");
        actualizado.Modelo.Should().Be("A1");
    }

    [Test]
    public void Delete_Logico_DebeMarcarComoBorrado() {
        // Arrange
        var v = _repository.Create(new Vehiculo { 
            Matricula = "LOGIC-1", DniPropietario = "Y", Marca = "M", Modelo = "M" 
        }).Value;

        // Act
        _repository.Delete(v.Id, isLogical: true);
        var recuperado = _repository.GetById(v.Id);

        // Assert
        recuperado.Should().NotBeNull();
        recuperado!.IsDeleted.Should().BeTrue();
        _repository.GetAll(includeDeleted: false).Should().BeEmpty();
    }

    [Test]
    public void Restore_DebeReactivarVehiculoBorrado() {
        // Arrange
        var v = _repository.Create(new Vehiculo { 
            Matricula = "RES-100", DniPropietario = "Z", Marca = "T", Modelo = "T" 
        }).Value;
        _repository.Delete(v.Id, isLogical: true);

        // Act
        var result = _repository.Restore(v.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsDeleted.Should().BeFalse();
        _repository.GetById(v.Id)!.IsDeleted.Should().BeFalse();
    }

    [Test]
    public void ValidarLimite3Vehiculos_DebeFallarAlCuarto() {
        // Arrange
        var dni = "LIMIT-333";
        for (int i = 0; i < 3; i++) {
            _repository.Create(new Vehiculo { 
                Matricula = $"MAT-{i}", DniPropietario = dni, Marca = "A", Modelo = "B" 
            });
        }

        // Act
        var result = _repository.Create(new Vehiculo { 
            Matricula = "MAT-CUARTA", DniPropietario = dni, Marca = "A", Modelo = "B" 
        });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.ToString().Should().Contain("tiene 3 vehículos");
    }

    [Test]
    public void GetByMatricula_DebeRetornarCorrecto() {
        // Arrange
        var mat = "BUSCAR-MAT";
        _repository.Create(new Vehiculo { Matricula = mat, DniPropietario = "123", Marca="A", Modelo="B" });

        // Act
        var encontrado = _repository.GetByMatricula(mat);

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Matricula.Should().Be(mat);
    }

    [Test]
    public void DeleteAll_DebeVaciarLaTabla() {
        // Arrange
        _repository.Create(new Vehiculo { Matricula = "V1", DniPropietario = "1", Marca="A", Modelo="A" });
        _repository.Create(new Vehiculo { Matricula = "V2", DniPropietario = "2", Marca="B", Modelo="B" });

        // Act
        _repository.DeleteAll();

        // Assert
        _repository.CountVehiculos(includeDeleted: true).Should().Be(0);
    }
    
    [Test]
    public void GettersPorDni_DebeRetornarSoloVehiculosActivos() {
        // Arrange
        var dni = "TEST-ADO-DNI";
        // Insertamos un vehículo activo
        var vActivo = _repository.Create(new Vehiculo { 
            Matricula = "ACT-111", DniPropietario = dni, Marca="A", Modelo="A", Cilindrada=100 
        }).Value;
    
        // Insertamos otro y lo borramos lógicamente
        var vBorrado = _repository.Create(new Vehiculo { 
            Matricula = "DEL-222", DniPropietario = dni, Marca="B", Modelo="B", Cilindrada=100 
        }).Value;
        _repository.Delete(vBorrado.Id, isLogical: true);

        // Act
        // 1. Probamos GetByDniPropietario (Líneas rojas en imagen)
        var encontrado = _repository.GetByDniPropietario(dni);
    
        // 2. Probamos ExistsDniPropietario (Líneas rojas en imagen)
        var existe = _repository.ExistsDniPropietario(dni);
    
        // 3. Probamos un DNI que no existe para cubrir la rama del 'null'
        var noExiste = _repository.GetByDniPropietario("DNI-FANTASMA");

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Matricula.Should().Be("ACT-111"); // Debe devolver el activo, no el borrado
        existe.Should().BeTrue();
        noExiste.Should().BeNull();
    }
}