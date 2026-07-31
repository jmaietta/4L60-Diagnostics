using System.Globalization;
using System.Net;
using System.Text;
using LT1Diagnostics.Domain.Diagnostics;

namespace LT1Diagnostics.Reporting;

public static class DiagnosticReportGenerator
{
    public static string GenerateHtml(DiagnosticReportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var output = new StringBuilder();
        output.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>Maietta Diagnostics — GM 4L60E report</title><style>")
            .Append("body{font:15px system-ui,sans-serif;color:#0f172a;background:#f8fafc;margin:0}main{max-width:980px;margin:36px auto;background:white;padding:42px;border:1px solid #e2e8f0;border-radius:12px}h1{font-size:30px;margin:0 0 6px}h2{font-size:20px;margin-top:34px}.meta{color:#475569}.notice{background:#fffbeb;border:1px solid #fde68a;padding:14px 16px;border-radius:8px;margin:24px 0}table{width:100%;border-collapse:collapse;margin-top:14px}th,td{text-align:left;padding:10px 12px;border-bottom:1px solid #e2e8f0}th{font-size:12px;letter-spacing:.06em;color:#475569;background:#f8fafc}.dtc{border:1px solid #e2e8f0;border-radius:8px;padding:18px;margin:12px 0}.code{color:#1d4ed8;font-weight:700}footer{margin-top:36px;color:#64748b;font-size:13px}@media print{body{background:white}main{border:0;margin:0;padding:0}}</style></head><body><main>")
            .Append("<h1>Maietta Diagnostics</h1><div class=\"meta\"><strong>GM 4L60E diagnostic report</strong><br>")
            .Append(E(input.Vehicle)).Append(" · ").Append(E(input.GeneratedAt.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)))
            .Append("</div><div class=\"notice\"><strong>").Append(E(input.EvidenceLabel)).Append("</strong><br>")
            .Append(E(input.Analysis.InterpretationBoundary)).Append("</div>")
            .Append("<h2>Session</h2><table><tbody>")
            .Append(Row("File", input.SessionFileName))
            .Append(Row("Data quality", input.DataQuality))
            .Append(Row("Samples", input.Analysis.SampleCount.ToString(CultureInfo.InvariantCulture)))
            .Append(Row("Duration", input.Analysis.Duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " seconds"))
            .Append(Row("Observed state changes", input.Analysis.Events.Count.ToString(CultureInfo.InvariantCulture)))
            .Append("</tbody></table><h2>Trouble codes</h2>");

        if (input.TroubleCodes.Count == 0)
        {
            output.Append("<p>No logged transmission DTC flags were present in the decoded sample. This is not a complete clean bill of health.</p>");
        }
        else
        {
            foreach (DiagnosticReportDtc dtc in input.TroubleCodes)
            {
                output.Append("<section class=\"dtc\"><div class=\"code\">Code ")
                    .Append(dtc.Code.ToString(CultureInfo.InvariantCulture)).Append(" · ").Append(E(dtc.EvidenceStatus))
                    .Append("</div><h3>").Append(E(dtc.Title)).Append("</h3><p>").Append(E(dtc.Meaning))
                    .Append("</p><strong>Possible causes, in order</strong><p>").Append(E(dtc.PossibleCauses).Replace("\n", "<br>", StringComparison.Ordinal))
                    .Append("</p><strong>First check</strong><p>").Append(E(dtc.NextCheck)).Append("</p></section>");
            }
        }

        output.Append("<h2>Recorded timeline</h2><table><thead><tr><th>Time</th><th>Speed</th><th>Engine</th><th>Commanded gear</th><th>Slip</th><th>Fluid temperature</th></tr></thead><tbody>");
        foreach (TransmissionObservation observation in input.Observations)
        {
            output.Append("<tr><td>").Append(N(observation.Elapsed.TotalSeconds, "0.000")).Append(" s</td><td>")
                .Append(N(observation.VehicleSpeedMph, "0.0")).Append(" mph</td><td>")
                .Append(N(observation.EngineSpeedRpm, "0")).Append(" rpm</td><td>")
                .Append(observation.CommandedGear.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                .Append(N(observation.SlipRpm, "0.0")).Append(" rpm</td><td>")
                .Append(N(observation.TransmissionFluidTemperatureCelsius, "0.0")).Append(" °C</td></tr>");
        }

        output.Append("</tbody></table><footer>Raw bytes remain in the companion .lt1raw session so corrected definitions can re-decode this capture later.</footer></main></body></html>");
        return output.ToString();
    }

    public static string GenerateCsv(IReadOnlyList<TransmissionObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var output = new StringBuilder("elapsed_seconds,vehicle_speed_mph,engine_speed_rpm,commanded_gear,slip_rpm,fluid_temperature_c,ignition_voltage_v,torque_signal_pressure_psi,reference_force_motor_current_a,actual_force_motor_current_a,tcc_commanded,tcc_enabled,shift_solenoid_a,shift_solenoid_b\r\n");
        foreach (TransmissionObservation item in observations)
        {
            output.Append(N(item.Elapsed.TotalSeconds, "0.000000")).Append(',')
                .Append(N(item.VehicleSpeedMph, "0.000")).Append(',')
                .Append(N(item.EngineSpeedRpm, "0.000")).Append(',')
                .Append(item.CommandedGear.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(N(item.SlipRpm, "0.000")).Append(',')
                .Append(N(item.TransmissionFluidTemperatureCelsius, "0.000")).Append(',')
                .Append(N(item.TransmissionIgnitionVoltage, "0.000")).Append(',')
                .Append(N(item.CurrentTorqueSignalPressurePsi, "0.000")).Append(',')
                .Append(N(item.ReferenceForceMotorCurrentAmps, "0.000000")).Append(',')
                .Append(N(item.ActualForceMotorCurrentAmps, "0.000000")).Append(',')
                .Append(item.TccControlCommanded).Append(',')
                .Append(item.TccEnabled).Append(',')
                .Append(item.ShiftSolenoidACommanded).Append(',')
                .Append(item.ShiftSolenoidBCommanded).Append("\r\n");
        }

        return output.ToString();
    }

    private static string Row(string label, string value) => $"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>";

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string N(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
}
