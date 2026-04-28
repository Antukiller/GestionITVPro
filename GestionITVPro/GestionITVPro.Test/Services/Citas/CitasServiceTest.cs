using System.Diagnostics.Contracts;
using CSharpFunctionalExtensions;
using FluentAssertions;
using GestionITVPro.Cache;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using GestionITVPro.Service.Citas;
using GestionITVPro.Validator.Common;
using Moq;

namespace GestionITVPro.Test.Services.Citas;

[TestFixture]
public class CitasServiceTest {
    [SetUp]
    public void SetUp() {
        _repositoryMock = new Mock<ICitaRepository>();
        _valCitaMock = new Mock<IValidador<Cita>>();
        _cacheMock = new Mock<ICache<int, Cita>>();
        
        // Configurar validador para que devuelvan Success por defecto
        _valCitaMock.Setup(v => v.Validar(It.IsAny<Cita>()))
            .Returns((Cita c) => Result.Success<Cita, DomainError>(c));

        _service = new CitasService(
            _repositoryMock.Object,
            _valCitaMock.Object,
            _cacheMock.Object
        );
    }

    private CitasService _service = null!;
    private Mock<ICitaRepository> _repositoryMock = null!;
    private Mock<IValidador<Cita>> _valCitaMock = null!;
    private Mock<ICache<int, Cita>> _cacheMock = null!;
    
    
    [TestFixture]
    public class CasosPositivos : CitasServiceTest{
        [Test]
        public void GetAll_SinParametros_RetornarTodaLasCitas() {
            // Arrange
            var c = new List<Cita> {
                new Cita { Id = 1, Matricula = "1234-BBB" }
            };
            _repositoryMock.Setup(r => r.GetAll(1, 10, true)).Returns(c);
            
            // Act
            var r = _service.GetAll().ToList();
            
            // Assert
            r.Should().HaveCount(1);
            _repositoryMock.Verify(c => c.GetAll(1, 10, true), Times.Once);
        }

        [Test]
        public void GetAll_ConPaginacion_RetornarCitasPaginadas() {
            // Arrange
            var c = new List<Cita> {
                new Cita { Id = 1, Matricula = "1234-BBB" }
            };
            _repositoryMock.Setup(c => c.GetAll(2, 5, false)).Returns(c);
            
            // Act
            var r = _service.GetAll(2, 5, false).ToList();
            
            // Assert
            r.Should().HaveCount(1);
            _repositoryMock.Verify(c => c.GetAll(2, 5, false), Times.Once);
        }
        
        [Test]
        public void TotalCitas_DeberiaRetornarElConteoCorrectoDesdeElRepositorio()
        {
            // Arrange
            var citas = new List<Cita>
            {
                new() { Id = 1 },
                new() { Id = 2 },
                new() { Id = 3 }
            };

            // Configuramos el mock para que cuando GetAll pida "todo" (1 a int.MaxValue), devuelva la lista
            _repositoryMock.Setup(r => r.GetAll(1, int.MaxValue, It.IsAny<bool>()))
                .Returns(citas);

            // Act
            // Accedemos a la propiedad que está en rojo en tu captura
            var total = _service.TotalCitas;

            // Assert
            total.Should().Be(3);
            _repositoryMock.Verify(r => r.GetAll(1, int.MaxValue, It.IsAny<bool>()), Times.Once);
        }

        [Test]
        public void GetById_ConCache_RetornarDeCache() {
            // Arrange
            var cita = new Cita { Id = 1, Matricula = "1234-BBB" };
            _cacheMock.Setup(c => c.Get(1)).Returns(cita);
            
            // Act 
            var r = _service.GetById(1);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Matricula.Should().Be("1234-BBB");
            _cacheMock.Verify(c => c.Get(1), Times.Once);
            _repositoryMock.Verify(c => c.GetById(It.IsAny<int>()), Times.Never);
        }
        
