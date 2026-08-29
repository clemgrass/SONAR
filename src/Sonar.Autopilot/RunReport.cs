using System.Collections.Generic;
using System.Text;

namespace Sonar.Autopilot;

/// <summary>Una oleada, cerrada. Los números que el test no afirma.</summary>
public sealed record WaveRow(
    int Wave, int StartTick, int Ticks, int EnemiesLeft,
    int Shots, int Kills, int Threats, int Commands,
    double PeakSaturation, double EndSaturation);

/// <summary>
/// La corrida entera, en números. Es la evidencia de las preguntas 3, 7 y 8 de M1, y va a
/// stdout — no se escribe en el repositorio.
/// </summary>
public sealed record RunReport(
    int Seed,
    double PaceSeconds,
    int DeathTick,
    string DeathCause,
    int DeathWave,
    double DeathSaturation,
    IReadOnlyList<WaveRow> Waves,
    IReadOnlyList<double> SaturationPerSecond)
{
    /// <summary>Índice de saturación en el segundo dado, o cero si la corrida no llegó ahí.</summary>
    public double SaturationAtSecond(int second) =>
        second >= 0 && second < SaturationPerSecond.Count ? SaturationPerSecond[second] : 0.0;

    public string Render()
    {
        var text = new StringBuilder();
        text.AppendLine($"seed {Seed} · pace {PaceSeconds:F1}s");
        text.AppendLine("WAVE  START   TICKS  LEFT  SHOTS  KILLS  THREATS  CMDS   PEAK-S   END-S");

        foreach (var wave in Waves)
        {
            text.AppendLine(
                $"{wave.Wave,4}  {wave.StartTick,6}  {wave.Ticks,6}  {wave.EnemiesLeft,4}  " +
                $"{wave.Shots,5}  {wave.Kills,5}  {wave.Threats,7}  {wave.Commands,4}  " +
                $"{wave.PeakSaturation,7:F2}  {wave.EndSaturation,6:F2}");
        }

        text.AppendLine(
            $"DEATH  tick {DeathTick} · {DeathCause} · wave {DeathWave} · S {DeathSaturation:F2}");
        return text.ToString();
    }
}
