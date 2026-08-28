param(
    [int]$ProcessId = 3396,
    [string]$AnchorAddress = "0x7FF4DACFFF1C",
    [string]$ProfilePath = "docs/pes2021/competition-fixtures/examples/pes2021-fixture-profile.example.json",
    [string]$CompetitionMap = "docs/pes2021/competition-fixtures/examples/competition-map.example.csv",
    [string]$TeamMap = "docs/pes2021/competition-fixtures/examples/competition-17-team-map.csv",
    [string]$OutputFile = "docs/pes2021/competition-fixtures/benchmark-results.csv"
)

$commit = (git rev-parse HEAD).Trim()
$profileSha256 = (Get-FileHash $ProfilePath).Hash.ToLowerInvariant()
$recordLimit = 13014

$results = [System.Collections.Generic.List[PSObject]]::new()

function Run-Extraction([string]$variant, [int]$run, [int]$blockRecords) {
    Write-Host "Running $variant (run $run, blockRecords=$blockRecords)..."
    $tmpOutput = [System.IO.Path]::GetTempFileName()
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    
    $cliOutput = dotnet run --project src/Overmem.Cli --no-build -- `
        pes2021-extract-competition-fixtures `
        --pid $ProcessId `
        --competition-id 17 `
        --anchor-address $AnchorAddress `
        --competition-block-base-address $AnchorAddress `
        --profile-file $ProfilePath `
        --competition-map-file $CompetitionMap `
        --team-map-file $TeamMap `
        --block-records $blockRecords `
        --output-file $tmpOutput 2>&1
    
    $sw.Stop()
    
    if (-not (Test-Path $tmpOutput)) {
        Write-Warning "Failed run for $variant : $cliOutput"
        return $null
    }

    $json = Get-Content $tmpOutput | ConvertFrom-Json
    Remove-Item $tmpOutput -Force -ErrorAction SilentlyContinue

    $diag = $json.diagnostics
    $durationMs = if ($diag.stageDurationMs.read_blocks) { [double]$diag.stageDurationMs.read_blocks } else { $sw.Elapsed.TotalMilliseconds }

    $obj = [PSCustomObject]@{
        variant = $variant
        run = $run
        record_limit = $recordLimit
        block_records = $blockRecords
        read_calls = $diag.readCalls
        bytes_requested = $diag.bytesRequested
        bytes_read = $diag.bytesRead
        duration_ms = [math]::Round($durationMs, 2)
        fixture_count = $json.fixtureCount
        process_id = $ProcessId
        profile_sha256 = $profileSha256
        overmem_commit = $commit
    }

    return $obj
}

Write-Host "--- Warmup Round ---"
Run-Extraction "blocks-1024" 0 1024 | Out-Null
Run-Extraction "blocks-512" 0 512 | Out-Null
Run-Extraction "legacy" 0 1 | Out-Null

Write-Host "--- Benchmark Runs (5 per variant, alternating) ---"
for ($run = 1; $run -le 5; $run++) {
    $res1024 = Run-Extraction "blocks-1024" $run 1024
    if ($res1024) { $results.Add($res1024) }

    $res512 = Run-Extraction "blocks-512" $run 512
    if ($res512) { $results.Add($res512) }

    $resLeg = Run-Extraction "legacy" $run 1
    if ($resLeg) { $results.Add($resLeg) }
}

$results | Export-Csv -Path $OutputFile -NoTypeInformation
Write-Host "Benchmark results saved to $OutputFile"

Write-Host "`n--- Summary Metrics (Median & P95) ---"
$grouped = $results | Group-Object variant
foreach ($g in $grouped) {
    $durations = $g.Group | Select-Object -ExpandProperty duration_ms | Sort-Object
    $count = $durations.Count
    $median = $durations[[math]::Floor($count / 2)]
    $p95Index = [math]::Min($count - 1, [math]::Floor($count * 0.95))
    $p95 = $durations[$p95Index]
    $avgCalls = ($g.Group | Measure-Object -Property read_calls -Average).Average
    Write-Host "Variant: $($g.Name.PadRight(12)) | Median: $($median.ToString().PadLeft(8)) ms | P95: $($p95.ToString().PadLeft(8)) ms | Avg Read Calls: $avgCalls"
}
