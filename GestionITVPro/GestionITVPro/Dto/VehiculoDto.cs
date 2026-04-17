namespace GestionITVPro.Dto;

public record VehiculoDto(
    int Id,
    string Matricula,
    string Marca,
    string Modelo,
    int Cilindrada,
    string Motor,
    string DniPropietario,
    string CreateAt,
    string UpdateAt,
    bool IsDeleted,
    string DeletedAt
) {
    public VehiculoDto() : this(0, "", "", "", 0, "", "", "", "", false, "") { }
}