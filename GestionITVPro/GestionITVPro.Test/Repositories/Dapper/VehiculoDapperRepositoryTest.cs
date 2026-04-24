using System.Data;
using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Models;
using GestionITVPro.Storage.Dapper;
using Microsoft.Data.Sqlite;

namespace GestionITVPro.Test.Repositories.Dapper;

/// <summary>
/// Tests para PersonasDapperRepository.
/// Configuración de tests: Se utiliza una conexión SQLite en memoria (:memory:) para cada clase de test.
/// El repositorio Dapper es ideal para tests porque:
/// - Usa SQL directo (similar a la aplicación real)
/// - Es rápido (no usa ORM completo)
/// - La BD se crea y destruye con cada test
/// 
/// Esta configuración garantiza:
/// 1. Conexión independiente por clase de test
/// 2. Estado limpio en cada test (no hay datos residuales)
/// 3. Los IDs siempre empiezan desde 1
/// 4. Tests rápidos y aislados
/// </summary>

[TestFixture]
public class VehiculoDapperRepositoryTest {
    [TestFixture]
    public class CasosPositivos {
        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _repository = new VehiculoDapperRepository(_connection);
        }

        [TearDown]
        public void TearDown() {
            _connection.Close();
            _connection.Dispose();
        }

        private IDbConnection _connection = null!;
        private VehiculoDapperRepository _repository = null!;


        [Test]
        public void Create_VehiculoValido_CrearCorrectamente() {
            // Arrange 
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            
            // Act 
            var r = _repository.Create(v);

            r.IsSuccess.Should().BeTrue();
            r.Value.Id.Should().Be(1);
        }

        [Test]
        public void GetById_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetById(1);
            