        [Test]
        public void GetByDniPropietario_CuandoExiste_RetornarCita() {
            // Arrange
            var dni = "12345678Z";
            var cita = new Cita { Id = 1, DniPropietario = dni, Matricula = "1234BBB" };
            _repositoryMock.Setup(r => r.GetByDniPropietario(dni)).Returns(cita);

            // Act
            var resultado = _service.GetByDniPropietario(dni);

            // Assert
            resultado.IsSuccess.Should().BeTrue();
            resultado.Value.DniPropietario.Should().Be(dni);
        }

        [Test]
        public void GetById_SinCache_BuscarEnRepositorioYAgregarACache() {
            // Arrange
            var cita = new Cita { Id = 1, Matricula = "1234-BBB" };
            if (_cacheMock.Setup(c => c.Get(1)) == null) {
                
            }
            _repositoryMock.Setup(c => c.GetById(1)).Returns(cita);
            
            // Act
            var r = _service.GetById(1);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Matricula.Should().Be("1234-BBB");
            _cacheMock.Verify(c => c.Get(1), Times.Once);
            _cacheMock.Verify(c => c.Add(1, cita), Times.Once);
            _repositoryMock.Verify(c => c.GetById(1), Times.Once);
        }

        [Test]
        public void GetByMatricula_ConCitaExistente_RetornarCita() {
            // Arrange
            var c = new Cita { Id = 1, Matricula = "1234-BBB" };
            _repositoryMock.Setup(c => c.GetByMatricula("1234-BBB")).Returns(c);
            
            // Act
            var r = _service.GetByMatricula("1234-BBB");
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Matricula.Should().Be("1234-BBB");
            _repositoryMock.Verify(c => c.GetByMatricula("1234-BBB"), Times.Once);
        }

