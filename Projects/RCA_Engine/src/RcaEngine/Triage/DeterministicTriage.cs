using RcaEngine.Models;
using RcaEngine.Parsing;
using RcaEngine.Telemetry;

namespace RcaEngine.Triage;

/// <summary>
/// Rule-based fault isolation used as (a) an offline mode when no LLM is available and
/// (b) a cross-check / fallback for the agent path. Scores each failure category from
/// the parsed telemetry and picks the strongest signal.
/// </summary>
public static class DeterministicTriage
{
    public static RcaReport Run(DiagnosticBundle bundle)
    {
        var scores = new Dictionary<FailureCategory, (double Score, List<EvidenceItem> Evidence, string Component)>
        {
            [FailureCategory.MemorySubsystem] = ScoreMemory(bundle),
            [FailureCategory.StorageNvme] = ScoreNvme(bundle),
            [FailureCategory.PcieLink] = ScorePcieLink(bundle),
            [FailureCategory.PcieEnumeration] = ScoreEnumeration(bundle),
            [FailureCategory.DpuOffload] = ScoreOffload(bundle),
        };

        var best = scores.OrderByDescending(kv => kv.Value.Score).First();
        if (best.Value.Score <= 0)
        {
            return new RcaReport
            {
                Category = FailureCategory.Unknown,
                Summary = "No dominant fault signature found in the diagnostic bundle.",
                RootCause = "Insufficient evidence for automated isolation; manual review required.",
                Confidence = 0.1,
                GeneratedBy = "deterministic"
            };
        }

        var (score, evidence, component) = best.Value;
        var totalScore = scores.Values.Sum(v => Math.Max(0, v.Score));
        var confidence = Math.Clamp(score / Math.Max(totalScore, 1e-9), 0, 0.95);

        var report = best.Key switch
        {
            FailureCategory.MemorySubsystem => new RcaReport
            {
                Category = FailureCategory.MemorySubsystem,
                FailingComponent = component,
                Summary = $"Corrected-error storm on memory module {component}: sustained EDAC corrected errors plus BMC ECC events indicate a degrading DIMM.",
                RootCause = $"DRAM device degradation on {component} producing a high corrected-ECC error rate (predictive failure).",
                CustomerImpact = "No data corruption yet (errors are corrected), but interrupt storm steals CPU cycles and the module is at elevated risk of an uncorrectable fault.",
                RecommendedActions =
                [
                    $"Schedule proactive replacement of {component} at next service window",
                    "Enable/verify memory page offlining for the affected ranks",
                    "Migrate latency-sensitive workloads off the node until serviced"
                ],
            },
            FailureCategory.StorageNvme => new RcaReport
            {
                Category = FailureCategory.StorageNvme,
                FailingComponent = component,
                Summary = $"NVMe controller failure on {component}: I/O timeouts escalating to controller-down and device removal, corroborated by BMC drive health events.",
                RootCause = $"NVMe drive controller on {component} stopped responding (controller hang/failure), not a PCIe fabric or host-side issue.",
                CustomerImpact = "I/O to the affected namespace failed; workloads on that drive saw errors or stalls until failover.",
                RecommendedActions =
                [
                    $"Replace the NVMe drive at {component}",
                    "Verify drive firmware level against known-issue list before redeploying the model",
                    "Check RAID/replication health and resync after replacement"
                ],
            },
            FailureCategory.PcieLink => new RcaReport
            {
                Category = FailureCategory.PcieLink,
                FailingComponent = component,
                Summary = $"PCIe physical-layer degradation on {component}: correctable AER error storm (receiver errors / bad TLP/DLLP) with link downtraining.",
                RootCause = $"Signal-integrity fault on the PCIe link to {component} — consistent with a marginal connector, riser, or trace rather than endpoint logic failure.",
                CustomerImpact = "Link retraining and replay reduce effective bandwidth and add latency; risk of escalation to uncorrectable link errors.",
                RecommendedActions =
                [
                    $"Reseat the device/riser at {component}; inspect connector",
                    "If errors persist after reseat, replace the riser/cable, then the endpoint device",
                    "Compare correctable-error counters before/after service to confirm fix"
                ],
            },
            FailureCategory.PcieEnumeration => new RcaReport
            {
                Category = FailureCategory.PcieEnumeration,
                FailingComponent = component,
                Summary = $"PCIe function enumeration failure on {component}: config-space reads failing / functions missing, with device firmware stuck in initialization.",
                RootCause = $"The device at {component} failed to enumerate — config space unreadable and firmware init timing out. Fault is in the endpoint device (likely DPU firmware/hardware), not the host PCIe fabric.",
                CustomerImpact = "Network/accelerator functions backed by the device are absent from the host; dependent workloads cannot start or lost connectivity at boot.",
                RecommendedActions =
                [
                    $"Attempt DPU firmware recovery / cold reset (power-cycle, not warm reboot) of {component}",
                    "If functions still fail to enumerate, collect device firmware logs and RMA the device",
                    "Verify slot with a known-good device to exonerate the riser/socket"
                ],
            },
            FailureCategory.DpuOffload => new RcaReport
            {
                Category = FailureCategory.DpuOffload,
                FailingComponent = component,
                Summary = $"DPU offload-engine degradation on {component}: flow-rule programming failures forced the datapath to fall back to the host CPU.",
                RootCause = $"The offload engine on {component} is rejecting flow-table programming (resource exhaustion / bad resource state). Traffic is being processed on the host datapath instead of in hardware — the PCIe link itself is healthy.",
                CustomerImpact = "No outage, but throughput drops and host CPU utilization spikes while offload is bypassed; latency-sensitive tenants see degradation.",
                RecommendedActions =
                [
                    $"Restart the offload/steering agent and reprogram flow tables on {component}",
                    "Check DPU firmware version against known flow-table-leak issues; upgrade if applicable",
                    "Cap offloaded-flow scale or drain latency-sensitive workloads until offload is restored"
                ],
            },
            _ => new RcaReport { Category = FailureCategory.Unknown }
        };

        report.Evidence = evidence;
        report.Confidence = Math.Round(confidence, 2);
        report.GeneratedBy = "deterministic";
        return report;
    }

