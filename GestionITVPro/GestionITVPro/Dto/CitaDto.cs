namespace GestionITVPro.Dto;

public record CitaDto(
    int Id,
    string Matricula,
    string Marca,
    string Modelo,
    int Cilindrada,
    string Motor,
    string DniPropietario,
    string FechaItv,
    string FechaInspeccion,
    string CreatedAt,
    string UpdatedAt,
    bool IsDeleted,
    string DeletedAt
) {
    public CitaDto() : this(0, "", "", "", 0, "", "","","", "", "", false, "") { }
}