        [Test]
        public void Save_ConCitaValida_GuardarCorrectamente() {
            var c = new Cita { Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4" };

            _valCitaMock.Setup(c => c.Validar(It.IsAny<Cita>()))
                .Returns((Cita c) => Result.Success<Cita, DomainError>(c));
            _repositoryMock.Setup(c => c.ExistsMatricula(It.IsAny<string>())).Returns(false);
            _repositoryMock.Setup(c => c.ExistsDniPropietario(It.IsAny<string>())).Returns(false);
            _repositoryMock.Setup(c => c.Create(It.IsAny<Cita>()))
                .Returns((Cita c) => Result.Success<Cita, DomainError>(c));
            
            // Act
            var r = _service.Save(c);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            _repositoryMock.Verify(c => c.Create(It.IsAny<Cita>()), Times.Once);
            _repositoryMock.Verify(c => c.ExistsMatricula(It.IsAny<string>()), Times.Once);
            _repositoryMock.Verify(c => c.ExistsDniPropietario(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void Update_ConCitaExistente_ActualizarYLimpiarCache() {
            // Arrange
            var e = new Cita { Id = 1, Matricula = "1234-BBB", Marca = "BMW" };
            var a = new Cita { Id = 1, Matricula = "1234-BBB", Marca = "Toyota" };

            _repositoryMock.Setup(c => c.GetById(1)).Returns(e);
            _repositoryMock.Setup(c => c.GetByMatricula(It.IsAny<string>())).Returns((Cita?)null);
            _repositoryMock.Setup(c => c.GetByDniPropietario(It.IsAny<string>())).Returns((Cita?)null);
            _valCitaMock.Setup(c => c.Validar(It.IsAny<Cita>()));
            _repositoryMock.Setup(c => c.Update(1, It.IsAny<Cita>()))
                .Returns((int id, Cita c) => Result.Success<Cita, DomainError>(c));
            
            // Act
            var r = _service.Update(1, a);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            _cacheMock.Verify(c => c.Remove(1), Times.Once);
            _repositoryMock.Verify(c => c.Update(1, It.IsAny<Cita>()), Times.Once);

        }

        [Test]
        public void Delete_ConCitaExistente_EliminarYLimpiarCache() {
            var c = new Cita { Id = 1, Matricula = "1234-BBB" };
            _repositoryMock.Setup(c => c.GetById(1)).Returns(c);
            _repositoryMock.Setup(c => c.Delete(1, true)).Returns(c);
            
            // Act
            var r = _service.Delete(1);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            _cacheMock.Verify(c => c.Remove(1), Times.Once);
            _repositoryMock.Verify(c => c.Delete(1, true), Times.Once);
            _repositoryMock.Verify(c => c.GetById(1), Times.Once);
        }

        [Test]
        public void DeleteAll_LlamarRepository() {
            // Arrange
            _repositoryMock.Setup(c => c.DeleteAll()).Returns(true);
            
            // Act 
            var r = _service.DeleteAll();
            
            // Assert
            r.Should().BeTrue();
            _repositoryMock.Verify(c => c.DeleteAll(), Times.Once);
        }

        [Test]
        public void GetCitasOrderBy_RetornarCitasOrdenadas()
        {
            // Arrange
            var citas = new List<Cita>
            {
                new() { Id = 1, Matricula = "ZZZ", FechaItv = DateTime.Now.AddDays(10) },
                new() { Id = 2, Matricula = "AAA", FechaItv = DateTime.Now.AddDays(1) }
            };

            // Configuramos el Mock para que acepte CUALQUIER valor en los parámetros
            // o los valores específicos que usa tu servicio (1, int.MaxValue, true)
            _repositoryMock.Setup(r => r.GetAll(
                    It.IsAny<int>(), 
                    It.IsAny<int>(), 
                    It.IsAny<bool>()))
                .Returns(citas);

            // Act
            // Usamos el Enum TipoOrdenamiento que tienes en tu código
            var resultado = _service.GetCitasOrderBy(TipoOrdenamiento.Matricula, 1, 10, true).ToList();

            // Assert
            resultado.Should().HaveCount(2);
            // Como ordenamos por Matrícula, "AAA" debe ir primero
            resultado.First().Matricula.Should().Be("AAA");
            resultado.Last().Matricula.Should().Be("ZZZ");
        }
        
        [Test]
        public void GetCitasOrderBy_DeberiaCubrirTodosLosCasosDelSwitch() {
            // Arrange
            var citas = new List<Cita> {
                new() { Id = 10, Matricula = "B", DniPropietario = "2", Marca = "Z", Modelo = "M2", Cilindrada = 2000 },
                new() { Id = 1, Matricula = "A", DniPropietario = "1", Marca = "A", Modelo = "M1", Cilindrada = 1000 }
            };
    
            _repositoryMock.Setup(r => r.GetAll(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(citas);

            // Act & Assert - Probamos cada rama para forzar la cobertura
    
            // Probar DniPropietario
            _service.GetCitasOrderBy(TipoOrdenamiento.DniPropietario).First().Id.Should().Be(1);
    
            // Probar Marca
            _service.GetCitasOrderBy(TipoOrdenamiento.Marca).First().Marca.Should().Be("A");
    
            // Probar Modelo
            _service.GetCitasOrderBy(TipoOrdenamiento.Modelo).First().Modelo.Should().Be("M1");
    
            // Probar Cilindrada
            _service.GetCitasOrderBy(TipoOrdenamiento.Cilindrada).First().Cilindrada.Should().Be(1000);
    
            // Probar Caso por defecto (ID)
            _service.GetCitasOrderBy((TipoOrdenamiento)999).First().Id.Should().Be(1);
        }
        
        [Test]
        public void GetCitasOrderBy_FechaItv_DeberiaRetornarCitaMasCercanaPrimero()
        {
            // Arrange
            var hoy = DateTime.Today;
            var citas = new List<Cita>
            {
                new() { Id = 1, FechaItv = hoy.AddDays(5) },
                new() { Id = 2, FechaItv = hoy.AddDays(1) }
            };

            _repositoryMock.Setup(r => r.GetAll(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(citas);

            // Act
            var resultado = _service.GetCitasOrderBy(TipoOrdenamiento.FechaItv).ToList();

            // Assert
            resultado.First().Id.Should().Be(2); // La del día 1 es antes que la del día 5
        }
    }
    
    [TestFixture]
    public class CasosNegativos : CitasServiceTest {
        [Test]
        public void GetById_ConCitaNoExistente_RetonarErrorNotFound() {
            // Arrange
            _cacheMock.Setup(c => c.Get(1)).Returns((Cita?)null);
            _repositoryMock.Setup(c => c.GetById(1)).Returns((Cita?)null);
            
            // Act
            var r = _service.GetById(1);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();
            r.Error.Message.Should().Contain("1");
            _cacheMock.Verify(c => c.Get(1), Times.Once);
            _repositoryMock.Verify(c => c.GetById(1), Times.Once);

        }
        
        [Test]
        public void GetByDniPropietario_CuandoNoExiste_RetornarNotFound() {
            // Arrange
            var dni = "99999999X";
            _repositoryMock.Setup(r => r.GetByDniPropietario(dni)).Returns((Cita)null!);

            // Act
            var resultado = _service.GetByDniPropietario(dni);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<CitaError.NotFound>();
        }

        [Test]
        public void GetByMatricula_ConCitaNoExistente_RetornarErrotNotFound() {
            // Arrange
            _repositoryMock.Setup(c => c.GetByMatricula("1234-BBB")).Returns((Cita?)null);
            
            // Act
            var r = _service.GetByMatricula("1234-BBB");
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();
            r.Error.Message.Should().Contain("1234-BBB");
            _repositoryMock.Verify(c => c.GetByMatricula("1234-BBB"), Times.Once);
        }

        [Test]
        public void Save_MatriculaDuplicada_RetornarErrorMatriculaAlreadyExists() {
            var c = new Cita { Matricula = "1234-BBB", Marca = "BMW"};

            _valCitaMock.Setup(c => c.Validar(It.IsAny<Cita>()))
                .Returns((Cita c) => Result.Success<Cita, DomainError>(c));
            _repositoryMock.Setup(c => c.ExistsMatricula("1234-BBB")).Returns(true);
            
            // Act
            var r = _service.Save(c);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
            r.Error.Message.Should().Contain("1234-BBB");
            _repositoryMock.Verify(c => c.ExistsMatricula("1234-BBB"), Times.Once);
            _repositoryMock.Verify(c => c.Create(It.IsAny<Cita>()), Times.Never);
        }
        
        [Test]
        public void Save_DniPropietarioDuplicado_RetornarErrorDniPropietarioAlreadyExists() {
            var c = new Cita { Matricula = "1234-BBB", Marca = "BMW", DniPropietario = "12345678A"};

            _valCitaMock.Setup(c => c.Validar(It.IsAny<Cita>()))
                .Returns((Cita c) => Result.Success<Cita, DomainError>(c));
            _repositoryMock.Setup(c => c.ExistsDniPropietario("12345678A")).Returns(true);
            
            // Act
            var r = _service.Save(c);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.DniPropiestarioAlreadyExists>();
            r.Error.Message.Should().Contain("12345678A");
            _repositoryMock.Verify(c => c.ExistsDniPropietario("12345678A"), Times.Once);
            _repositoryMock.Verify(c => c.Create(It.IsAny<Cita>()), Times.Never);
        }

        [Test]
        public void Save_ConValidacionFallida_RetornarErrorValidation() {
            var c = new Cita { Matricula = "1234-BBB", Marca = "BMW" };
            var error = new CitaError.Validation(new[] { "La marca no puede estar vacia" });

            _valCitaMock.Setup(v => v.Validar(It.IsAny<Cita>()))
                .Returns(Result.Failure<Cita, DomainError>(error));
            
            // Act
            var r = _service.Save(c);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.Validation>();
            r.Error.Message.Should().Contain("La marca no puede estar vacia");
            _valCitaMock.Verify(v => v.Validar(c), Times.Once);
            _repositoryMock.Verify(r => r.Create(It.IsAny<Cita>()), Times.Never);
        }

        [Test]
        public void Update_ConCitaNoExistente_RetornarErrorNotFound() {
            // Arrange
            _repositoryMock.Setup(c => c.GetById(999)).Returns((Cita?)null);
            
            // Act
            var r = _service.Update(999, new Cita { Matricula = "123-ddf" });
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();
            r.Error.Message.Should().Contain("999");
            _repositoryMock.Verify(c => c.GetById(999), Times.Once);
            _repositoryMock.Verify(c => c.Update(It.IsAny<int>(), It.IsAny<Cita>()), Times.Never);
        }
        
        [Test]
        public void Delete_ConPersonaNoExistente_DeberiaRetornarErrorNotFound() {
            // Arrange
            _repositoryMock.Setup(r => r.GetById(999)).Returns((Cita?)null);

            // Act
            var resultado = _service.Delete(999);

            // Assert
            resultado.IsFailure.Should().BeTrue();
            resultado.Error.Should().BeOfType<CitaError.NotFound>();
            resultado.Error.Message.Should().Contain("999");
            _repositoryMock.Verify(r => r.GetById(999), Times.Once);
            _repositoryMock.Verify(r => r.Delete(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void Update_ConMatriculaEnOtraCita_RetornarError() {
            var e = new Cita { Id = 1, Matricula = "1234-BBB", Marca = "BMW" };
            var a = new Cita { Id = 1, Matricula = "1234-BBC" };
            var otraCita = new Cita { Id = 2, Matricula = "1234-BBC" };

            _repositoryMock.Setup(v => v.GetById(1)).Returns(e);
            _valCitaMock.Setup(v => v.Validar(It.IsAny<Cita>()))
                .Returns(Result.Success<Cita, DomainError>(a));
            _repositoryMock.Setup(v => v.GetByMatricula("1234-BBC")).Returns(otraCita);
            
            // Act
            var r = _service.Update(1, a);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.MatriculaAlreadyExists>();
            r.Error.Message.Should().Contain("1234-BBC");

        }
        
        
        [Test]
        public void Update_ConDniPropietarioEnOtraCita_RetornarError() {
            var e = new Cita { Id = 1, Matricula = "1234-BBB", Marca = "BMW", DniPropietario = "12345678B"};
            var a = new Cita { Id = 1, Matricula = "1234-BBB", DniPropietario = "12345678A"};
            var otraCita = new Cita { Id = 2, DniPropietario = "12345678A"};

            _repositoryMock.Setup(v => v.GetById(1)).Returns(e);
            _valCitaMock.Setup(v => v.Validar(It.IsAny<Cita>()))
                .Returns(Result.Success<Cita, DomainError>(a));
            _repositoryMock.Setup(v => v.GetByMatricula(It.IsAny<string>())).Returns((Cita?)null);
            _repositoryMock.Setup(v => v.GetByDniPropietario("12345678A")).Returns(otraCita);
            
            // Act
            var r = _service.Update(1, a);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.DniPropiestarioAlreadyExists>();
            r.Error.Message.Should().Contain("12345678A");

        }

        [Test]
        public void Restore_ConCitaExistenteEliminada_Restaurar() {
            // Arrange
            var c = new Cita { Id = 1, Matricula = "1234-BBB", IsDeleted = true};
            _repositoryMock.Setup(v => v.Restore(1))
                .Returns(Result.Success<Cita, DomainError>(new Cita
                    { Id = 1, Matricula = "1234-BBB", IsDeleted = false }));
            
            // Act
            var r = _service.Restore(1);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            _repositoryMock.Verify(v=> v.Restore(1), Times.Once);
        }

        [Test]
        public void Restore_ConCitaNoExistente_RetornarError() {
            // Arrange
            _repositoryMock.Setup(v => v.Restore(999))
                .Returns(Result.Failure<Cita, DomainError>(CitaErrors.NotFound("999")));

            // Act
            var r = _service.Restore(999);

            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<CitaError.NotFound>();

        }

        [Test]
        public void CountCita_RetornarCorrecto() {
            // Arrange
            _repositoryMock.Setup(v => v.CountCita(false)).Returns(5);
            
            // Act
            var r = _service.CountCitas();
            
            // Assert
            r.Should().Be(5);
            _repositoryMock.Verify(v => v.CountCita(false), Times.Once);
        }
    }
    
}