    private static (double, List<EvidenceItem>, string) ScoreMemory(DiagnosticBundle bundle)
    {
        var evidence = new List<EvidenceItem>();
        double score = 0;

        var ceLines = bundle.DmesgLines.Where(l => l.Category == DmesgCategory.MemoryCorrectedError).ToList();
        if (ceLines.Count >= 5)
        {
            score += ceLines.Count;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{ceLines.Count} EDAC corrected-error lines, e.g.: {ceLines[0].Message}" });
        }

        var ueLines = bundle.DmesgLines.Count(l => l.Category == DmesgCategory.MemoryUncorrectedError);
        if (ueLines > 0)
        {
            score += ueLines * 20;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{ueLines} uncorrected memory error lines" });
        }

        var memEvents = bundle.RedfishEvents
            .Where(e => e.MessageId.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (memEvents.Count > 0)
        {
            score += memEvents.Count * 2;
            evidence.Add(new EvidenceItem { Source = "redfish", Detail = $"{memEvents.Count} memory events, e.g.: {memEvents[^1].MessageId} @ {memEvents[^1].OriginOfCondition}" });
        }

        var component = memEvents
            .GroupBy(e => e.OriginOfCondition)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key.Split('/').LastOrDefault() ?? ExtractDimmFromDmesg(ceLines);

        return (score, evidence, component ?? "unknown DIMM");
    }

    private static string? ExtractDimmFromDmesg(List<DmesgLine> ceLines)
    {
        var match = ceLines
            .Select(l => System.Text.RegularExpressions.Regex.Match(l.Message, @"(CPU_SrcID#\d+_MC#\d+_Chan#\d+_DIMM#\d+|DIMM[_\s]?\w+)"))
            .FirstOrDefault(m => m.Success);
        return match?.Value;
    }

    private static (double, List<EvidenceItem>, string) ScoreNvme(DiagnosticBundle bundle)
    {
        var evidence = new List<EvidenceItem>();
        double score = 0;

        var nvmeLines = bundle.DmesgLines.Where(l => l.Category == DmesgCategory.NvmeError).ToList();
        if (nvmeLines.Count > 0)
        {
            score += nvmeLines.Count * 4;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{nvmeLines.Count} NVMe error lines, e.g.: {nvmeLines[0].Message}" });

            if (nvmeLines.Any(l => l.Message.Contains("controller is down", StringComparison.OrdinalIgnoreCase)))
            {
                score += 25;
                evidence.Add(new EvidenceItem { Source = "dmesg", Detail = "NVMe controller-down event (CSTS unreadable) — controller-level failure" });
            }
        }

        var ioLines = bundle.DmesgLines.Count(l => l.Category == DmesgCategory.IoError);
        if (ioLines > 0 && nvmeLines.Count > 0)
        {
            score += ioLines * 2;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{ioLines} downstream block-layer I/O error lines" });
        }

        var driveEvents = bundle.RedfishEvents
            .Where(e => e.MessageId.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
                        e.MessageId.Contains("Drive", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (driveEvents.Count > 0)
        {
            score += driveEvents.Count * 3;
            evidence.Add(new EvidenceItem { Source = "redfish", Detail = $"{driveEvents.Count} drive health events, e.g.: {driveEvents[^1].MessageId} @ {driveEvents[^1].OriginOfCondition}" });
        }

        // Completion timeouts on an NVMe endpoint corroborate a dead controller.
        var nvmeDevice = bundle.PcieDevices.FirstOrDefault(d =>
            d.Description.Contains("NVMe", StringComparison.OrdinalIgnoreCase) &&
            d.DecodedUncorrectableErrors.Count > 0);
        if (nvmeDevice is not null)
        {
            score += 10;
            evidence.Add(new EvidenceItem
            {
                Source = "pcie-aer",
                Detail = $"{nvmeDevice.Address}: uncorrectable AER bits [{string.Join(", ", nvmeDevice.DecodedUncorrectableErrors)}]"
            });
        }

        var component = nvmeDevice?.Address is { } addr
            ? $"{addr} ({nvmeDevice.Description})"
            : driveEvents.LastOrDefault()?.OriginOfCondition.Split('/').LastOrDefault() ?? "nvme0";

        return (score, evidence, component);
    }

    private static (double, List<EvidenceItem>, string) ScorePcieLink(DiagnosticBundle bundle)
    {
        var evidence = new List<EvidenceItem>();
        double score = 0;

        // Physical-layer signature: high correctable counts and/or a downtrained link,
        // on a device that is NOT exhibiting NVMe controller death (that scores under StorageNvme).
        var suspect = bundle.PcieDevices
            .Where(d => d.AerCorrectableCount >= 50 || d.IsLinkDowntrained)
            .OrderByDescending(d => d.AerCorrectableCount)
            .FirstOrDefault();

        if (suspect is not null)
        {
            score += Math.Min(suspect.AerCorrectableCount / 10.0, 40);
            evidence.Add(new EvidenceItem
            {
                Source = "pcie-aer",
                Detail = $"{suspect.Address}: {suspect.AerCorrectableCount} correctable errors [{string.Join(", ", suspect.DecodedCorrectableErrors)}]"
            });
            if (suspect.IsLinkDowntrained)
            {
                score += 20;
                evidence.Add(new EvidenceItem
                {
                    Source = "pcie-aer",
                    Detail = $"{suspect.Address}: link downtrained to {suspect.LinkStatus.Speed} {suspect.LinkStatus.Width} (capable of {suspect.LinkCapabilities.Speed} {suspect.LinkCapabilities.Width})"
                });
            }
        }

        var aerLines = bundle.DmesgLines.Count(l => l.Category == DmesgCategory.PcieAer);
        if (aerLines > 0 && suspect is not null)
        {
            score += aerLines * 2;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{aerLines} pcieport AER corrected-error lines" });
        }

        var component = suspect is not null ? $"{suspect.Address} ({suspect.Description})" : "unknown PCIe device";
        return (score, evidence, component);
    }

    private static (double, List<EvidenceItem>, string) ScoreEnumeration(DiagnosticBundle bundle)
    {
        var evidence = new List<EvidenceItem>();
        double score = 0;

        var enumLines = bundle.DmesgLines.Where(l => l.Category == DmesgCategory.PcieEnumeration).ToList();
        if (enumLines.Count > 0)
        {
            score += enumLines.Count * 8;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{enumLines.Count} enumeration-failure lines, e.g.: {enumLines[0].Message}" });
        }

        var notDetected = bundle.RedfishEvents
            .Where(e => e.MessageId.Contains("DeviceNotDetected", StringComparison.OrdinalIgnoreCase) ||
                        e.MessageId.Contains("FirmwareInit", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (notDetected.Count > 0)
        {
            score += notDetected.Count * 10;
            evidence.Add(new EvidenceItem { Source = "redfish", Detail = $"{notDetected.Count} device-not-detected/firmware-init events, e.g.: {notDetected[^1].MessageId} @ {notDetected[^1].OriginOfCondition}" });
        }

        // Pre-OS corroboration: option-ROM / config-read failures already visible at DXE.
        var bootErrors = bundle.BootProgress
            .Where(e => e.IsError && (e.Message.Contains("OpROM", StringComparison.OrdinalIgnoreCase) ||
                                      e.Message.Contains("config read", StringComparison.OrdinalIgnoreCase) ||
                                      e.Message.Contains("0xffffffff", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (bootErrors.Count > 0 && score > 0)
        {
            score += bootErrors.Count * 5;
            evidence.Add(new EvidenceItem { Source = "boot-progress", Detail = $"Fault visible pre-OS: POST 0x{bootErrors[0].Code:X2} {bootErrors[0].Stage}: {bootErrors[0].Message}" });
        }

        // DPU's own console: boot-chain errors from inside the device settle root cause.
        var dpuBootErrors = bundle.DpuConsole.Where(l => l.Category == DpuConsoleCategory.BootError).ToList();
        if (dpuBootErrors.Count > 0)
        {
            score += dpuBootErrors.Count * 6;
            evidence.Add(new EvidenceItem { Source = "dpu-console", Detail = $"{dpuBootErrors.Count} DPU-internal boot/firmware errors, e.g.: [{dpuBootErrors[0].Subsystem}] {dpuBootErrors[0].Message}" });
        }

        var component =
            notDetected.LastOrDefault()?.OriginOfCondition.Split('/').LastOrDefault()
            ?? ExtractPciAddress(enumLines)
            ?? "unknown PCIe device";
        return (score, evidence, component);
    }

    private static (double, List<EvidenceItem>, string) ScoreOffload(DiagnosticBundle bundle)
    {
        var evidence = new List<EvidenceItem>();
        double score = 0;

        var offloadLines = bundle.DmesgLines.Where(l => l.Category == DmesgCategory.OffloadFallback).ToList();
        if (offloadLines.Count > 0)
        {
            score += offloadLines.Count * 6;
            evidence.Add(new EvidenceItem { Source = "dmesg", Detail = $"{offloadLines.Count} offload-failure/fallback lines, e.g.: {offloadLines[0].Message}" });

            if (offloadLines.Any(l => l.Message.Contains("falling back to host", StringComparison.OrdinalIgnoreCase)))
            {
                score += 15;
                evidence.Add(new EvidenceItem { Source = "dmesg", Detail = "Explicit fallback-to-host-datapath event — hardware offload bypassed" });
            }
        }

        var offloadEvents = bundle.RedfishEvents
            .Where(e => e.MessageId.Contains("Offload", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (offloadEvents.Count > 0)
        {
            score += offloadEvents.Count * 5;
            evidence.Add(new EvidenceItem { Source = "redfish", Detail = $"{offloadEvents.Count} offload-engine events, e.g.: {offloadEvents[^1].MessageId} @ {offloadEvents[^1].OriginOfCondition}" });
        }

        // DPU's own console confirming resource exhaustion from inside the offload engine.
        var dpuOffloadErrors = bundle.DpuConsole.Where(l => l.Category == DpuConsoleCategory.OffloadError).ToList();
        if (dpuOffloadErrors.Count > 0)
        {
            score += dpuOffloadErrors.Count * 6;
            evidence.Add(new EvidenceItem { Source = "dpu-console", Detail = $"{dpuOffloadErrors.Count} DPU-internal offload-engine errors, e.g.: [{dpuOffloadErrors[0].Subsystem}] {dpuOffloadErrors[0].Message}" });
        }

        // A healthy link on the implicated adapter strengthens "offload engine, not fabric".
        var adapter = bundle.PcieDevices.FirstOrDefault(d =>
            ExtractPciAddress(offloadLines) is { } addr && d.Address.Contains(addr, StringComparison.OrdinalIgnoreCase));
        if (adapter is not null && score > 0 && !adapter.IsLinkDowntrained && adapter.AerUncorrectableStatus == 0)
            evidence.Add(new EvidenceItem { Source = "pcie-aer", Detail = $"{adapter.Address}: link {adapter.LinkStatus.Speed} {adapter.LinkStatus.Width}, AER clean — fabric exonerated" });

        var component = adapter is not null
            ? $"{adapter.Address} ({adapter.Description})"
            : offloadEvents.LastOrDefault()?.OriginOfCondition.Split('/').LastOrDefault()
              ?? ExtractPciAddress(offloadLines) ?? "unknown adapter";
        return (score, evidence, component);
    }

    private static string? ExtractPciAddress(List<DmesgLine> lines)
    {
        var match = lines
            .Select(l => System.Text.RegularExpressions.Regex.Match(l.Message, @"[0-9a-f]{4}:[0-9a-f]{2}:[0-9a-f]{2}\.\d"))
            .FirstOrDefault(m => m.Success);
        return match?.Value;
    }
}
