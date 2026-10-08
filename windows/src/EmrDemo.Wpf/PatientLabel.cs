using EmrDemo.Core;

namespace EmrDemo.Wpf;

static class PatientLabel
{
    /// <summary>환자 목록 항목의 UIA 이름. WinForms판 목록 글자와 같다(id 뒤 공백 두 칸).</summary>
    public static string For(Patient patient) => $"{patient.Id}  {patient.Name}  {patient.Department}";
}
