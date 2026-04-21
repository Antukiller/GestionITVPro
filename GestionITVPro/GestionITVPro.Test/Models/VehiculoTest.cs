namespace GestionITVPro.Test.Models;

using FluentAssertions;
using GestionITVPro.Enums;
using GestionITVPro.Models;
 

[TestFixture]
public class VehiculoTest {
    [TestFixture]
    public class CasosPositivos {
        [Test]
        public void ToString_DeberiaRetonarFormatoCorrecto() {
            // Arrange
            var v = new Vehiculo {
                Id = 1,
                Matricula = "1234-CDF",
                Marca = "BMW",
                Modelo = "M-4"
            };

            // Act
            var resultado = v.ToString();

            // Assert
            // Es mejor buscar partes clave que sabemos que siempre estarán
            resultado.Should().Contain("Vehiculo"); // Sin tilde y sin corchetes
            resultado.Should().Contain("1234-CDF");
            resultado.Should().Contain("BMW");
            resultado.Should().Contain("M-4");
        }

        [Test]
        public void Descripcion_DeberiaConcatenarMatriculaMarcaModeloYMotor() {
            // Arrange
            var vehiculo = new Vehiculo { Matricula = "1234-CDF", Marca = "BMW",  Modelo = "M-4", Motor =  Motor.Gasolina};
            
            // Act 
            var resultado = vehiculo.Descripcion;
            
            resultado.Should().Be("1234-CDF, BMW, M-4, Gasolina");
            
        }
        
        [Test]
        public void Equals_MismaMatricula_DeberiaSerIgual() {
            var vehiculo1 = new Vehiculo { Matricula = "1234-CDF", Marca = "BMW",};
            var vehiculo2 = new Vehiculo  { Matricula = "1234-CDF", Marca = "BMW",};
            
            // Act 
            var resultado = vehiculo1.Equals(vehiculo2);
            
            // Assert
            resultado.Should().BeTrue();
        }

        [Test]
        public void Equals_DistintaMatricula_DeberiaNoSerIgual() {
            var vehiculo = new Vehiculo { Matricula = "1234-CDF", Marca = "BMW",};
            var vehiculo2 = new Vehiculo  { Matricula = "1234-CDN", Marca = "BMW",};
            
            // Act
            var resultado = vehiculo.Equals(vehiculo2);
            
            // Assert
            resultado.Should().BeFalse();
            
        }

        [Test]
        public void GetHashCode_MismaMatricula_MismoHashCode() {
            // Arrange
            var vehiculo =  new Vehiculo { Matricula = "1234-CDF" };
            var vehiculo2 = new Vehiculo { Matricula = "1234-CDF" };
            
            // Act
            var hash1 = vehiculo.GetHashCode();
            var hash2 = vehiculo2.GetHashCode();
            
            // Assert
            hash1.Should().Be(hash2);
        }
    }
    
    
    [TestFixture]
    public class CasosNegativos {
        [Test]
        public void Equals_Nulo_DeberiaRetonarFalse() {
            // Arrange
            var vehiculo = new Vehiculo { Matricula = "1234-CDF" };
            
            // Act
            var resultado = vehiculo.Equals(null);
            
            // Assert
            resultado.Should().BeFalse();
        }
    }
}