            // Assert
            r.Should().NotBeNull();
            r!.Id.Should().Be(1);
        }

        [Test]
        public void GetByMatricula_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetByMatricula("1234-BBB");
            
            // Assert
            r.Should().NotBeNull();
            r!.Matricula.Should().Be("1234-BBB");
        }
        
        [Test]
        public void GetByDniPropietario_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.GetByDniPropietario("12345678A");
            
            // Assert
            r.Should().NotBeNull();
            r!.DniPropietario.Should().Be("12345678A");
        }

        [Test]
        public void ExistsDniPropietario_CuandoExiste_RetornarTrue() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.ExistsDniPropietario("12345678A");
            
            // Assert
            r.Should().BeTrue();
        }
        
        [Test]
        public void ExistsMatricula_CuandoExiste_RetornarTrue() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB",
                Marca = "M-4",
                Modelo = "M-4",
                Cilindrada = 3000,
                Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            _repository.Create(v);
            
            // Act
            var r = _repository.ExistsMatricula("1234-BBB");
            
            // Assert
            r.Should().BeTrue();
        }

        [Test]
        public void GetAll_SinParametros_RetonarTodos() {
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act 
            var r = _repository.GetAll();
            
            // Assert
            r.Should().HaveCount(2);
        }

        [Test]
        // Arrange 
        public void GetAll_ConPaginacion_RetornarPagina() {
            // Arrange 
            for (var i = 1; i <= 5; i++)
                _repository.Create(new Vehiculo {
                    Matricula = $"{i:D4}-BBB", Marca = $"Marca{i}", Modelo = "Modelo", Cilindrada = 3000,
                    Motor = Motor.Diesel, DniPropietario = $"{i:D8}A"
                });
            
            // Act
            var r = _repository.GetAll(1, 3);
            
            // Assert
            r.Should().HaveCount(3);
        }

        [Test]
        public void GetAll_SinIncluirBorrados_RetornarSoloActivos() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            var v2 = _repository.Create(new Vehiculo {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            }).Value;
            _repository.Delete(v2.Id);
            
            // Act
            var r = _repository.GetAll(includeDeleted: false);
            
            // Assert
            r.Should().HaveCount(1);
            r.First().Matricula.Should().Be("1234-BBB");
        }

        [Test]
        public void Update_ConDatosValidos_RetornarActualizacion() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);

            var a = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-467", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            
            // Act
            var r = _repository.Update(1, a);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.Modelo.Should().Be("M-467");
        }

        [Test]
        public void Delete_Logico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);
            
            // Act
            var r = _repository.Delete(1);
            
            // Assert
            r.Should().NotBeNull();
            r!.IsDeleted.Should().BeTrue();
        }

        [Test]
        public void Delete_Fisico_CuandoExiste_RetornarVehiculo() {
            // Arrange
            var v = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            _repository.Create(v);
            
            // Act
            var r = _repository.Delete(1, false);
            
            // Assert
            r.Should().NotBeNull();
            _repository.GetById(1).Should().BeNull();
        }

        [Test]
        public void DeleteAll_CuandoHayDatos_EliminarTodos() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act
            var r = _repository.DeleteAll();
            
            // Assert
            r.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
    }

    [TestFixture]
    public class CasosNegativos {
        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _repository = new VehiculoDapperRepository(_connection);
        }

        [TearDown]
        public void TearDown() {
            _connection.Close();
            _connection.Dispose();
        }

        private IDbConnection _connection = null!;
        private VehiculoDapperRepository _repository = null!;

        [Test]
        public void Create_ConMatriculaExistente_RetornarFailure() {
            var v1 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            
            // Act 
            var r = _repository.Create(v2);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
            (r.Error as VehiculoError.MatriculaAlreadyExists)?.Matricula.Should().Be("1234-BBB");
            r.Error.Message.Should().Contain("1234-BBB");
        }

        [Test]
        public void GetById_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.GetById(213456);
            
            // Assert
            r.Should().BeNull();
            
        }
        
        [Test]
        public void GetByDniPropietario_CuandoNoExiste_RetonarNull() {
            // Arrange & Act
            var r = _repository.GetByDniPropietario("12345678asd");
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void GetByMatricula_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.GetByMatricula("1234456-BVCV");
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void ExistsDniPropietario_CuandoNoExiste_RetornarFalse() {
            // Arrange & Act
            var r = _repository.ExistsDniPropietario("1234567890DC");
            
            // Assert
            r.Should().BeFalse();
        }

        [Test]
        public void ExistsMatricula_CuandoNoExiste_RetornarFailure() {
            // Arrange & Act
            var r = _repository.ExistsMatricula("12345-VBNM");
            
            // Assert
            r.Should().BeFalse();
        }

        [Test]
        public void Update_CuandoNoExiste_RetornarFailure() {
            // Arrange
            var v1 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            // Act
            var r = _repository.Update(9999, v1);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<VehiculoError.NotFound>();
            (r.Error as VehiculoError.NotFound)?.Id.Should().Be("9999");
            r.Error.Message.Should().Contain("9999");

        }
        
        [Test]
        public void Update_ConMatriculaExistente_RetornarFailure() { 
            // Arrange
            var v1 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Vehiculo {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            _repository.Create(v2);
            
            // Act
            var r = _repository.Update(2, v1);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<VehiculoError.MatriculaAlreadyExists>();
            (r.Error as VehiculoError.MatriculaAlreadyExists)?.Matricula.Should().Be("1234-BBB");
        }
        
        [Test]
        public void Update_ConDniPropietarioExisteEnOtro_RetornarFaulure() {
            // Arrange
            var v1 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };

            var v2 = new Vehiculo {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678B"
            };
            _repository.Create(v1);
            _repository.Create(v2);
            
            var a = new Vehiculo {
                Matricula = "1234-BBC", Marca = "BMW", Modelo = "M-6", Cilindrada = 3000, Motor = Motor.Hibrido,
                DniPropietario = "12345678A"
            };
            
            // Act 
            var r = _repository.Update(2, a);

            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<VehiculoError.DniPropiestarioAlreadyExists>();
            (r.Error as VehiculoError.DniPropiestarioAlreadyExists)?.DniPropietario.Should().Be("12345678A");
        }

        [Test]
        public void Delete_CuandoNoExiste_RetornarNull() {
            // Arrange & Act
            var r = _repository.Delete(999);
            
            // Assert
            r.Should().BeNull();
        }

        [Test]
        public void Restore_CuandoNoExiste_RetornarFailure() {
            // Arrange & Act
            var r = _repository.Restore(999);
            
            // Assert
            r.IsFailure.Should().BeTrue();
            r.Error.Should().BeOfType<VehiculoError.NotFound>();
        }
    }

    [TestFixture]
    public class CasosMixtos {
        [SetUp]
        public void SetUp() {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _repository = new VehiculoDapperRepository(_connection);
        }

        [TearDown]
        public void TearDown() {
            _connection.Close();
            _connection.Dispose();
        }

        private IDbConnection _connection = null!;
        private VehiculoDapperRepository _repository = null!;
        
        [Test]
        public void Restore_CuandoEliminasLogicamente_Restaurar() {
            // Arrange
            var v1 = new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            };
            var creada = _repository.Create(v1).Value;
            _repository.Delete(creada.Id);
            
            // Act
            var r = _repository.Restore(creada.Id);
            
            // Assert
            r.IsSuccess.Should().BeTrue();
            r.Value.IsDeleted.Should().BeFalse();
            r.Value.DeletedAt.Should().BeNull();
        }

        [Test]
        public void CountVehiculos_SinEliminados_RetornarSoloActivos() {
            // Arrange (3 vehículos creados)
            _repository.Create(new Vehiculo { Matricula = "V1", DniPropietario = "A", Marca="A", Modelo="A" });
            var v2 = _repository.Create(new Vehiculo { Matricula = "V2", DniPropietario = "B", Marca="A", Modelo="A" }).Value;
            _repository.Create(new Vehiculo { Matricula = "V3", DniPropietario = "C", Marca="A", Modelo="A" });
    
            // Borramos 1
            _repository.Delete(v2.Id);

            // Act
            var r = _repository.CountVehiculos();
    
            // Assert: Quedan 2 activos
            r.Should().Be(2); 
        }

        [Test]
        public void CountVehiculos_IncluyendoEliminados_ContarTodos() {
            _repository.Create(new Vehiculo { Matricula = "V1", DniPropietario = "A", Marca = "A", Modelo = "A" });
            var v2 = _repository.Create(new Vehiculo
                { Matricula = "V2", DniPropietario = "B", Marca = "A", Modelo = "A" }).Value;
            _repository.Delete(v2.Id);
            
            // Act
            var r = _repository.CountVehiculos(true);
            
            // Assert
            r.Should().Be(2);
        }

        public void DeleteAll_DeberiaVaciarRepositorio() {
            // Arrange
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBB", Marca = "BMW", Modelo = "M-4", Cilindrada = 3000, Motor = Motor.Gasolina,
                DniPropietario = "12345678A"
            });
            
            _repository.Create(new Vehiculo {
                Matricula = "1234-BBC", Marca = "Toyota", Modelo = "Sandero", Cilindrada = 1500, Motor = Motor.Diesel,
                DniPropietario = "12345678B"
            });
            
            // Act
            var r = _repository.DeleteAll();
            
            // Assert
            r.Should().BeTrue();
            _repository.GetAll().Should().BeEmpty();
        }
    }
    
    
}