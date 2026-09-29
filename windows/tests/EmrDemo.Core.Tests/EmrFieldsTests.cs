using EmrDemo.Core;

namespace EmrDemo.Core.Tests;

public class EmrFieldsTests
{
    [Fact]
    public void Paths_UseCamelCasePropertyNamesAndRowIndex()
    {
        Assert.Equal("chartText", EmrFields.ChartText);
        Assert.Equal("medicalMemo", EmrFields.MedicalMemo);
        Assert.Equal("patientMemo", EmrFields.PatientMemo);
        Assert.Equal("orders[0].frequency", EmrFields.Order(0, nameof(Order.Frequency)));
        Assert.Equal("vitals[4].temp", EmrFields.Vital(4, nameof(VitalSign.Temp)));
    }
}
