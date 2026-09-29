namespace EmrDemo.Core;

public sealed class Patient
{
    public const int VitalRowCount = 5;
    public const int OrderRowCount = 8;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Department { get; set; } = "";
    public string VisitDate { get; set; } = "";
    public string ChartText { get; set; } = "";
    public string MedicalMemo { get; set; } = "";
    public string PatientMemo { get; set; } = "";
    public string? FormId { get; set; }
    public Dictionary<string, string> FormValues { get; set; } = [];
    public List<VitalSign> Vitals { get; set; } = [];
    public List<Diagnosis> Diagnoses { get; set; } = [];
    public List<Order> Orders { get; set; } = [];

    internal void Normalize()
    {
        Vitals = (Vitals ?? []).Select(v => v ?? new VitalSign()).ToList();
        Orders = (Orders ?? []).Select(o => o ?? new Order()).ToList();
        Diagnoses = (Diagnoses ?? []).Where(d => d is not null).ToList();
        FormValues ??= [];

        while (Vitals.Count < VitalRowCount)
        {
            Vitals.Add(new VitalSign());
        }

        while (Orders.Count < OrderRowCount)
        {
            Orders.Add(new Order());
        }
    }
}

public sealed class VitalSign
{
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public string Sbp { get; set; } = "";
    public string Dbp { get; set; } = "";
    public string Pulse { get; set; } = "";
    public string Temp { get; set; } = "";
}

public sealed class Diagnosis
{
    public string Date { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class Order
{
    public string Name { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string Days { get; set; } = "";
    public string Route { get; set; } = "";
}
