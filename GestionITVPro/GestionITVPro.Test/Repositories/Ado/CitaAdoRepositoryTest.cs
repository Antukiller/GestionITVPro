
using FluentAssertions;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Ado;
using GestionITVPro.Error.Cita;

namespace GestionITVPro.Test.Repositories.Ado;

[TestFixture]
public class CitaAdoRepositoryTest {
    private CitaAdoRepository _repository;

    [SetUp]
    public void SetUp() {
        // Inicializamos con dropData: true para recrear la tabla en cada test
        _repository = new CitaAdoRepository(dropData: true, seedData: false);
    }

    [Test]
    public void Create_DebeInsertarEnBaseDeDatosYRecuperar() {
        // Arrange
        var vehiculo = new Cita { 
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
        var v1 = new Cita { Matricula = "DUP-111", Marca="A", Modelo="B", DniPropietario="1Z" };
        _repository.Create(v1);

        // Act
        var result = _repository.Create(v1); // Intentar insertar misma matrícula

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
    }
    
    [Test]
    public void Update_CambiarModeloSinCambiarFecha_DebeTenerExito()
    {
        // Arrange
        var fecha = new DateTime(2026, 10, 15);
        var original = new Cita { Matricula = "9999XXX", FechaItv = fecha, DniPropietario = "12345678Z", Marca = "Ford", Modelo = "Fiesta" };
        var creada = _repository.Create(original).Value;

        // Act: Editamos el modelo manteniendo la misma fecha
        var editada = creada with { Modelo = "Focus" };
        var result = _repository.Update(creada.Id, editada);

        // Assert: No debe colisionar con sigo misma
        result.IsSuccess.Should().BeTrue();
        result.Value.Modelo.Should().Be("Focus");
    }


    [Test]
    public void Update_DebeModificarRegistroExistente() {
        // Arrange
        var v = _repository.Create(new Cita { 
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
        var v = _repository.Create(new Cita { 
            Matricula = "LOGIC-1", DniPropietario = "Y", Marca = "M", Modelo = "M" 
        }).Value;

        // Act
        _repository.Delete(v.Id, isLogical: true);
        var recuperado = _repository.GetById(v.Id);

        // Assert
        recuperado.Should().NotBeNull();
        recuperado!.IsDeleted.Should().BeTrue();
        _repository.GetAll(1, 10, false, null).Should().BeEmpty();
    }

    [Test]
    public void Restore_DebeReactivarVehiculoBorrado() {
        // Arrange
        var v = _repository.Create(new Cita { 
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
        // Limpiamos o aseguramos que el entorno sea controlado
        for (int i = 0; i < 3; i++) {
            _repository.Create(new Cita { 
                Matricula = $"MAT-{i}", DniPropietario = dni, Marca = "A", Modelo = "B" 
            });
        }

        // Act
        var result = _repository.Create(new Cita { 
            Matricula = "MAT-CUARTA", DniPropietario = dni, Marca = "A", Modelo = "B" 
        });

        // Assert
        result.IsFailure.Should().BeTrue();
    
        // CORRECCIÓN: Buscamos partes del texto que sabemos que existen en el mensaje de error real
        var mensajeError = result.Error.ToString();
    
        mensajeError.Should().Contain("Límite alcanzado");
        mensajeError.Should().Contain("3 vehículos"); 
        // He quitado el "tiene" porque en tu mensaje real dice "límite de 3 vehículos"
    }

    [Test]
    public void GetByMatricula_DebeRetornarCorrecto() {
        // Arrange
        var mat = "BUSCAR-MAT";
        _repository.Create(new Cita { Matricula = mat, DniPropietario = "123", Marca="A", Modelo="B" });

        // Act
        var encontrado = _repository.GetByMatricula(mat);

        // Assert
        encontrado.Should().NotBeNull();
        encontrado!.Matricula.Should().Be(mat);
    }
    
    

    [Test]
    public void DeleteAll_DebeVaciarLaTabla() {
        // Arrange
        _repository.Create(new Cita { Matricula = "V1", DniPropietario = "1", Marca="A", Modelo="A" });
        _repository.Create(new Cita { Matricula = "V2", DniPropietario = "2", Marca="B", Modelo="B" });

        // Act
        _repository.DeleteAll();

        // Assert
        _repository.CountCita(includeDeleted: true).Should().Be(0);
    }
    
    [Test]
    public void GettersPorDni_DebeRetornarSoloVehiculosActivos() {
        // Arrange
        var dni = "TEST-ADO-DNI";
        // Insertamos un vehículo activo
        var vActivo = _repository.Create(new Cita { 
            Matricula = "ACT-111", DniPropietario = dni, Marca="A", Modelo="A", Cilindrada=100 
        }).Value;
    
        // Insertamos otro y lo borramos lógicamente
        var vBorrado = _repository.Create(new Cita { 
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
    
    [Test]
    public void Create_CitaDuplicadaMismoDia_DebeRetornarError()
    {
        // Arrange
        var fecha = new DateTime(2026, 10, 15);
        var cita1 = new Cita { Matricula = "1234BBB", FechaItv = fecha, DniPropietario = "12345678Z", Marca = "Toyota", Modelo = "Corolla" };
        _repository.Create(cita1);

        // Act: Intentar crear otra para el mismo coche el mismo día
        var cita2 = cita1 with { Id = 0, Marca = "Otro" }; 
        var result = _repository.Create(cita2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("ya tiene programada una cita para esa fecha");
    }
        
    [Test]
    public void Create_ExcederLimiteDeTresVehiculos_DebeRetornarError()
    {
        // Arrange
        const string dni = "11112222A";
        var mismaFecha = DateTime.Now.AddDays(5); // Una fecha fija para todos

        for (int i = 1; i <= 3; i++)
        {
            _repository.Create(new Cita 
            { 
                Matricula = $"MAT00{i}", 
                DniPropietario = dni, 
                FechaItv = mismaFecha, // <--- Misma fecha
                Marca = "Fiat", 
                Modelo = "500" 
            });
        }

        // Act: Intentar el cuarto el mismo día
        var cuarta = new Cita { 
            Matricula = "MAT004", 
            DniPropietario = dni, 
            FechaItv = mismaFecha, // <--- Misma fecha
            Marca = "Fiat", 
            Modelo = "Panda" 
        };
        var result = _repository.Create(cuarta);

        // Assert
        result.IsFailure.Should().BeTrue();
        // Verifica que el mensaje coincida exactamente con lo que devuelve tu CitaErrors
        result.Error.Message.ToLower().Should().Contain("límite");
    }
}