$ErrorActionPreference = "Stop"

$BenchProj = "benchmarks/CodeDraw.Net.Benchmarks"
$OutDir   = "docs/perf"

dotnet run -c Release --project $BenchProj -- --join

$reports = Get-ChildItem "$BenchProj/BenchmarkDotNet.Artifacts/results" -Filter "*-report-github.md" | Sort-Object LastWriteTime -Descending
if (-not $reports) { throw "No BenchmarkDotNet GitHub report found." }
$report = $reports[0].FullName

$date = Get-Date -Format "yyyy-MM-dd_HH-mm"
$sha  = (git rev-parse --short HEAD).Trim()

$base = [System.IO.Path]::GetFileNameWithoutExtension($report)        # e.g., DrawBench-report-github
$name = $base -replace "-report-github$", ""                          # e.g., DrawBench
$dest = Join-Path $OutDir "$name`_$date`_$sha.md"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item $report $dest -Force

git add $dest
git commit -m "perf: $name @ $date ($sha) [report]"
