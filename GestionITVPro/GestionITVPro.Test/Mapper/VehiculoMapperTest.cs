using FluentAssertions;
using GestionITVPro.Dto;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
using GestionITVPro.Mapper;
using GestionITVPro.Models;

namespace GestionITVPro.Test.Mapper;


/// <summary>
/// Tests para PersonaMapper.
/// Configuración de tests: Tests unitarios que no requieren conexión a base de datos.
/// Los mappers son métodos estáticos sin estado, ideales para tests puros.
/// 
/// Esta configuración garantiza:
/// 1. Tests rápidos (sin BD)
/// 2. Tests deterministas
/// 3. Cobertura de casos positivos y negativos
/// </summary>


[TestFixture]
public class VehiculoMapperTest {
    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void SetUp() {
            _vehiculo = new Vehiculo {
                Id = 1, 
                Matricula = "1234BCD", 
                Marca = "Seat Ibiza", 
                Cilindrada = 1200, 
                Motor = Motor.Gasolina, 
                DniPropietario = "01234567L", 
                IsDeleted = false, 
                CreatedAt = new DateTime(2024, 01, 17), 
                UpdatedAt = new DateTime(2024, 01, 17)
            };
            _vehiculoDto = new VehiculoDto(
                1,
                "1234BCD",
                "Seat Ibiza",
                "M-4",
                1200,
                "Gasolina",
                "01234567L",
                "2024-01-17T00:00:00",
                "2024-01-17T00:00:00",
                false,
                null
            );
            _vehiculoEntity = new VehiculoEntity {
                Id = 1,
                Matricula = "1234BCD",
                Marca = "Seat Ibiza",
                Cilindrada = 1200,
                Motor = 0,
                DniPropietario = "01234567L",
                IsDeleted = false,
                CreatedAt = new DateTime(2024, 01, 17, 0, 0, 0),
                UpdatedAt = new DateTime(2024, 01, 17, 0, 0, 0)
            };
        }

        private Vehiculo _vehiculo = null!;
        private VehiculoDto _vehiculoDto = null!;
        private VehiculoEntity _vehiculoEntity = null!;

        [Test]
        public void ToModel_VehiculoDto_Correcto() {
            var res = _vehiculoDto.ToModel();
            res.Should().NotBeNull();
            res.Id.Should().Be(1);
            res.Matricula.Should().Be("1234BCD");
            res.Marca.Should().Be("Seat Ibiza");
            res.Cilindrada.Should().Be(1200);
            res.Motor.Should().Be(Motor.Gasolina);
            res.DniPropietario.Should().Be("01234567L");
        }

        [Test]
        public void ToDto_Vehiculo_Correcto() {
            var res = _vehiculo.ToDto();
            res.Should().NotBeNull();
            res.Id.Should().Be(1);
            res.Matricula.Should().Be("1234BCD");
            res.Marca.Should().Be("Seat Ibiza");
            res.Cilindrada.Should().Be(1200);
            res.Motor.Should().Be("Gasolina");
            res.DniPropietario.Should().Be("01234567L");
        }

        [Test]
        public void ToModel_VehiculoEntity_Correcto() {
            var res = _vehiculoEntity.ToModel();

            res.Should().NotBeNull();
            res!.Id.Should().Be(1);
            res.Matricula.Should().Be("1234BCD");
            res.Marca.Should().Be("Seat Ibiza");
            res.Cilindrada.Should().Be(1200);
            res.Motor.Should().Be(Motor.Gasolina);
            res.DniPropietario.Should().Be("01234567L");
        }

        [Test]
        public void ToEntity_Vehiculo_Correcto() {
            var res = _vehiculo.ToEntity();
            
            res.Should().NotBeNull();
            res.Id.Should().Be(1);
            res.Matricula.Should().Be("1234BCD");
            res.Marca.Should().Be("Seat Ibiza");
            res.Cilindrada.Should().Be(1200);
            res.Motor.Should().Be(0);
            res.DniPropietario.Should().Be("01234567L");

        }
        [Test]
        public void ToModel_ListaEntity_DevuelveTodos() {
            var entities = new List<VehiculoEntity> { _vehiculoEntity };

            var res = entities.ToModel();

            res.Should().HaveCount(1);
        }
    }

    [TestFixture]
    public class CasosInvalidos {
        [Test]
        public void ToModel_VehiculoDto_UsaValorPorDefectoConEntradaInvalida() {
            var dto = new VehiculoDto(
                1,
                "1234BCD",
                "Seat Ibiza",
                "M-4",
                1200,
                "Diesel",
                "01234567L",
                "2024-01-17T00:00:00",
                "2024-01-17T00:00:00",
                false,
                null
                
            );

            var res = dto.ToModel();

            res.Should().NotBeNull();
            res.Motor.Should().Be(Motor.Diesel);
        }
        [Test]
        public void ToModel_VehiculoEntity_EsNullDevuelveDull() {
            VehiculoEntity? entity = null;

            var res = entity.ToModel();
        
            res.Should().BeNull();
        }
        
    }

    
}