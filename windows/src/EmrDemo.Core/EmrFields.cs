using System.Text.Json;

namespace EmrDemo.Core;

public static class EmrFields
{
    public const string ChartText = "chartText";
    public const string MedicalMemo = "medicalMemo";
    public const string PatientMemo = "patientMemo";

    public static string Vital(int row, string property) => $"vitals[{row}].{Camel(property)}";

    public static string Order(int row, string property) => $"orders[{row}].{Camel(property)}";

    static string Camel(string property) => JsonNamingPolicy.CamelCase.ConvertName(property);
}
