using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Models;
using GestionITVPro.Validator;

namespace GestionITVPro.Test.Validator;

[TestFixture]
public class ValidadorCitaTest {
    
    [TestFixture]
    public class CasosPositivos {
        private ValidadorCita _validador = null!;

        [SetUp]
        public void SetUp() {
            _validador = new ValidadorCita();
        }

        [Test]
        public void Validar_VehiculoValido_DeberiaRetornarSuccess() {
            var v = new Cita {
                Id = 1,
                Matricula = "1234BBB",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678Z" // DNI Matemáticamente correcto
            };
            
            var result = _validador.Validar(v);
            result.IsSuccess.Should().BeTrue();
        }

        [Test]
        public void Validar_Matricula_Success() {
            var v = new Cita {
                Id = 1,
                Matricula = "1234BBB",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 3000,
                Motor = Motor.Diesel,
                DniPropietario = "12345678Z"
            };
            
            var result = _validador.Validar(v);
            result.IsSuccess.Should().BeTrue();
        }
        
        [Test]
        public void Validar_FechaCita_DeberiaRetornarSuccess() {
            var v = new Cita {
                Id = 1,
                Matricula = "1234BBB",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678Z",
                FechaItv = DateTime.Today.AddDays(7) // Cita para dentro de una semana
            };
    
            var result = _validador.Validar(v);
            result.IsSuccess.Should().BeTrue();
        }
    }
    
    
    [TestFixture]
    public class CasosNegativos {
        private ValidadorCita _validador = null!;

        [SetUp]
        public void SetUp() {
            _validador = new ValidadorCita();
        }

        [TestCase("")]
        [TestCase("NombreExtremadamenteLargoQueSuperaLosCincuentaCaracteresPermitidos")]
        [TestCase("S")] // Demasiado corto (menos de 2)
        public void Validar_Modelo_DeberiaRetornarFailure(string modelo) {
            var v = new Cita {
                Id = 1,
                Modelo = modelo,
                Marca = "BMW",
                Cilindrada = 3000,
                Matricula = "1234LLL",
                Motor = Motor.Gasolina,
                DniPropietario = "23232323Q"
            };
            
            var result = _validador.Validar(v);
            
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<CitaError.Validation>();
            var validationError = (CitaError.Validation)result.Error;
            validationError.Errors.Should().Contain("El modelo es obliogatorio y no puede estar vacío(2-50 car.)");
        }
        
        
        [TestCase("")]
        [TestCase(" ")] // Caso de espacios en blanco
        [TestCase("S")] // Demasiado corto
        [TestCase("NombreExtremadamenteLargoQueSuperaLosCincuentaCaracteresPermitidos")] // Demasiado largo
        public void Validar_Marca_DeberiaRetornarFailure(string marca) {
            // Arrange
            var v = new Cita {
                Id = 1,
                Modelo = "M-4",
                Marca = marca,
                Cilindrada = 3000,
                Matricula = "1234LLL",
                Motor = Motor.Gasolina,
                DniPropietario = "23232323Q"
            };
    
            // Act
            var result = _validador.Validar(v);
    
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().BeOfType<CitaError.Validation>();
    
            var validationError = result.Error as CitaError.Validation;
            validationError!.Errors.Should().Contain(e => e.Contains("marca") && e.Contains("2-50"));
        }

        [TestCase(-1)]
        [TestCase(3001)]
        public void Validar_Cilindrada_DeberiaRetornarFailure(int cilindrada) {
            var v = new Cita {
                Id = 1,
                Matricula = "1234LLL",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = cilindrada,
                Motor = Motor.Diesel,
                DniPropietario = "23232323A"
            };
            
            var result = _validador.Validar(v);

            result.IsFailure.Should().BeTrue();
            var validationError = (CitaError.Validation)result.Error;
            validationError.Errors.Should().Contain("La cilindrada debe de estar entre 0 y 3000");
        }

        [Test]
        public void Validar_MotorInvalido_DeberiaRetornarFailure() {
            var v = new Cita {
                Id = 1,
                Matricula = "1234LLL",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 1500,
                Motor = (Motor)99, // Valor que no existe en el Enum
                DniPropietario = "23232323A"
            };
            
            var result = _validador.Validar(v);

            result.IsFailure.Should().BeTrue();
            var validationError = (CitaError.Validation)result.Error;
            validationError.Errors.Should().Contain("El motor debe ser acorde a la base de datos.");
        }

        [TestCase("12345678Ñ")] // Letra inválida
        [TestCase("123")]      // Muy corto
        [TestCase("")]         // Vacío
        public void Validar_DniPropietario_DeberiaRetornarFailure(string dni) {
            var v = new Cita {
                Id = 1,
                Matricula = "1234LLL",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 1500,
                Motor = Motor.Diesel,
                DniPropietario = dni
            };
            
            var result = _validador.Validar(v);

            result.IsFailure.Should().BeTrue();
            var validationError = (CitaError.Validation)result.Error;
            validationError.Errors.Should().Contain("El DNI no es válido (8 números y letra correcta)");
        }
        
        [Test]
        public void Validar_FechaPasada_DeberiaRetornarFailure() {
            // Arrange: Una fecha de ayer
            var v = new Cita {
                Id = 1,
                Matricula = "1234BBB",
                Modelo = "M-4",
                Marca = "BMW",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678Z",
                FechaItv = DateTime.Today.AddDays(-1) 
            };
    
            // Act
            var result = _validador.Validar(v);
    
            // Assert
            result.IsFailure.Should().BeTrue();
            var validationError = (CitaError.Validation)result.Error;
            validationError.Errors.Should().Contain("La fecha de la cita no puede ser anterior al día de hoy.");
        }
    }
}