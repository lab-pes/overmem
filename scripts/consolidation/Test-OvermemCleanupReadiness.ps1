param(
    [string]$PlanPath = 'docs/consolidation/cleanup-plan.json',
    [string]$OutputPath = 'artifacts/consolidation-2026-09-12/cleanup-readiness-latest.json',
    [switch]$ConfirmSavedProjectRemoved,
    [switch]$ConfirmTargetsClosed
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$plan = Get-Content -LiteralPath (Join-Path $repoRoot $PlanPath) -Raw | ConvertFrom-Json
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $(if ($Passed) { 'PASS' } else { 'FAIL' }); detail = $Detail })
}
function Invoke-Checked([string]$Name, [scriptblock]$Action) {
    try { $detail = & $Action 2>&1 | Out-String; Add-Check $Name $true $detail.Trim() }
    catch { Add-Check $Name $false $_.Exception.Message }
}

$canonical = [IO.Path]::GetFullPath($plan.canonicalRepository)
$primaryVault = [IO.Path]::GetFullPath($plan.primaryVault)
$secondaryVault = [IO.Path]::GetFullPath($plan.secondaryVault)
Add-Check 'canonical-path' ($repoRoot.TrimEnd('\') -ieq $canonical.TrimEnd('\')) $repoRoot
Add-Check 'independent-secondary-vault' (-not $secondaryVault.StartsWith($canonical + '\', [StringComparison]::OrdinalIgnoreCase)) $secondaryVault
$targetPaths = @($plan.targets | ForEach-Object { [IO.Path]::GetFullPath($_.path).TrimEnd('\') })
Add-Check 'canonical-not-a-target' ($canonical.TrimEnd('\') -notin $targetPaths) $canonical
Add-Check 'backup-not-a-target' ($secondaryVault.TrimEnd('\') -notin $targetPaths) $secondaryVault

Invoke-Checked 'git-integrity' { git -C $canonical fsck --full --no-dangling; if ($LASTEXITCODE) { throw 'git fsck failed' }; 'git fsck passed' }
$branch = (git -C $canonical branch --show-current).Trim()
Add-Check 'canonical-branch' ($branch -eq $plan.requiredBranch) $branch
$localHead = (git -C $canonical rev-parse HEAD).Trim()
$remoteHead = ((git -C $canonical ls-remote origin refs/heads/master) -split '\s+')[0]
Add-Check 'canonical-synchronized' ($localHead -eq $remoteHead) "local=$localHead remote=$remoteHead"
git -C $canonical merge-base --is-ancestor $plan.requiredIntegrationCommit HEAD
Add-Check 'integration-is-ancestor' ($LASTEXITCODE -eq 0) $plan.requiredIntegrationCommit
$trackedStatus = git -C $canonical status --porcelain=v1 --untracked-files=no
Add-Check 'tracked-worktree-clean' ([string]::IsNullOrWhiteSpace(($trackedStatus | Out-String))) ($trackedStatus | Out-String).Trim()

$validation = Get-Content -LiteralPath (Join-Path $canonical 'docs/consolidation/validation.json') -Raw | ConvertFrom-Json
foreach ($subtree in @('src', 'tests', 'scripts')) {
    $actual = (git -C $canonical rev-parse "HEAD:$subtree").Trim()
    $expected = $validation.testedGitSubtrees.$subtree
    Add-Check "validated-subtree-$subtree" ($actual -eq $expected) "actual=$actual expected=$expected"
}

foreach ($vault in @($primaryVault, $secondaryVault)) {
    Invoke-Checked "preservation-$vault" {
        & python (Join-Path $canonical 'scripts/consolidation/verify_preservation.py') --repo $canonical --vault $vault
        if ($LASTEXITCODE) { throw 'preservation verification failed' }
    }
    foreach ($bundle in @('overmem-before.bundle', 'legacy-dgit.bundle', 'canonical-master.bundle')) {
        Invoke-Checked "bundle-$bundle-$vault" {
            git bundle verify (Join-Path $vault $bundle)
            if ($LASTEXITCODE) { throw 'bundle verification failed' }
        }
    }
}

Invoke-Checked 'source-snapshots-unchanged' {
    & python (Join-Path $canonical 'scripts/consolidation/verify_source_snapshots.py') `
        --manifest (Join-Path $canonical 'docs/consolidation/preservation-manifest.json') --canonical-root $canonical
    if ($LASTEXITCODE) { throw 'one or more source directories changed after preservation' }
}

$oldPaths = @($plan.targets | ForEach-Object { $_.path })
$configurationHits = [Collections.Generic.List[string]]::new()
foreach ($config in $plan.activeConfigurationFiles) {
    if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { continue }
    $content = Get-Content -LiteralPath $config -Raw
    foreach ($oldPath in $oldPaths) {
        if ($content.IndexOf($oldPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $content.IndexOf($oldPath.Replace('\', '\\'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $configurationHits.Add("$config -> $oldPath")
        }
    }
}
Add-Check 'active-configurations-migrated' ($configurationHits.Count -eq 0) ($configurationHits -join [Environment]::NewLine)
Add-Check 'saved-codex-project-removed' $ConfirmSavedProjectRemoved.IsPresent 'Attestation required immediately before cleanup.'
Add-Check 'target-tasks-and-terminals-closed' $ConfirmTargetsClosed.IsPresent 'Attestation required immediately before cleanup.'

foreach ($target in $plan.targets) {
    $exists = Test-Path -LiteralPath $target.path -PathType Container
    Add-Check "target-present-$($target.source)" $exists $target.path
}

$failures = @($checks | Where-Object status -eq 'FAIL')
$result = [ordered]@{
    schemaVersion = 1
    generatedAt = [DateTimeOffset]::Now.ToString('o')
    status = $(if ($failures.Count -eq 0) { 'READY' } else { 'BLOCKED' })
    canonicalCommit = $localHead
    targetCount = @($plan.targets).Count
    warning = 'This report authorizes no deletion. The two confirmation switches are just-in-time attestations, not deletion authorization.'
    checks = $checks
}
$absoluteOutput = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteOutput) -Force | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $absoluteOutput -Encoding utf8
$result | ConvertTo-Json -Depth 8
if ($failures.Count -gt 0) { exit 1 }
